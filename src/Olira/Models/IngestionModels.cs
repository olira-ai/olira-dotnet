namespace Olira;

/// <summary>Lifecycle status values of a HistoricalIngestionJob.</summary>
public static class IngestionJobStatus
{
    /// <summary>Queued.</summary>
    public const string Queued = "queued";

    /// <summary>Validating.</summary>
    public const string Validating = "validating";

    /// <summary>Inserting patients.</summary>
    public const string InsertingPatients = "inserting_patients";

    /// <summary>Inserting logs.</summary>
    public const string InsertingLogs = "inserting_logs";

    /// <summary>Awaiting confirmation.</summary>
    public const string AwaitingConfirmation = "awaiting_confirmation";

    /// <summary>Confirmed.</summary>
    public const string Confirmed = "confirmed";

    /// <summary>Extracting.</summary>
    public const string Extracting = "extracting";

    /// <summary>Replaying.</summary>
    public const string Replaying = "replaying";

    /// <summary>Loading.</summary>
    public const string Loading = "loading";

    /// <summary>Rebasing.</summary>
    public const string Rebasing = "rebasing";

    /// <summary>Embedding.</summary>
    public const string Embedding = "embedding";

    /// <summary>Backfilling.</summary>
    public const string Backfilling = "backfilling";

    /// <summary>Completed.</summary>
    public const string Completed = "completed";

    /// <summary>Completed with errors.</summary>
    public const string CompletedWithErrors = "completed_with_errors";

    /// <summary>Failed.</summary>
    public const string Failed = "failed";

    /// <summary>Cancelled.</summary>
    public const string Cancelled = "cancelled";
}

/// <summary>A single per-row error from an ingestion job (validation or insert failure).</summary>
public sealed class IngestionRowError
{
    /// <summary>1-indexed line number in the JSONL file (0 = non-row error).</summary>
    public int Line { get; set; }

    /// <summary>Machine-readable error code, e.g. "missing_patient".</summary>
    public string Code { get; set; } = "";

    /// <summary>Human-readable description.</summary>
    public string Message { get; set; } = "";
}

/// <summary>Leaf-unit progress for the active ingestion stage (aggregated across patients).</summary>
public sealed class IngestionStageWork
{
    /// <summary>Stage work key.</summary>
    public string Key { get; set; } = "";

    /// <summary>Human-readable label.</summary>
    public string Label { get; set; } = "";

    /// <summary>Unit of work: logs | docs | blocks.</summary>
    public string Unit { get; set; } = "";

    /// <summary>Units completed.</summary>
    public int Done { get; set; }

    /// <summary>Units total.</summary>
    public int Total { get; set; }
}

/// <summary>A historical data ingestion job returned by the API.</summary>
public sealed class IngestionJob
{
    /// <summary>Job identifier.</summary>
    public string JobId { get; set; } = "";

    /// <summary>Lifecycle status (see <see cref="IngestionJobStatus"/>).</summary>
    public string Status { get; set; } = "";

    /// <summary>Current stage name.</summary>
    public string Stage { get; set; } = "";

    /// <summary>Progress percentage (0–100).</summary>
    public double ProgressPct { get; set; }

    /// <summary>Whether confirmation is required before replay.</summary>
    public bool RequireConfirmation { get; set; } = true;

    /// <summary>Summary types requested for the job.</summary>
    public List<string> SummaryTypes { get; set; } = [];

    /// <summary>Total patients in the job.</summary>
    public int PatientsTotal { get; set; }

    /// <summary>Patients processed so far.</summary>
    public int PatientsProcessed { get; set; }

    /// <summary>Total logs in the job.</summary>
    public int LogsTotal { get; set; }

    /// <summary>Logs processed so far.</summary>
    public int LogsProcessed { get; set; }

    /// <summary>Logs that failed.</summary>
    public int LogsFailed { get; set; }

    /// <summary>Total documents in the job.</summary>
    public int DocumentsTotal { get; set; }

    /// <summary>Documents registered.</summary>
    public int DocumentsRegistered { get; set; }

    /// <summary>Documents whose OCR succeeded.</summary>
    public int DocumentsOcrSucceeded { get; set; }

    /// <summary>Documents whose OCR failed.</summary>
    public int DocumentsOcrFailed { get; set; }

