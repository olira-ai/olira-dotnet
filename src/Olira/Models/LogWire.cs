using System.Text.RegularExpressions;

namespace Olira;

/// <summary>Maximum UTF-8 size of a serialized log wire entry (512 KB).</summary>
public static class LogLimits
{
    /// <summary>512 KB per event payload.</summary>
    public const int MaxEventPayloadBytes = 512 * 1024;
}

/// <summary>
/// Links a log to an object in your own system (e.g. a conversation or message).
/// <see cref="ObjectId"/> is your identifier — stored and returned as-is.
/// </summary>
public sealed class OliraTrace
{
    /// <summary>Category of the linked object, e.g. "conversation" or "message".</summary>
    public string? ObjectType { get; set; }

    /// <summary>Your identifier for the linked object.</summary>
    public string? ObjectId { get; set; }
}

/// <summary>Lightweight log specification for <c>LogBatch</c>.</summary>
public sealed class LogSpec
{
    /// <summary>Creates a log specification.</summary>
    public LogSpec(
        string logType,
        string patientId,
        Dictionary<string, object?>? payload = null,
        OliraTrace? trace = null,
        string? timestamp = null,
        string? idempotencyKey = null,
        Dictionary<string, object?>? metadata = null,
        bool writeBack = false,
        string? writeBackIntegrationId = null)
    {
        LogType = logType;
        PatientId = patientId;
        Payload = payload;
        Trace = trace;
        Timestamp = timestamp;
        IdempotencyKey = idempotencyKey;
        Metadata = metadata;
        WriteBack = writeBack;
        WriteBackIntegrationId = writeBackIntegrationId;
    }

    /// <summary>Log type string (see <see cref="OliraLogType"/>).</summary>
    public string LogType { get; set; }

    /// <summary>Patient identifier (must not contain PII patterns).</summary>
    public string PatientId { get; set; }

    /// <summary>Event payload.</summary>
    public Dictionary<string, object?>? Payload { get; set; }

    /// <summary>Optional provenance link into your system.</summary>
    public OliraTrace? Trace { get; set; }

    /// <summary>Optional ISO-8601 timestamp.</summary>
    public string? Timestamp { get; set; }

    /// <summary>Optional idempotency key.</summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>Optional metadata.</summary>
    public Dictionary<string, object?>? Metadata { get; set; }

    /// <summary>
    /// Request write-back of this log into the org's connected EHR.
    /// Silently ignored unless the API key carries <c>sdk:integration-write</c>
    /// and the integration passes the platform write gate.
    /// </summary>
    public bool WriteBack { get; set; }

    /// <summary>
    /// Target integration instance for <see cref="WriteBack"/> when the org
    /// holds several write-configured integrations.
    /// </summary>
    public string? WriteBackIntegrationId { get; set; }
}

/// <summary>Per-event error from a batch response.</summary>
public sealed class BatchError
{
    /// <summary>Zero-based index of the failed event in the batch.</summary>
    public int Index { get; set; }

    /// <summary>Machine-readable error code.</summary>
    public string Code { get; set; } = "";

    /// <summary>Human-readable error message.</summary>
    public string Message { get; set; } = "";
}

/// <summary>Result of a <c>LogBatch</c> call. Mirrors <c>/v1/logs/batch</c> response.</summary>
public sealed class BatchResult
{
    /// <summary>Number of accepted events.</summary>
    public int Accepted { get; set; }

    /// <summary>Number of failed events.</summary>
    public int Failed { get; set; }

    /// <summary>Per-event errors for failed items.</summary>
    public List<BatchError> Errors { get; set; } = [];
}

/// <summary>Patient ID PII validation (empty, email, US phone, SSN).</summary>
public static class PatientIdValidation
{
    private static readonly Regex Empty = new(@"^\s*$", RegexOptions.Compiled);
    private static readonly Regex Email = new(@"@", RegexOptions.Compiled);
    private static readonly Regex UsPhone = new(@"^\d{10}$", RegexOptions.Compiled);
    private static readonly Regex Ssn = new(@"^\d{3}-\d{2}-\d{4}$", RegexOptions.Compiled);

    /// <summary>
    /// Validates <paramref name="value"/> and returns it, or throws
    /// <see cref="ValidationError"/> if empty or matching PII patterns.
    /// </summary>
    public static string Validate(string value)
    {
        if (Empty.IsMatch(value))
            throw new ValidationError("patient_id cannot be empty or whitespace");
        if (Email.IsMatch(value))
            throw new ValidationError("patient_id must not contain email addresses; use a pseudonymous identifier");

        var stripped = value.Trim().Replace("-", "").Replace(" ", "");
        if (UsPhone.IsMatch(stripped) && stripped.Length == 10)
            throw new ValidationError("patient_id must not contain US phone numbers; use a pseudonymous identifier");
        if (Ssn.IsMatch(value.Trim()))
            throw new ValidationError("patient_id must not contain SSN; use a pseudonymous identifier");
        return value;
    }
}
