#nullable enable

namespace Olira;

public sealed partial class OliraClient
{
    /// <summary>Create a patient. Requires api:manage-patients scope.</summary>
    public Patient CreatePatient(
        string? firstName = null,
        string? lastName = null,
        string? email = null,
        string? phoneNumber = null,
        string? dateOfBirth = null,
        string sex = "unknown",
        string timezone = "UTC",
        string? primaryDiseaseSite = null,
        string? diseaseStage = null,
        IReadOnlyList<ExternalIdentifier>? externalIdentifiers = null,
        Dictionary<string, object?>? metadata = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var req = new CreatePatientRequest
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PhoneNumber = phoneNumber,
            DateOfBirth = dateOfBirth,
            Sex = sex,
            Timezone = timezone,
            PrimaryDiseaseSite = primaryDiseaseSite,
            DiseaseStage = diseaseStage,
            ExternalIdentifiers = externalIdentifiers?.ToList() ?? [],
            Metadata = metadata,
        };
        return _transport.CreatePatient(req.ToDictionary());
    }

    /// <summary>Batch-create up to 500 patients. Requires api:manage-patients scope.</summary>
    public PatientBatchResult CreatePatientsBatch(IReadOnlyList<CreatePatientRequest> patients)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var wire = patients.Select(p => (object)p.ToDictionary()).ToList();
        return _transport.CreatePatientsBatch(wire);
    }

    /// <summary>Get a patient by id. Requires api:manage-patients scope.</summary>
    public Patient GetPatient(string patientId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetPatient(patientId);
    }

    /// <summary>
    /// List patients in your organisation. Requires api:manage-patients scope.
    /// Filters compose as AND on the same identifier: <paramref name="externalSystem"/> alone finds
    /// every patient with an identifier for that system (e.g. every Epic patient);
    /// <paramref name="externalSystem"/> + <paramref name="externalValue"/> finds the one patient
    /// with that exact identifier; <paramref name="integrationId"/> alone finds every patient linked
    /// to that specific integration instance, regardless of system or value.
    /// <paramref name="externalValue"/> requires <paramref name="externalSystem"/>.
    /// </summary>
    public PatientListResult ListPatients(
        int limit = 100,
        int offset = 0,
        string? externalSystem = null,
        string? externalValue = null,
        string? integrationId = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var parameters = new Dictionary<string, object?>
        {
            ["limit"] = limit,
            ["offset"] = offset,
        };
        if (externalSystem is not null)
        {
            parameters["external_system"] = externalSystem;
        }

        if (externalValue is not null)
        {
            parameters["external_value"] = externalValue;
        }

        if (integrationId is not null)
        {
            parameters["integration_id"] = integrationId;
        }

        return _transport.ListPatients(parameters);
    }

    /// <summary>
    /// Update a patient. Requires api:manage-patients scope.
    /// <paramref name="externalIdentifiers"/> is merge/append-only: it adds any
    /// (system, value) pair not already stored, and never modifies or removes one
    /// that is — including one a platform integration owns. An empty list is
    /// rejected; use <see cref="RemovePatientExternalIdentifiers"/> to remove one.
    /// </summary>
    public Patient UpdatePatient(
        string patientId,
        string? firstName = null,
        string? lastName = null,
        string? email = null,
        string? phoneNumber = null,
        string? dateOfBirth = null,
        string? sex = null,
        string? timezone = null,
        string? primaryDiseaseSite = null,
        string? diseaseStage = null,
        IReadOnlyList<ExternalIdentifier>? externalIdentifiers = null,
        Dictionary<string, object?>? metadata = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateExternalIdentifiersForUpdate(externalIdentifiers);
        var req = new UpdatePatientRequest
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PhoneNumber = phoneNumber,
            DateOfBirth = dateOfBirth,
            Sex = sex,
            Timezone = timezone,
            PrimaryDiseaseSite = primaryDiseaseSite,
            DiseaseStage = diseaseStage,
            ExternalIdentifiers = externalIdentifiers?.ToList(),
            Metadata = metadata,
        };
        return _transport.UpdatePatient(patientId, ToBody(req));
    }

    /// <summary>
    /// Fail fast, client-side, on the same empty-list case the server rejects with 422 —
    /// avoids a round trip for a request that can never succeed.
    /// </summary>
    private static void ValidateExternalIdentifiersForUpdate(IReadOnlyList<ExternalIdentifier>? externalIdentifiers)
    {
        if (externalIdentifiers is { Count: 0 })
        {
            throw new ValidationError(
                "externalIdentifiers cannot be emptied via UpdatePatient — it only adds identifiers. " +
                "Use RemovePatientExternalIdentifiers to remove one.");
        }
    }

    /// <summary>
    /// Add one or more external identifiers to a patient. Requires api:manage-patients scope.
    /// Idempotent — an identifier already present (matched on system + value) is skipped,
    /// not modified. Only System and Value are sent; IntegrationId is platform-owned and
    /// stripped from the request even if set on the objects you pass in.
    /// </summary>
    public ExternalIdentifierMutationResult AddPatientExternalIdentifiers(
        string patientId, IReadOnlyList<ExternalIdentifier> identifiers)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var body = new Dictionary<string, object?>
        {
            ["identifiers"] = identifiers.Select(i => new Dictionary<string, object?>
            {
                ["system"] = i.System,
                ["value"] = i.Value,
            }).ToList(),
        };
        return _transport.AddPatientExternalIdentifiers(patientId, body);
    }

    /// <summary>
    /// Remove one or more external identifiers from a patient. Requires api:manage-patients scope.
    /// The only way to remove an external identifier — <see cref="UpdatePatient"/> never removes.
    /// Each entry is a matcher (see <see cref="ExternalIdentifierMatcher"/>), not just an exact
    /// identifier: <c>System</c> + <c>Value</c> targets one identifier; <c>System</c> alone removes
    /// every identifier for that system (e.g. every Epic identifier across every connected Epic
    /// instance); <c>IntegrationId</c> alone removes every identifier owned by that specific
    /// integration instance. Can match ANY identifier, including one owned by a platform
    /// integration: doing so is a deliberate, irreversible unlink. Under linked_only import mode
    /// the patient immediately stops receiving further data from that integration. Idempotent — a
    /// matcher that matches nothing is skipped, not an error.
    /// <para>
    /// Call <see cref="GetPatient"/> first and check each identifier's <c>IntegrationId</c> to
    /// know the consequence before you remove it: <c>null</c> means you supplied it yourself, so
    /// removal has no side effects beyond dropping that row; non-null means a platform
    /// integration owns it, and removing it unlinks the patient from that integration.
    /// </para>
    /// </summary>
    public ExternalIdentifierMutationResult RemovePatientExternalIdentifiers(
        string patientId, IReadOnlyList<ExternalIdentifierMatcher> identifiers)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var body = new Dictionary<string, object?>
        {
            ["identifiers"] = identifiers.Select(MatcherToDictionary).ToList(),
        };
        return _transport.RemovePatientExternalIdentifiers(patientId, body);
    }

    /// <summary>Builds the wire body for a matcher, omitting unset fields.</summary>
    private static Dictionary<string, object?> MatcherToDictionary(ExternalIdentifierMatcher matcher)
    {
        var dict = new Dictionary<string, object?>();
        if (matcher.System is not null) dict["system"] = matcher.System;
        if (matcher.Value is not null) dict["value"] = matcher.Value;
        if (matcher.IntegrationId is not null) dict["integration_id"] = matcher.IntegrationId;
        return dict;
    }

    /// <summary>Delete a patient. Soft-deletes by default.</summary>
    public void DeletePatient(string patientId, bool permanent = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _transport.DeletePatient(patientId, permanent);
    }

    /// <summary>Mint a short-lived patient-scoped JWT. Requires sdk:patient-token scope.</summary>
    public PatientToken GetPatientToken(string patientId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetPatientToken(new Dictionary<string, object?> { ["patient_id"] = patientId });
    }
}
