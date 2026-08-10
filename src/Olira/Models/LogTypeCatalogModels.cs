namespace Olira;

/// <summary>
/// One entry in the platform's log-type catalog, returned by ListLogTypes()/GetLogType().
///
/// This is the live counterpart to the static <see cref="OliraLogType"/> constants —
/// it always reflects the current server-side catalog, including each type's full
/// payload JSON Schema, and is not limited to the subtypes known when this SDK
/// version was released.
/// </summary>
public sealed class LogType
{
    /// <summary>Canonical subtype string, e.g. "symptom_report".</summary>
    public string Subtype { get; set; } = "";

    /// <summary>Platform category this subtype belongs to, e.g. "symptom_reports".</summary>
    public string Category { get; set; } = "";

    /// <summary>Deprecated alias subtypes that still resolve to this one.</summary>
    public List<string> Aliases { get; set; } = [];

    /// <summary>Human-readable name.</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>What this type is reserved for, and what not to use it for.</summary>
    public string Description { get; set; } = "";

    /// <summary>Full JSON Schema for this type's payload.</summary>
    public Dictionary<string, object?> PayloadSchema { get; set; } = new();

    /// <summary>Prose summary of the payload shape.</summary>
    public string PayloadDescription { get; set; } = "";

    /// <summary>How this type reaches the platform, e.g. "logged".</summary>
    public List<string> Sources { get; set; } = [];

    /// <summary>Schema version.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Patient-state modules this type's data feeds into.</summary>
    public List<string> TargetModules { get; set; } = [];

    /// <summary>Whether this type is meant to be surfaced to end users.</summary>
    public bool UserFacing { get; set; } = true;
}

/// <summary>Result of ListLogTypes().</summary>
public sealed class LogTypeListResult
{
    /// <summary>Every log type in the platform catalog.</summary>
    public List<LogType> Data { get; set; } = [];
}
