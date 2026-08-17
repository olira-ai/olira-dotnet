using System.Text.Json;
using Olira.Json;

namespace Olira;

/// <summary>
/// Links a patient to their ID in an external system (e.g. Epic MRN, Flatiron ID, FHIR resource ID).
/// </summary>
public sealed class ExternalIdentifier
{
    /// <summary>System name, e.g. "epic", "flatiron", "fhir".</summary>
    public string System { get; set; } = "";

    /// <summary>Patient ID in that system.</summary>
    public string Value { get; set; } = "";

    /// <summary>
    /// Platform-assigned id of the integration that owns this identifier (e.g. an Epic
    /// sync). Read-only — set by Olira, never by you. Null for identifiers you supply
    /// yourself. <c>UpdatePatient</c> only ever adds identifiers, so it's safe to leave
    /// this as-is when echoing a value from <c>GetPatient</c>/<c>ListPatients</c>; prefer
    /// <c>AddPatientExternalIdentifiers</c>/<c>RemovePatientExternalIdentifiers</c> for
    /// incremental changes.
    /// </summary>
    public string? IntegrationId { get; set; }
}

/// <summary>
/// Selects one or more stored external identifiers to remove from a patient — a filter,
/// not a full identifier. Every field is nullable and rows match if they satisfy every
/// field you set. At least one field is required.
///
/// <list type="bullet">
/// <item><description><c>System</c> + <c>Value</c>: exactly one identifier (the common case).</description></item>
/// <item><description><c>System</c> only: every identifier for that system (e.g. every Epic
/// identifier, across every Epic instance the org has connected). <c>System = "epic"</c>
/// unlinks the patient from every connected Epic instance — use <c>IntegrationId</c> alone
/// to drop one hospital.</description></item>
/// <item><description><c>IntegrationId</c> only: every identifier owned by that specific
/// integration instance, regardless of system or value.</description></item>
/// <item><description><c>System</c> + <c>IntegrationId</c>: that system on that instance only.</description></item>
/// </list>
///
/// <c>Value</c> without <c>System</c> is rejected — a bare value isn't enough
/// to know which system's namespace it belongs to.
/// </summary>
public sealed class ExternalIdentifierMatcher
{
    /// <summary>System name to match, e.g. "epic". Omit to match any system.</summary>
    public string? System { get; set; }

    /// <summary>Value to match. Requires <see cref="System"/> to also be set.</summary>
    public string? Value { get; set; }

    /// <summary>Integration instance id to match. Omit to match any (or no) integration.</summary>
    public string? IntegrationId { get; set; }
}

/// <summary>
/// Request body for creating a patient. Demographics are optional so you can create
/// shell patients. You must send at least one of: external_identifiers, email,
/// phone_number, first_name, last_name, or date_of_birth.
/// </summary>
public sealed class CreatePatientRequest
{
    /// <summary>Given name; omit for shell patients.</summary>
    public string? FirstName { get; set; }

    /// <summary>Family name; omit for shell patients.</summary>
    public string? LastName { get; set; }

    /// <summary>Email address.</summary>
    public string? Email { get; set; }

    /// <summary>Phone number.</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>ISO 8601 datetime string, e.g. "1985-03-22T00:00:00Z".</summary>
    public string? DateOfBirth { get; set; }

    /// <summary>Sex; defaults to "unknown".</summary>
    public string Sex { get; set; } = "unknown";

    /// <summary>IANA timezone, e.g. America/New_York. Defaults to UTC.</summary>
    public string Timezone { get; set; } = "UTC";

    /// <summary>Primary disease site.</summary>
    public string? PrimaryDiseaseSite { get; set; }

    /// <summary>Disease stage.</summary>
    public string? DiseaseStage { get; set; }

    /// <summary>External system identifiers.</summary>
    public List<ExternalIdentifier> ExternalIdentifiers { get; set; } = [];

    /// <summary>Optional metadata.</summary>
    public Dictionary<string, object?>? Metadata { get; set; }

    /// <summary>
    /// Strips name fields and ensures at least one anchor field is present.
    /// Throws <see cref="ValidationError"/> on failure.
    /// </summary>
    public void Validate()
    {
        FirstName = StripName(FirstName);
        LastName = StripName(LastName);

        var hasExt = ExternalIdentifiers.Count > 0;
        var hasEmail = Email is not null;
        var hasPhone = !string.IsNullOrWhiteSpace(PhoneNumber);
        var hasName = FirstName is not null || LastName is not null;
        var hasDob = !string.IsNullOrWhiteSpace(DateOfBirth);

        if (!(hasExt || hasEmail || hasPhone || hasName || hasDob))
        {
            throw new ValidationError(
                "Provide at least one of: external_identifiers, email, phone_number, " +
                "first_name, last_name, or date_of_birth");
        }
    }

    /// <summary>Serialize to a dictionary suitable for ingestion payloads (nulls omitted).</summary>
    public Dictionary<string, object?> ToDictionary()
    {
        Validate();
        var json = JsonSerializer.Serialize(this, OliraJson.Default);
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, OliraJson.Default)
               ?? new Dictionary<string, object?>();
    }

    private static string? StripName(string? value)
    {
        if (value is null)
            return null;
        var s = value.Trim();
        return s.Length == 0 ? null : s;
    }
}

