using System.Text.Json;
using Olira.Json;

namespace Olira.Internal;

/// <summary>
/// Wire-format log entry built by the SDK for batch transport (512 KB max per event).
/// </summary>
internal sealed class LogWire
{
    public string LogType { get; set; } = "";

    public string PatientId { get; set; } = "";

    public string? Timestamp { get; set; }

    public string LogId { get; set; } = Guid.NewGuid().ToString();

    public string? IdempotencyKey { get; set; }

    public Dictionary<string, object?> Payload { get; set; } = new();

    public Dictionary<string, object?>? Metadata { get; set; }

    public Dictionary<string, string> Context { get; set; } = new();

    public OliraTrace? Trace { get; set; }

    public bool WriteBack { get; set; }

    public string? WriteBackIntegrationId { get; set; }

    /// <summary>Build a validated wire entry from a <see cref="LogSpec"/>.</summary>
    public static LogWire FromSpec(LogSpec spec, IReadOnlyDictionary<string, string>? context = null)
    {
        var wire = new LogWire
        {
            LogType = spec.LogType,
            PatientId = spec.PatientId,
            Payload = spec.Payload ?? new Dictionary<string, object?>(),
            Metadata = spec.Metadata,
            Context = context is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(context),
            Trace = spec.Trace,
            Timestamp = spec.Timestamp,
            WriteBack = spec.WriteBack,
            WriteBackIntegrationId = spec.WriteBackIntegrationId,
            IdempotencyKey = spec.IdempotencyKey,
        };
        wire.Validate();
        return wire;
    }

    /// <summary>
    /// Validates patient_id PII rules, trace completeness, and payload size.
    /// </summary>
    public void Validate()
    {
        PatientId = PatientIdValidation.Validate(PatientId);

        if (Trace is not null)
        {
            if (string.IsNullOrEmpty(Trace.ObjectType) || string.IsNullOrEmpty(Trace.ObjectId))
                throw new ValidationError("trace requires both object_type and object_id");
        }

        // Size check includes null optional fields — parity with Python model_dump_json().
        var json = JsonSerializer.Serialize(this, OliraJson.IncludeNulls);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > LogLimits.MaxEventPayloadBytes)
        {
            throw new ValidationError(
                $"Event payload exceeds {LogLimits.MaxEventPayloadBytes / 1024} KB limit; " +
                "truncate or chunk the payload before sending");
        }
    }
}