    /// <summary>Log counts keyed by event type.</summary>
    public Dictionary<string, int> LogsByEventType { get; set; } = new();

    /// <summary>Per-patient log counts.</summary>
    public Dictionary<string, int> PatientLogCounts { get; set; } = new();

    /// <summary>Per-patient event-type counts.</summary>
    public Dictionary<string, Dictionary<string, int>> PatientEventTypeCounts { get; set; } = new();

    /// <summary>Per-patient replay statuses.</summary>
    public Dictionary<string, string> PatientReplayStatuses { get; set; } = new();

    /// <summary>Aggregated row errors.</summary>
    public List<IngestionRowError> ErrorSummary { get; set; } = [];

    /// <summary>
    /// patient_id → list of summary_type keys missing a view slot.
    /// Present at awaiting_confirmation when affected patients exist.
    /// </summary>
    public Dictionary<string, List<string>> MissingTemplateSlots { get; set; } = new();

    /// <summary>Estimated seconds remaining, if known.</summary>
    public int? EstimatedSecondsRemaining { get; set; }

    /// <summary>Related view backfill job id.</summary>
    public string? ViewBackfillJobId { get; set; }

    /// <summary>Backfill status.</summary>
    public string? BackfillStatus { get; set; }

    /// <summary>Backfill progress percentage.</summary>
    public double? BackfillProgressPct { get; set; }

    /// <summary>Tokens consumed.</summary>
    public int TokensUsed { get; set; }

    /// <summary>Estimated cost in USD.</summary>
    public double CostUsd { get; set; }

    /// <summary>Creation timestamp.</summary>
    public string? CreatedAt { get; set; }

    /// <summary>Start timestamp.</summary>
    public string? StartedAt { get; set; }

    /// <summary>Completion timestamp.</summary>
    public string? CompletedAt { get; set; }

    /// <summary>Raw progress object, when present.</summary>
    public Dictionary<string, object?>? Progress { get; set; }

    /// <summary>Active stage work progress.</summary>
    public IngestionStageWork? StageWork { get; set; }
}

/// <summary>Result of list_ingestion_jobs().</summary>
public sealed class IngestionJobListResult
{
    /// <summary>Total jobs matching the query.</summary>
    public int Total { get; set; }

    /// <summary>Jobs in this page.</summary>
    public List<IngestionJob> Jobs { get; set; } = [];
}

/// <summary>
/// Specification for a single log record in a historical ingestion job.
/// </summary>
public sealed class IngestLogSpec
{
    /// <summary>Creates an ingest log specification.</summary>
    public IngestLogSpec(
        string eventType,
        string patientId,
        string timestamp,
        Dictionary<string, object?>? payload = null,
        string? idempotencyKey = null,
        OliraTrace? trace = null)
    {
        EventType = eventType;
        PatientId = patientId;
        Timestamp = timestamp;
        Payload = payload;
        IdempotencyKey = idempotencyKey;
        Trace = trace;
    }

    /// <summary>Platform event type string (e.g. "symptom_report").</summary>
    public string EventType { get; set; }

    /// <summary>
    /// Olira patient UUID or an external_identifier value present in the same
    /// file or already in the org.
    /// </summary>
    public string PatientId { get; set; }

    /// <summary>ISO-8601 event timestamp.</summary>
    public string Timestamp { get; set; }

    /// <summary>Event payload.</summary>
    public Dictionary<string, object?>? Payload { get; set; }

    /// <summary>Prevents duplicate insertion if the same file is re-submitted.</summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>Optional provenance (same shape as live log).</summary>
    public OliraTrace? Trace { get; set; }
}

/// <summary>
/// A document binary for a historical document-package ingestion job.
/// Uploaded via jobs:begin multi-PUT; OCR runs post-confirm.
/// </summary>
public sealed class IngestDocument
{
    /// <summary>Creates an ingest document specification.</summary>
    public IngestDocument(
        string path,
        string patientId,
        string logType,
        string timestamp,
        string? refId = null,
        string? documentType = null,
        string? noteType = null,
        object? source = null,
        string? idempotencyKey = null,
        string? contentType = null,
        string? filename = null)
    {
        Path = path;
        PatientId = patientId;
        LogType = logType;
        Timestamp = timestamp;
        RefId = refId;
        DocumentType = documentType;
        NoteType = noteType;
        Source = source;
        IdempotencyKey = idempotencyKey;
        ContentType = contentType;
        Filename = filename;
    }

