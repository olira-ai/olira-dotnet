#nullable enable

using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Olira.Json;

namespace Olira.Internal;

/// <summary>
/// HTTP transport for the Olira ingestion API with retry policy.
/// API keys are never logged (always redacted as <c>olira_***</c>).
/// </summary>
public sealed partial class HttpTransport : IDisposable
{
    private const string RedactedKey = "olira_***";

    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly TimeSpan _timeout;
    private readonly int _maxRetries;
    private readonly HttpClient _client;
    private bool _disposed;

    /// <summary>
    /// Creates an HTTP transport.
    /// </summary>
    /// <param name="baseUrl">API base URL (trailing slash stripped).</param>
    /// <param name="apiKey">Bearer API key.</param>
    /// <param name="timeout">Per-request timeout (default 5s).</param>
    /// <param name="maxRetries">Max retries for retryable failures (default 3).</param>
    /// <param name="project">Optional project id/slug sent as <c>X-Olira-Project</c>.</param>
    /// <param name="handler">
    /// Optional message handler (e.g. for unit tests with MockHttp). When null, a default handler is used.
    /// </param>
    public HttpTransport(
        string baseUrl,
        string apiKey,
        TimeSpan? timeout = null,
        int maxRetries = 3,
        string? project = null,
        HttpMessageHandler? handler = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        _baseUrl = baseUrl.TrimEnd('/');
        _apiKey = apiKey;
        _timeout = timeout ?? TimeSpan.FromSeconds(5);
        _maxRetries = maxRetries;

        _client = handler is null
            ? new HttpClient()
            : new HttpClient(handler, disposeHandler: true);
        _client.BaseAddress = new Uri(_baseUrl + "/");
        _client.Timeout = _timeout;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // CloudFront/WAF in front of app-api rejects requests with no User-Agent (HTML 403).
        // .NET HttpClient sends none by default — set an explicit SDK identity.
        _client.DefaultRequestHeaders.UserAgent.ParseAdd($"olira-dotnet/{VersionInfo.Version}");
        if (!string.IsNullOrEmpty(project))
        {
            // Selects the project (workspace) every request operates in; omitted =
            // the key's own project (locked keys) or the org's default project.
            _client.DefaultRequestHeaders.TryAddWithoutValidation("X-Olira-Project", project);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _client.Dispose();
        GC.SuppressFinalize(this);
    }

    private static string RedactKey(string? apiKey) =>
        string.IsNullOrEmpty(apiKey) ? RedactedKey : RedactedKey;

    private static bool ShouldRetry(int statusCode) =>
        statusCode is 408 or 429 || (statusCode >= 500 && statusCode < 600);

    private static int ParseRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return Math.Max(0, (int)Math.Ceiling(delta.TotalSeconds));
        }

        if (response.Headers.TryGetValues("Retry-After", out var values))
        {
            var value = values.FirstOrDefault() ?? "60";
            return int.TryParse(value, out var seconds) ? seconds : 60;
        }