/// <summary>
/// Request body for updating a patient (all fields optional).
/// Only set fields are changed; omitted fields are left as-is.
/// <see cref="ExternalIdentifiers"/> is merge/append-only: any (system, value) pair not
/// already stored is added, and anything already stored — including a platform
/// integration's identifier — is left untouched. An empty list is rejected; use
/// <c>RemovePatientExternalIdentifiers</c> to remove one.
/// </summary>
public sealed class UpdatePatientRequest
{
    /// <summary>Given name.</summary>
    public string? FirstName { get; set; }

    /// <summary>Family name.</summary>
    public string? LastName { get; set; }

    /// <summary>Email address.</summary>
    public string? Email { get; set; }

    /// <summary>Phone number.</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>ISO 8601 datetime string, e.g. "1985-03-22T00:00:00Z".</summary>
    public string? DateOfBirth { get; set; }

    /// <summary>Sex.</summary>
    public string? Sex { get; set; }

    /// <summary>IANA timezone.</summary>
    public string? Timezone { get; set; }

    /// <summary>Primary disease site.</summary>
    public string? PrimaryDiseaseSite { get; set; }

    /// <summary>Disease stage.</summary>
    public string? DiseaseStage { get; set; }

    /// <summary>External system identifiers.</summary>
    public List<ExternalIdentifier>? ExternalIdentifiers { get; set; }

    /// <summary>Optional metadata.</summary>
    public Dictionary<string, object?>? Metadata { get; set; }
}

/// <summary>
/// A patient in your organisation. <see cref="Id"/> is the Olira-assigned identifier.
/// </summary>
public sealed class Patient
{
    /// <summary>Olira-assigned patient identifier.</summary>
    public string Id { get; set; } = "";

    /// <summary>Given name.</summary>
    public string? FirstName { get; set; }

    /// <summary>Family name.</summary>
    public string? LastName { get; set; }

    /// <summary>Sex.</summary>
    public string? Sex { get; set; }

    /// <summary>IANA timezone.</summary>
    public string Timezone { get; set; } = "";

    /// <summary>Patient status.</summary>
    public string Status { get; set; } = "";

    /// <summary>Email address.</summary>
    public string? Email { get; set; }

    /// <summary>Phone number.</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>Date of birth (ISO 8601).</summary>
    public string? DateOfBirth { get; set; }

    /// <summary>Primary disease site.</summary>
    public string? PrimaryDiseaseSite { get; set; }

    /// <summary>Disease stage.</summary>
    public string? DiseaseStage { get; set; }

    /// <summary>Creation timestamp.</summary>
    public string? CreatedAt { get; set; }

    /// <summary>External system identifiers.</summary>
    public List<ExternalIdentifier> ExternalIdentifiers { get; set; } = [];

    /// <summary>Optional metadata.</summary>
    public Dictionary<string, object?>? Metadata { get; set; }
}

/// <summary>Result of a list_patients() call.</summary>
public sealed class PatientListResult
{
    /// <summary>Patients in this page.</summary>
    public List<Patient> Patients { get; set; } = [];

    /// <summary>Total matching patients.</summary>
    public int Total { get; set; }

    /// <summary>Whether more pages are available.</summary>
    public bool HasMore { get; set; }
}

/// <summary>One successfully created patient from a batch create call.</summary>
public sealed class PatientBatchItem
{
    /// <summary>Index in the request batch.</summary>
    public int Index { get; set; }

    /// <summary>Olira-assigned patient id.</summary>
    public string Id { get; set; } = "";

    /// <summary>Creation source, if provided.</summary>
    public string? Source { get; set; }
}

/// <summary>Result of a create_patients_batch() call.</summary>
public sealed class PatientBatchResult
{
    /// <summary>Number of successfully created patients.</summary>
    public int Count { get; set; }

    /// <summary>Successfully created items.</summary>
    public List<PatientBatchItem> Items { get; set; } = [];

    /// <summary>Per-item errors.</summary>
    public List<BatchError> Errors { get; set; } = [];
}

/// <summary>Result of AddPatientExternalIdentifiers/RemovePatientExternalIdentifiers.</summary>
public sealed class ExternalIdentifierMutationResult
{
    /// <summary>Olira-assigned patient id.</summary>
    public string PatientId { get; set; } = "";

    /// <summary>Number of identifiers added.</summary>
    public int Added { get; set; }

    /// <summary>Number of identifiers removed.</summary>
    public int Removed { get; set; }

    /// <summary>Number of identifiers skipped (already present, or not found on remove).</summary>
    public int Skipped { get; set; }

    /// <summary>The patient's full external identifier list after the mutation.</summary>
    public List<ExternalIdentifier> ExternalIdentifiers { get; set; } = [];
}

/// <summary>
/// A short-lived patient-scoped JWT. Pass <see cref="AccessToken"/> as a Bearer
/// token to the Olira MCP Patient State server.
/// </summary>
public sealed class PatientToken
{
    /// <summary>JWT access token.</summary>
    public string AccessToken { get; set; } = "";

    /// <summary>Token type; defaults to "bearer".</summary>
    public string TokenType { get; set; } = "bearer";

    /// <summary>Seconds until expiry.</summary>
    public int ExpiresIn { get; set; }

    /// <summary>Granted scopes.</summary>
    public List<string> Scopes { get; set; } = [];
}
