namespace Olira;

/// <summary>One example's dry-run outcome within a <see cref="SchemaCheckResult"/>.</summary>
public sealed class SchemaCheckExampleResult
{
    /// <summary>Example input object.</summary>
    public Dictionary<string, object?> Input { get; set; } = new();

    /// <summary>Whether mapping succeeded for this example.</summary>
    public bool Ok { get; set; }

    /// <summary>Mapped events produced by the dry-run.</summary>
    public List<Dictionary<string, object?>> MappedEvents { get; set; } = [];

    /// <summary>Per-example errors.</summary>
    public List<string> Errors { get; set; } = [];
}

/// <summary>Result of check_schema().</summary>
public sealed class SchemaCheckResult
{
    /// <summary>Whether the overall check passed.</summary>
    public bool Ok { get; set; }

    /// <summary>Per-example results.</summary>
    public List<SchemaCheckExampleResult> Results { get; set; } = [];

    /// <summary>Top-level error message, if any.</summary>
    public string? Error { get; set; }
}

/// <summary>One version entry within a <see cref="SchemaDetail"/>.</summary>
public sealed class SchemaVersion
{
    /// <summary>Version number.</summary>
    public int Version { get; set; }

    /// <summary>Version status.</summary>
    public string Status { get; set; } = "";

    /// <summary>Source of the version.</summary>
    public string Source { get; set; } = "";

    /// <summary>Payload JSON schema, if present.</summary>
    public Dictionary<string, object?>? PayloadSchema { get; set; }

    /// <summary>Mapping summary, if present.</summary>
    public Dictionary<string, object?>? MappingSummary { get; set; }

    /// <summary>Version description.</summary>
    public string Description { get; set; } = "";

    /// <summary>Creation timestamp.</summary>
    public string? CreatedAt { get; set; }

    /// <summary>Creator identifier.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Submission mode.</summary>
    public string? SubmissionMode { get; set; }

    /// <summary>Self-check result object.</summary>
    public Dictionary<string, object?>? SelfCheck { get; set; }

    /// <summary>Registration identifier.</summary>
    public string? RegistrationId { get; set; }
}

/// <summary>Result of get_schema().</summary>
public sealed class SchemaDetail
{
    /// <summary>Schema subtype key.</summary>
    public string Subtype { get; set; } = "";

    /// <summary>Schema status.</summary>
    public string Status { get; set; } = "";

    /// <summary>Active version number, if any.</summary>
    public int? ActiveVersion { get; set; }

    /// <summary>Version history.</summary>
    public List<SchemaVersion> Versions { get; set; } = [];
}

/// <summary>One entry returned by list_schemas().</summary>
public sealed class SchemaSummary
{
    /// <summary>Schema subtype key.</summary>
    public string Subtype { get; set; } = "";

    /// <summary>Schema status.</summary>
    public string Status { get; set; } = "";

    /// <summary>Active version number, if any.</summary>
    public int? ActiveVersion { get; set; }

    /// <summary>Latest version number.</summary>
    public int LatestVersion { get; set; }

    /// <summary>Schema description.</summary>
    public string Description { get; set; } = "";
}

/// <summary>Result of register_schema() and edit_schema().</summary>
public sealed class SchemaRegistrationResult
{
    /// <summary>Registration identifier.</summary>
    public string RegistrationId { get; set; } = "";

    /// <summary>Schema subtype key.</summary>
    public string Subtype { get; set; } = "";

    /// <summary>Target version number.</summary>
    public int TargetVersion { get; set; }

    /// <summary>Submission mode.</summary>
    public string SubmissionMode { get; set; } = "";

    /// <summary>Registration status.</summary>
    public string Status { get; set; } = "";

    /// <summary>Self-check result object.</summary>
    public Dictionary<string, object?>? SelfCheck { get; set; }
}

/// <summary>Result of deprecate_schema() and activate_schema_version().</summary>
public sealed class SchemaActionResult
{
    /// <summary>Schema subtype key.</summary>
    public string Subtype { get; set; } = "";

    /// <summary>Affected version number.</summary>
    public int Version { get; set; }

    /// <summary>Resulting status.</summary>
    public string Status { get; set; } = "";
}
