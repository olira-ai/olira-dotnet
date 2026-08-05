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

    /// <summary>List patients in your organisation. Requires api:manage-patients scope.</summary>
    public PatientListResult ListPatients(
        int limit = 100,
        int offset = 0,
        string? externalSystem = null,
        string? externalValue = null)
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

        return _transport.ListPatients(parameters);
    }

    /// <summary>Update a patient. Requires api:manage-patients scope.</summary>
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
