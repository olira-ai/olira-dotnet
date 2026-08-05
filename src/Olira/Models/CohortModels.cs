namespace Olira;

/// <summary>A named patient cohort returned by create/get/update cohort operations.</summary>
public sealed class Cohort
{
    /// <summary>Cohort identifier.</summary>
    public string Id { get; set; } = "";

    /// <summary>Cohort name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Optional description.</summary>
    public string? Description { get; set; }

    /// <summary>Patient identifiers in the cohort.</summary>
    public List<string> PatientIds { get; set; } = [];

    /// <summary>Creation timestamp.</summary>
    public string? CreatedAt { get; set; }

    /// <summary>Last update timestamp.</summary>
    public string? UpdatedAt { get; set; }
}

/// <summary>Summary entry returned by list_cohorts().</summary>
public sealed class CohortListItem
{
    /// <summary>Cohort identifier.</summary>
    public string Id { get; set; } = "";

    /// <summary>Cohort name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Optional description.</summary>
    public string? Description { get; set; }

    /// <summary>Number of patients in the cohort.</summary>
    public int PatientCount { get; set; }

    /// <summary>Number of template assignments.</summary>
    public int TemplateAssignmentCount { get; set; }

    /// <summary>Creation timestamp.</summary>
    public string? CreatedAt { get; set; }

    /// <summary>Last update timestamp.</summary>
    public string? UpdatedAt { get; set; }
}

/// <summary>Result of list_cohorts().</summary>
public sealed class CohortListResult
{
    /// <summary>Cohort summary entries.</summary>
    public List<CohortListItem> Data { get; set; } = [];
}

/// <summary>Result of add/remove patients on a cohort.</summary>
public sealed class CohortPatientMutationResult
{
    /// <summary>Cohort identifier.</summary>
    public string CohortId { get; set; } = "";

    /// <summary>Updated patient count.</summary>
    public int PatientCount { get; set; }
}

/// <summary>One template assignment returned by assign/list cohort template operations.</summary>
public sealed class CohortTemplateAssignment
{
    /// <summary>Assignment identifier.</summary>
    public string Id { get; set; } = "";

    /// <summary>Summary type key.</summary>
    public string SummaryType { get; set; } = "";

    /// <summary>Template identifier.</summary>
    public string TemplateId { get; set; } = "";

    /// <summary>Cohort identifier.</summary>
    public string CohortId { get; set; } = "";

    /// <summary>Assignment timestamp.</summary>
    public string? AssignedAt { get; set; }
}

/// <summary>Result of list_cohort_templates().</summary>
public sealed class CohortTemplatesResult
{
    /// <summary>Template assignments.</summary>
    public List<CohortTemplateAssignment> Data { get; set; } = [];
}

/// <summary>Result of delete_cohort().</summary>
public sealed class CohortDeleteResult
{
    /// <summary>Whether the cohort was deleted.</summary>
    public bool Deleted { get; set; }

    /// <summary>Deleted cohort identifier.</summary>
    public string CohortId { get; set; } = "";
}

/// <summary>An isolated workspace within your organisation.</summary>
public sealed class Project
{
    /// <summary>Project identifier.</summary>
    public string Id { get; set; } = "";

    /// <summary>Project name.</summary>
    public string Name { get; set; } = "";

    /// <summary>URL-safe slug.</summary>
    public string Slug { get; set; } = "";

    /// <summary>Optional description.</summary>
    public string? Description { get; set; }

    /// <summary>Environment label, if any.</summary>
    public string? Environment { get; set; }

    /// <summary>Project status; defaults to "active".</summary>
    public string Status { get; set; } = "active";

    /// <summary>Whether this is the org default project.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Creation timestamp.</summary>
    public string? CreatedAt { get; set; }

    /// <summary>Deprecation timestamp, if any.</summary>
    public string? DeprecatedAt { get; set; }
}

/// <summary>Result of list_projects().</summary>
public sealed class ProjectListResult
{
    /// <summary>Projects.</summary>
    public List<Project> Data { get; set; } = [];
}
