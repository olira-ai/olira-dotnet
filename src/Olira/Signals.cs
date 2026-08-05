#nullable enable

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Olira.Internal;

namespace Olira;

/// <summary>
/// Passive signal ingestion: accelerometer / gyroscope / GPS batches.
/// In-memory <c>records</c> are serialized to Parquet via Parquet.Net
/// (<see cref="SerializeSignalRecords"/>); callers may also pass pre-built <c>parquet</c> bytes.
/// </summary>
public static partial class Signals
{
    /// <summary>
    /// Fallback sync-door body cap when GET /v1/sdk/config is unavailable.
    /// The server enforces the real limit; this only picks the routing.
    /// </summary>
    public const int DefaultSyncBodyCapBytes = 32 * 1024 * 1024;

    /// <summary>Shared implementation behind <c>OliraClient.SendSignals</c>.</summary>
    public static SignalJobHandle SendSignalsViaTransport(
        HttpTransport transport,
        string patientId,
        SignalSensorType sensorType,
        string sourceDevice,
        IReadOnlyList<Dictionary<string, object?>>? records = null,
        byte[]? parquet = null,
        string? schemaVersion = null,
        double? sampleRateHz = null,
        IReadOnlyDictionary<string, string>? units = null,
        string? timestampUnit = null,
        string? deviceTimezone = null)
    {
        if ((records is null) == (parquet is null))
        {
            throw new ValidationError("Provide exactly one of 'records' or 'parquet'");
        }

        var blob = parquet ?? SerializeSignalRecords(records!);
        var sha256 = Convert.ToHexString(SHA256.HashData(blob)).ToLowerInvariant();
        var metadata = BuildBatchMetadata(sampleRateHz, units, timestampUnit, deviceTimezone);

        int syncCap;
        try
        {
            var sdkConfig = transport.GetSdkConfig();
            syncCap = sdkConfig.TryGetValue("signals_max_sync_body_bytes", out var capEl)
                      && capEl.ValueKind == JsonValueKind.Number
                      && capEl.TryGetInt32(out var cap)
                ? cap
                : DefaultSyncBodyCapBytes;
        }
        catch
        {
            syncCap = DefaultSyncBodyCapBytes;
        }

        var descriptor = new Dictionary<string, object?>
        {
            ["patient_id"] = patientId,
            ["sensor_type"] = sensorType.ToWireValue(),
            ["source_device"] = sourceDevice,
            ["content_sha256"] = sha256,
            ["size_bytes"] = blob.Length,
            ["batch_metadata"] = metadata,
        };
        if (!string.IsNullOrEmpty(schemaVersion))
        {
            descriptor["schema_version"] = schemaVersion;
        }

        if (blob.Length <= syncCap)
        {
            var parameters = new Dictionary<string, object?>
            {
                ["patient_id"] = patientId,
                ["sensor_type"] = sensorType.ToWireValue(),
                ["source_device"] = sourceDevice,
            };
            if (!string.IsNullOrEmpty(schemaVersion))
            {
                parameters["schema_version"] = schemaVersion;
            }

            var headers = new Dictionary<string, string>
            {
                ["X-Content-SHA256"] = sha256,
                ["Content-Type"] = "application/vnd.apache.parquet",
            };
            if (metadata.Count > 0)
            {
                headers["X-Olira-Batch-Meta"] = JsonSerializer.Serialize(metadata);
            }

            var raw = transport.SendSignalBatch(parameters, blob, headers);
            var jobId = GetString(raw, "job_id")
                        ?? throw new ServerError("Signal batch response missing job_id");
            var job = transport.GetSignalJob(jobId);
            if (raw.TryGetValue("deduplicated", out var dedup)
                && dedup.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                job.Deduplicated = dedup.GetBoolean();
            }

            return new SignalJobHandle(job, transport.GetSignalJob);
        }

        // Bulk path: presigned PUT + manifest commit.
        var uploadUrls = transport.GetSignalUploadUrls(new Dictionary<string, object?>
        {
            ["files"] = new[] { descriptor },
        });
        if (!uploadUrls.TryGetValue("uploads", out var uploadsEl)
            || uploadsEl.ValueKind != JsonValueKind.Array
            || uploadsEl.GetArrayLength() == 0)
        {
            throw new ServerError("Signal upload-urls response missing uploads[0]");
        }

        var upload = uploadsEl[0];
        var uploadUrl = upload.GetProperty("upload_url").GetString()
                        ?? throw new ServerError("Signal upload missing upload_url");
        transport.PutPresigned(uploadUrl, blob);

        var manifestFile = new Dictionary<string, object?>(descriptor)
        {
            ["batch_id"] = upload.GetProperty("batch_id").GetString(),
            ["lake_key"] = upload.GetProperty("lake_key").GetString(),
        };
        var committed = transport.CommitSignalManifest(new Dictionary<string, object?>
        {
            ["files"] = new[] { manifestFile },
        });
        return new SignalJobHandle(committed, transport.GetSignalJob);
    }

