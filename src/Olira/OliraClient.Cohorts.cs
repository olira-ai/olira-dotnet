#nullable enable

using System.Text.Json;

namespace Olira;

public sealed partial class OliraClient
{
    /// <summary>Create a named patient cohort. Requires api:manage-patients scope.</summary>
    public Cohort CreateCohort(string name, string? description = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var body = new Dictionary<string, object?> { ["name"] = name };
        if (description is not null)
        {
            body["description"] = description;
        }

        return _transport.CreateCohort(body);
    }

    /// <summary>List all cohorts in the organisation.</summary>
    public CohortListResult ListCohorts()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.ListCohorts();
    }

    /// <summary>Get a cohort by id, including the full patient id list.</summary>
    public Cohort GetCohort(string cohortId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetCohort(cohortId);
    }

    /// <summary>Update a cohort's name or description.</summary>
    public Cohort UpdateCohort(string cohortId, string? name = null, string? description = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var body = new Dictionary<string, object?>();
        if (name is not null)
        {
            body["name"] = name;
        }

        if (description is not null)
        {
            body["description"] = description;
        }

        return _transport.UpdateCohort(cohortId, body);
    }

    /// <summary>Permanently delete a cohort and all its template assignments.</summary>
    public CohortDeleteResult DeleteCohort(string cohortId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.DeleteCohort(cohortId);
    }

    /// <summary>Add patients to a cohort (max 500 per call).</summary>
    public CohortPatientMutationResult AddPatientsToCohort(string cohortId, IReadOnlyList<string> patientIds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.AddPatientsToCohort(
            cohortId,
            new Dictionary<string, object?> { ["patient_ids"] = patientIds.ToList() });
    }

    /// <summary>Remove patients from a cohort (max 500 per call).</summary>
    public CohortPatientMutationResult RemovePatientsFromCohort(string cohortId, IReadOnlyList<string> patientIds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.RemovePatientsFromCohort(
            cohortId,
            new Dictionary<string, object?> { ["patient_ids"] = patientIds.ToList() });
    }

    /// <summary>Assign a summary type to a cohort.</summary>
    public CohortTemplateAssignment AssignCohortTemplate(string cohortId, string summaryType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.AssignCohortTemplate(
            cohortId,
            new Dictionary<string, object?> { ["summary_type"] = summaryType });
    }

    /// <summary>Remove a summary type assignment from a cohort.</summary>
    public Dictionary<string, JsonElement> UnassignCohortTemplate(string cohortId, string summaryType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.UnassignCohortTemplate(cohortId, summaryType);
    }

    /// <summary>List all template assignments for a cohort.</summary>
    public CohortTemplatesResult ListCohortTemplates(string cohortId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.ListCohortTemplates(cohortId);
    }
}