    /// <summary>Local filesystem path.</summary>
    public string Path { get; set; }

    /// <summary>Patient identifier.</summary>
    public string PatientId { get; set; }

    /// <summary>Log type: unstructured_report | clinical_note.</summary>
    public string LogType { get; set; }

    /// <summary>ISO-8601 document timestamp.</summary>
    public string Timestamp { get; set; }

    /// <summary>Optional document reference id.</summary>
    public string? RefId { get; set; }

    /// <summary>Optional document type.</summary>
    public string? DocumentType { get; set; }

    /// <summary>Optional note type.</summary>
    public string? NoteType { get; set; }

    /// <summary>Optional source metadata.</summary>
    public object? Source { get; set; }

    /// <summary>Optional idempotency key.</summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>MIME content type; defaults to application/pdf when omitted.</summary>
    public string? ContentType { get; set; }

    /// <summary>Filename override; defaults to the path basename.</summary>
    public string? Filename { get; set; }
}

/// <summary>
/// A single record in a historical ingestion payload (patient, log, or document).
/// Build via the factory methods rather than constructing directly.
/// </summary>
public sealed class IngestRecord
{
    /// <summary>Record type: "patient", "log", or "document".</summary>
    public string Type { get; set; } = "";

    /// <summary>Record data payload.</summary>
    public Dictionary<string, object?> Data { get; set; } = new();

    /// <summary>Create a patient record from a <see cref="CreatePatientRequest"/>.</summary>
    public static IngestRecord Patient(CreatePatientRequest req)
    {
        return new IngestRecord
        {
            Type = "patient",
            Data = req.ToDictionary(),
        };
    }

    /// <summary>Create a log record from an <see cref="IngestLogSpec"/>.</summary>
    public static IngestRecord Log(IngestLogSpec spec)
    {
        var data = new Dictionary<string, object?>
        {
            ["event_type"] = spec.EventType,
            ["patient_id"] = spec.PatientId,
            ["timestamp"] = spec.Timestamp,
        };

        if (spec.Payload is { Count: > 0 })
            data["payload"] = spec.Payload;
        if (!string.IsNullOrEmpty(spec.IdempotencyKey))
            data["idempotency_key"] = spec.IdempotencyKey;
        if (spec.Trace is not null)
        {
            if (string.IsNullOrEmpty(spec.Trace.ObjectType) || string.IsNullOrEmpty(spec.Trace.ObjectId))
                throw new ValidationError("trace requires both object_type and object_id");
            data["trace"] = new Dictionary<string, object?>
            {
                ["object_type"] = spec.Trace.ObjectType,
                ["object_id"] = spec.Trace.ObjectId,
            };
        }

        return new IngestRecord
        {
            Type = "log",
            Data = data,
        };
    }

    /// <summary>
    /// Create a document manifest row (binary already uploaded under <paramref name="s3Key"/>).
    /// </summary>
    public static IngestRecord Document(IngestDocument spec, string s3Key, string refId)
    {
        var filename = spec.Filename ?? System.IO.Path.GetFileName(spec.Path);
        var contentType = spec.ContentType ?? "application/pdf";

        var data = new Dictionary<string, object?>
        {
            ["ref_id"] = refId,
            ["patient_id"] = spec.PatientId,
            ["filename"] = filename,
            ["content_type"] = contentType,
            ["s3_key"] = s3Key,
            ["log_type"] = spec.LogType,
            ["timestamp"] = spec.Timestamp,
        };

        if (!string.IsNullOrEmpty(spec.DocumentType))
            data["document_type"] = spec.DocumentType;
        if (!string.IsNullOrEmpty(spec.NoteType))
            data["note_type"] = spec.NoteType;
        if (spec.Source is not null)
            data["source"] = spec.Source;
        if (!string.IsNullOrEmpty(spec.IdempotencyKey))
            data["idempotency_key"] = spec.IdempotencyKey;

        return new IngestRecord
        {
            Type = "document",
            Data = data,
        };
    }
}