    /// <summary>Parse a sensor type string.</summary>
    public static SignalSensorType ParseSensorType(string sensorType) =>
        sensorType switch
        {
            "accelerometer" => SignalSensorType.Accelerometer,
            "gyroscope" => SignalSensorType.Gyroscope,
            "gps" => SignalSensorType.Gps,
            _ => throw new ValidationError($"Unknown sensor_type {JsonSerializer.Serialize(sensorType)}"),
        };

    private static Dictionary<string, object?> BuildBatchMetadata(
        double? sampleRateHz,
        IReadOnlyDictionary<string, string>? units,
        string? timestampUnit,
        string? deviceTimezone)
    {
        var metadata = new Dictionary<string, object?>();
        if (sampleRateHz is not null)
        {
            metadata["declared_sample_rate_hz"] = sampleRateHz;
        }

        if (units is { Count: > 0 })
        {
            metadata["units"] = units;
        }

        if (!string.IsNullOrEmpty(timestampUnit))
        {
            metadata["timestamp_unit"] = timestampUnit;
        }

        if (!string.IsNullOrEmpty(deviceTimezone))
        {
            metadata["device_timezone"] = deviceTimezone;
        }

        return metadata;
    }

    private static string? GetString(IDictionary<string, JsonElement> dict, string key)
    {
        if (!dict.TryGetValue(key, out var el))
        {
            return null;
        }

        return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
    }
}

/// <summary>Sensors accepted by the v1 signal ingestion doors.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SignalSensorType>))]
public enum SignalSensorType
{
    [JsonStringEnumMemberName("accelerometer")]
    Accelerometer,

    [JsonStringEnumMemberName("gyroscope")]
    Gyroscope,

    [JsonStringEnumMemberName("gps")]
    Gps,
}

/// <summary>Extensions for <see cref="SignalSensorType"/>.</summary>
public static class SignalSensorTypeExtensions
{
    /// <summary>Wire value for the sensor type.</summary>
    public static string ToWireValue(this SignalSensorType sensorType) =>
        sensorType switch
        {
            SignalSensorType.Accelerometer => "accelerometer",
            SignalSensorType.Gyroscope => "gyroscope",
            SignalSensorType.Gps => "gps",
            _ => sensorType.ToString().ToLowerInvariant(),
        };
}

/// <summary>Signal ingestion job status.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SignalJobStatus>))]
public enum SignalJobStatus
{
    [JsonStringEnumMemberName("received")]
    Received,

    [JsonStringEnumMemberName("processing")]
    Processing,

    [JsonStringEnumMemberName("done")]
    Done,

    [JsonStringEnumMemberName("partial")]
    Partial,

    [JsonStringEnumMemberName("failed")]
    Failed,
}

/// <summary>Extensions for <see cref="SignalJobStatus"/>.</summary>
public static class SignalJobStatusExtensions
{
    /// <summary>True when the job finished (done / partial / failed).</summary>
    public static bool IsTerminal(this SignalJobStatus status) =>
        status is SignalJobStatus.Done or SignalJobStatus.Partial or SignalJobStatus.Failed;
}

/// <summary>A signal ingestion job returned by the API.</summary>
public sealed class SignalJob
{
    public string JobId { get; set; } = "";
    public SignalJobStatus Status { get; set; }
    public string Door { get; set; } = "sync";
    public List<string> BatchIds { get; set; } = [];
    public Dictionary<string, string> BatchStatuses { get; set; } = new();
    public Dictionary<string, Dictionary<string, JsonElement>> BatchProgress { get; set; } = new();
    public double? ProgressPct { get; set; }
    public int RecordsDecoded { get; set; }
    public int RecordsValid { get; set; }
    public int RecordsQuarantined { get; set; }
    public int RecordsDeduplicated { get; set; }
    public int RecordsWritten { get; set; }
    public List<string> ErrorSummary { get; set; } = [];
    public string? CreatedAt { get; set; }
    public string? CompletedAt { get; set; }

    /// <summary>True when the upload was a content-hash no-op.</summary>
    public bool Deduplicated { get; set; }
}

/// <summary>Poll/wait handle for a signal ingestion job.</summary>
public sealed class SignalJobHandle
{
    private SignalJob _job;
    private readonly Func<string, SignalJob> _fetch;

    /// <summary>Creates a handle around an initial job snapshot.</summary>
    public SignalJobHandle(SignalJob job, Func<string, SignalJob> fetch)
    {
        _job = job ?? throw new ArgumentNullException(nameof(job));
        _fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));
    }

    /// <summary>Job id.</summary>
    public string JobId => _job.JobId;

    /// <summary>Latest fetched job snapshot.</summary>
    public SignalJob Job => _job;

    /// <summary>Refresh and return the current job state.</summary>
    public SignalJob Poll()
    {
        _job = _fetch(_job.JobId);
        return _job;
    }

    /// <summary>Block until the job reaches a terminal status.</summary>
    public SignalJob Wait(double timeout = 300.0, double interval = 2.0)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeout);
        var job = Poll();
        while (!job.Status.IsTerminal())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new OliraError(
                    $"Signal job {job.JobId} not terminal after {timeout}s (status={job.Status})");
            }

            Thread.Sleep(TimeSpan.FromSeconds(interval));
            job = Poll();
        }

        return job;
    }
}