        return 60;
    }

    private static double BackoffSeconds(int attempt)
    {
        var jitter = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 1000) / 1000.0;
        return Math.Min(Math.Pow(2, attempt) + jitter, 60.0);
    }

    private static string TruncateBody(string text, int max = 500) =>
        text.Length <= max ? text : text[..max];

    private static string FormatQueryValue(object? value) =>
        value switch
        {
            null => string.Empty,
            bool b => b ? "true" : "false",
            IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty,
        };

    private static string AppendQuery(string path, IDictionary<string, object?>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return path;
        }

        var sb = new StringBuilder(path);
        sb.Append(path.Contains('?', StringComparison.Ordinal) ? '&' : '?');
        var first = true;
        foreach (var (key, value) in parameters)
        {
            if (!first)
            {
                sb.Append('&');
            }

            first = false;
            sb.Append(Uri.EscapeDataString(key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(FormatQueryValue(value)));
        }

        return sb.ToString();
    }

    private Uri BuildUri(string path, IDictionary<string, object?>? parameters = null)
    {
        var relative = AppendQuery(path.TrimStart('/'), parameters);
        return new Uri(_client.BaseAddress!, relative);
    }

    private async Task<JsonElement> RequestAsync(
        HttpMethod method,
        string path,
        object? json = null,
        IDictionary<string, object?>? parameters = null,
        bool retryable = true,
        byte[]? content = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // retryable=false for non-idempotent calls (e.g. project create/duplicate):
        // replaying a POST whose response was lost could create a duplicate resource.
        var maxRetries = retryable ? _maxRetries : 0;
        Exception? lastException = null;
        var retryAfterSeconds = 0;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            if (retryAfterSeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(retryAfterSeconds), cancellationToken).ConfigureAwait(false);
            }

            retryAfterSeconds = 0;

            using var request = new HttpRequestMessage(method, BuildUri(path, parameters));

            if (content is not null)
            {
                request.Content = new ByteArrayContent(content);
            }
            else if (json is not null)
            {
                // IncludeNulls so queued log batches keep null optional fields
                // (parity with Python httpx json= which serializes None as null).
                // Callers that omit keys (e.g. ToBody / exclude_none paths) are unchanged.
                var payload = JsonSerializer.Serialize(json, OliraJson.IncludeNulls);
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            }

            if (headers is not null)
            {
                foreach (var (key, value) in headers)
                {
                    if (key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) && request.Content is not null)
                    {
                        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(value);
                    }
                    else if (!request.Headers.TryAddWithoutValidation(key, value) && request.Content is not null)
                    {
                        request.Content.Headers.TryAddWithoutValidation(key, value);
                    }
                }
            }

            HttpResponseMessage response;
            try
            {
                response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                // TaskCanceledException is also thrown on timeout when not user-cancelled.
                if (ex is TaskCanceledException && cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                lastException = new NetworkError(ex.Message, ex);
                if (attempt < maxRetries)
                {
                    var delay = BackoffSeconds(attempt);
                    Debug.WriteLine(
                        $"Request failed (attempt {attempt + 1}/{maxRetries + 1}), retry in {delay:F1}s: {RedactKey(_apiKey)}");
                    await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken).ConfigureAwait(false);
                }

                continue;
            }

            using (response)
            {
                var status = (int)response.StatusCode;

                if (status is 401 or 403)
                {
                    throw new AuthError($"API key rejected (HTTP {status}). Check key validity and scope.");
                }

                if (status == 409)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    throw new ServerError(
                        $"Request rejected (HTTP {status}): {TruncateBody(body)}",
                        statusCode: status);
                }

                if (status is 400 or 404 or 422)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    throw new ValidationError($"Request rejected (HTTP {status}): {TruncateBody(body)}");
                }

                if (status == 429)
                {
                    retryAfterSeconds = ParseRetryAfter(response);
                    if (attempt == maxRetries)
                    {
                        throw new RateLimitError(
                            "Rate limited; retry after backoff",
                            retryAfter: retryAfterSeconds);
                    }

                    Debug.WriteLine(
                        $"Rate limited, retry after {retryAfterSeconds}s ({RedactKey(_apiKey)})");
                    continue;
                }

                if (ShouldRetry(status))
                {
                    if (attempt == maxRetries)
                    {
                        throw new ServerError($"Server error (HTTP {status}) after retries", statusCode: status);
                    }

                    var delay = BackoffSeconds(attempt);
                    Debug.WriteLine(
                        $"Server error {status} (attempt {attempt + 1}/{maxRetries + 1}), retry in {delay:F1}s");
                    await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (status >= 200 && status < 300)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                    if (bytes.Length == 0)
                    {
                        return default;
                    }

                    using var doc = JsonDocument.Parse(bytes);
                    return doc.RootElement.Clone();
                }

                await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                lastException = new ServerError($"Unexpected HTTP {status}", statusCode: status);
                break;
            }
        }

        if (lastException is not null)
        {
            throw lastException;
        }

        return default;
    }

    private JsonElement Request(
        HttpMethod method,
        string path,
        object? json = null,
        IDictionary<string, object?>? parameters = null,
        bool retryable = true,
        byte[]? content = null,
        IDictionary<string, string>? headers = null) =>
        RequestAsync(method, path, json, parameters, retryable, content, headers)
            .GetAwaiter()
            .GetResult();

    private static T DeserializeRequired<T>(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new ServerError("Empty response body where a payload was required");
        }

        return element.Deserialize<T>(OliraJson.Default)
               ?? throw new ServerError($"Failed to deserialize {typeof(T).Name}");
    }

    private static Dictionary<string, JsonElement> ElementToDictionary(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new ServerError($"Expected JSON object, got {element.ValueKind}");
        }

        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var prop in element.EnumerateObject())
        {
            result[prop.Name] = prop.Value.Clone();
        }

        return result;
    }

    private static List<JsonElement> GetArrayProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var arr)
            && arr.ValueKind == JsonValueKind.Array)
        {
            return arr.EnumerateArray().Select(e => e.Clone()).ToList();
        }

        return [];
    }
}
