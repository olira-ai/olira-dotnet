#nullable enable

using System.Text.Json;
using Olira.Internal;
using Olira.Json;

namespace Olira;

/// <summary>
/// Sync/async client for the Olira ingestion API. Use for multi-tenant or dependency injection.
/// Module-level <see cref="OliraModule.Init"/> creates a singleton; use <see cref="OliraClient"/>
/// directly for multiple keys.
/// </summary>
public sealed partial class OliraClient : IDisposable, IAsyncDisposable
{
    /// <summary>Default production API base URL.</summary>
    public const string DefaultBaseUrl = "https://app-api.prod.olira.ai/app-api";

    private static readonly Dictionary<string, string> ContentTypeBySuffix = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
        [".tif"] = "image/tiff",
        [".tiff"] = "image/tiff",
        [".gif"] = "image/gif",
    };

    private readonly string _apiKey;
    private readonly OliraEnv _environment;
    private readonly string? _serviceName;
    private readonly string? _project;
    private readonly string _baseUrl;
    private readonly bool _asyncFlush;
    private readonly Dictionary<string, string> _context;
    private readonly HttpTransport _transport;
    private BackgroundWorker? _worker;
    private bool _disposed;

    /// <summary>Creates an Olira client.</summary>
    /// <param name="apiKey">Bearer API key.</param>
    /// <param name="environment">Event-routing environment (<c>production</c> / <c>development</c>).</param>
    /// <param name="serviceName">Optional service name stamped into log context.</param>
    /// <param name="project">Optional project id/slug (<c>X-Olira-Project</c>).</param>
    /// <param name="baseUrl">API base URL.</param>
    /// <param name="batchSize">Background flush batch size.</param>
    /// <param name="flushInterval">Max seconds between background flushes.</param>
    /// <param name="maxQueueSize">Bounded log queue capacity.</param>
    /// <param name="timeout">HTTP timeout in seconds.</param>
    /// <param name="maxRetries">Max HTTP retries for retryable failures.</param>
    /// <param name="onError"><c>"drop"</c>, <c>"raise"</c>, or an error callback.</param>
    /// <param name="asyncFlush">When true, logs are queued on a background worker.</param>
    /// <param name="httpHandler">
    /// Optional message handler (e.g. for unit tests with MockHttp). When null, a default handler is used.
    /// </param>
    public OliraClient(
        string apiKey,
        OliraEnv environment = OliraEnv.Production,
        string? serviceName = null,
        string? project = null,
        string baseUrl = DefaultBaseUrl,
        int batchSize = 50,
        double flushInterval = 1.5,
        int maxQueueSize = 10_000,
        double timeout = 5.0,
        int maxRetries = 3,
        object? onError = null,
        bool asyncFlush = true,
        HttpMessageHandler? httpHandler = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        _apiKey = apiKey;
        _environment = environment;
        _serviceName = serviceName;
        _project = project;
        _baseUrl = baseUrl;
        _asyncFlush = asyncFlush;
        _context = BuildContext(environment, serviceName, project);

        _transport = new HttpTransport(
            baseUrl,
            apiKey,
            timeout: TimeSpan.FromSeconds(timeout),
            maxRetries: maxRetries,
            project: project,
            handler: httpHandler);

        if (asyncFlush)
        {
            _worker = new BackgroundWorker(
                sendBatch: logs => _transport.SendBatch(logs),
                batchSize: batchSize,
                flushInterval: flushInterval,
                maxQueueSize: maxQueueSize,
                onError: onError ?? "drop");
            _worker.Start();
        }
    }

    /// <summary>SDK context stamped onto every queued log.</summary>
    public IReadOnlyDictionary<string, string> Context => _context;

    internal HttpTransport Transport => _transport;

    private static Dictionary<string, string> BuildContext(
        OliraEnv environment,
        string? serviceName,
        string? project)
    {
        var ctx = new Dictionary<string, string>
        {
            ["environment"] = environment.ToWireValue(),
            ["service"] = serviceName ?? "",
            ["sdk_version"] = VersionInfo.Version,
            ["sdk_language"] = "csharp",
        };
        if (!string.IsNullOrEmpty(project))
        {
            ctx["project"] = project;
        }

        return ctx;
    }

    private static string GuessContentType(string path)
    {
        var ext = Path.GetExtension(path);
        return ContentTypeBySuffix.TryGetValue(ext, out var ct) ? ct : "application/pdf";
    }

    private static Dictionary<string, object?> ToBody(object request)
    {
        var json = JsonSerializer.Serialize(request, OliraJson.Default);
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, OliraJson.Default)
               ?? new Dictionary<string, object?>();
    }

    /// <summary>
    /// Wire dict for <see cref="LogBatch"/> — omits null optional fields
    /// (parity with Python <c>model_dump(mode="json", exclude_none=True)</c>).
    /// </summary>
    private static object ToWireObject(LogWire wire) => ToWireObject(wire, includeNulls: false);

    /// <summary>
    /// Wire dict for the live queue / sync-emit path — includes null optional fields
    /// (parity with Python <c>model_dump(mode="json")</c>).
    /// </summary>
    private static object ToQueuedWireObject(LogWire wire) => ToWireObject(wire, includeNulls: true);

    private static object ToWireObject(LogWire wire, bool includeNulls)
    {
        var options = includeNulls ? OliraJson.IncludeNulls : OliraJson.Default;
        var json = JsonSerializer.Serialize(wire, options);
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, options)!;
    }

    private bool Enqueue(LogWire eventWire)
    {
        if (_worker is not null)
        {
            return _worker.Enqueue(eventWire);
        }

        _transport.SendBatch([ToQueuedWireObject(eventWire)]);
        return true;
    }

    private void Emit(
        string logType,
        string patientId,
        Dictionary<string, object?> payload,
        OliraTrace? trace = null,
        string? timestamp = null,
        Dictionary<string, object?>? metadata = null,
        bool writeBack = false,
        string? writeBackIntegrationId = null)
    {
        var eventWire = new LogWire
        {
            LogType = logType,
            PatientId = patientId,
            Payload = payload,
            Metadata = metadata,
            Context = new Dictionary<string, string>(_context),
            Trace = trace,
            Timestamp = timestamp,
            WriteBack = writeBack,
            WriteBackIntegrationId = writeBackIntegrationId,
        };
        eventWire.Validate();
        Enqueue(eventWire);
    }

    /// <summary>
    /// Enqueue a log for background delivery. Returns immediately.
    /// </summary>
    public void Log(
        string logType,
        string patientId,
        Dictionary<string, object?>? payload = null,
        OliraTrace? trace = null,
        string? timestamp = null,
        Dictionary<string, object?>? metadata = null,
        bool writeBack = false,
        string? writeBackIntegrationId = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Emit(
            logType,
            patientId,
            payload ?? new Dictionary<string, object?>(),
            trace,
            timestamp,
            metadata,
            writeBack,
            writeBackIntegrationId);
    }

    /// <summary>
    /// Submit a single FHIR R4 resource for immediate ingestion.
    /// Raises <see cref="ValidationError"/> if the resource produced no accepted events.
    /// </summary>
    /// <param name="patientId">The patient to log this event for.</param>
    /// <param name="resource">A FHIR R4 JSON resource object with a <c>resourceType</c> field.</param>
    /// <param name="idempotencyKey">
    /// Makes the call safe to retry after a network error or 5xx. Pass the same key
    /// you sent the first time — one key per resource, not per mapped event. A treatment
    /// plan from an EHR can produce several Olira events; Olira applies the key to each.
    /// </param>
    public BatchResult LogFhir(string patientId, object resource, string? idempotencyKey = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var result = _transport.LogFhir(patientId, resource, idempotencyKey);
        if (result.Accepted == 0)
        {
            var msg = result.Errors.Count > 0
                ? result.Errors[0].Message
                : "FHIR resource produced no accepted events";
            throw new ValidationError(msg);
        }

        return result;
    }

    /// <summary>
    /// Send a batch of logs directly, bypassing the background queue.
    /// </summary>
    public BatchResult LogBatch(IReadOnlyList<LogSpec> events)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (events.Count == 0)
        {
            return new BatchResult { Accepted = 0, Failed = 0 };
        }

        var wireEvents = new List<object>(events.Count);
        foreach (var spec in events)
        {
            var wire = LogWire.FromSpec(spec, _context);
            wireEvents.Add(ToWireObject(wire));
        }

        return _transport.SendBatchDirect(wireEvents);
    }

    /// <summary>Block until all queued events are sent (or failed).</summary>
    public void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _worker?.Flush();
    }

    /// <summary>Stop the background worker and close the HTTP client.</summary>
    public void Close() => Dispose();

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_worker is not null)
        {
            _worker.Close();
            _worker = null;
        }

        _transport.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
