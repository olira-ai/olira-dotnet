using System.Text.Json;

namespace Olira;

/// <summary>Lifecycle status values for a batch export job.</summary>
public static class ExportJobStatus
{
    /// <summary>Queued.</summary>
    public const string Queued = "queued";

    /// <summary>Running.</summary>
    public const string Running = "running";

    /// <summary>Completed successfully.</summary>
    public const string Completed = "completed";

    /// <summary>Completed with soft-skipped patients.</summary>
    public const string CompletedWithErrors = "completed_with_errors";

    /// <summary>Failed.</summary>
    public const string Failed = "failed";

    /// <summary>Cancelled.</summary>
    public const string Cancelled = "cancelled";
}

/// <summary>
/// Which content types to include in a batch export zip.
/// Each field may be <c>true</c>/<c>false</c>, omitted (<c>null</c>), or a filter object
/// (e.g. <c>{"event_types": ["symptom_report"]}</c> for logs).
/// </summary>
public sealed class ExportInclude
{
    /// <summary>Include <c>logs.parquet</c> (EventLog rows). Bool or filter dict.</summary>
    public object? Logs { get; set; }

    /// <summary>Include <c>state_modules.parquet</c>. Bool or filter dict.</summary>
    public object? StateModules { get; set; }

    /// <summary>Include <c>view_blocks.parquet</c>. Bool or filter dict.</summary>
    public object? ViewBlocks { get; set; }

    /// <summary>Include <c>events.parquet</c> (state transitions). Bool or filter dict.</summary>
    public object? Events { get; set; }

    /// <summary>Include <c>extracted.parquet</c> (CLEAR results). Bool or filter dict.</summary>
    public object? Extracted { get; set; }
}

/// <summary>Batch export job status (GET/POST /v1/exports).</summary>
public sealed class ExportJob
{
    /// <summary>Export job identifier.</summary>
    public string ExportId { get; set; } = "";

    /// <summary>Lifecycle status (see <see cref="ExportJobStatus"/>).</summary>
    public string Status { get; set; } = "";

    /// <summary>Pipeline stage (queued, planning, exporting, finalizing, …).</summary>
    public string? Stage { get; set; }

    /// <summary>0–100 progress estimate.</summary>
    public double ProgressPct { get; set; }

    /// <summary>Selection mode: patient_ids | cohort | project.</summary>
    public string? Selection { get; set; }

    /// <summary>Cohort id when selection is cohort.</summary>
    public string? CohortId { get; set; }

    /// <summary>Number of patients in the export.</summary>
    public int PatientCount { get; set; }

    /// <summary>Inclusive window start (ISO-8601).</summary>
    public string? Start { get; set; }

    /// <summary>Inclusive window end (ISO-8601).</summary>
    public string? End { get; set; }

    /// <summary>Resolved include flags/filters.</summary>
    public Dictionary<string, JsonElement>? Include { get; set; }

    /// <summary>Error message when failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Patients soft-skipped during export.</summary>
    public List<string> FailedPatientIds { get; set; } = [];

    /// <summary>Row counts per parquet member.</summary>
    public Dictionary<string, int> RowCounts { get; set; } = new();

    /// <summary>Zip byte size when complete.</summary>
    public long? ByteSize { get; set; }

    /// <summary>SHA-256 of the zip when complete.</summary>
    public string? ContentSha256 { get; set; }

    /// <summary>Created at (ISO-8601).</summary>
    public string? CreatedAt { get; set; }

    /// <summary>Started at (ISO-8601).</summary>
    public string? StartedAt { get; set; }

    /// <summary>Updated at (ISO-8601).</summary>
    public string? UpdatedAt { get; set; }

    /// <summary>Completed at (ISO-8601).</summary>
    public string? CompletedAt { get; set; }

    /// <summary>True when a download URL can be requested.</summary>
    public bool Downloadable { get; set; }
}

/// <summary>Result of <c>ListExports</c>.</summary>
public sealed class ExportJobListResult
{
    /// <summary>Export jobs for this page.</summary>
    public List<ExportJob> Data { get; set; } = [];

    /// <summary>Total matching jobs.</summary>
    public int Total { get; set; }

    /// <summary>Page size.</summary>
    public int Limit { get; set; } = 50;

    /// <summary>Page offset.</summary>
    public int Offset { get; set; }
}

/// <summary>Presigned download response for a completed export.</summary>
public sealed class ExportDownload
{
    /// <summary>Export job identifier.</summary>
    public string ExportId { get; set; } = "";

    /// <summary>Presigned HTTPS GET URL for the zip.</summary>
    public string DownloadUrl { get; set; } = "";

    /// <summary>URL TTL in seconds.</summary>
    public int ExpiresInSeconds { get; set; }

    /// <summary>Zip byte size.</summary>
    public long? ByteSize { get; set; }

    /// <summary>SHA-256 of the zip.</summary>
    public string? ContentSha256 { get; set; }

    /// <summary>S3 object key.</summary>
    public string? S3Key { get; set; }
}
