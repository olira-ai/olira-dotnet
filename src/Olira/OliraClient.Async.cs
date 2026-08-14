#nullable enable

using System.Text.Json;
using Olira.Internal;
using Olira.Json;

namespace Olira;

public sealed partial class OliraClient
{
    /// <summary>
    /// Async enqueue (same as <see cref="Log"/> — queue put is synchronous).
    /// </summary>
    /// <remarks>
    /// Unlike Python's <c>AsyncOliraClient</c> (no <c>on_error</c>, silent drop when the
    /// queue is full), .NET <c>*Async</c> methods share the sync <see cref="OliraClient"/>
    /// background worker: <c>onError</c> applies, and a full queue notifies that handler.
    /// Document/signal APIs are available asynchronously here; Python's async client omits them.
    /// </remarks>
    public Task LogAsync(
        string logType,
        string patientId,
        Dictionary<string, object?>? payload = null,
        OliraTrace? trace = null,
        string? timestamp = null,
        Dictionary<string, object?>? metadata = null,
        bool writeBack = false,
        string? writeBackIntegrationId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Log(logType, patientId, payload, trace, timestamp, metadata, writeBack, writeBackIntegrationId);
        return Task.CompletedTask;
    }

    /// <summary>Async FHIR ingest.</summary>
    public async Task<BatchResult> LogFhirAsync(
        string patientId,
        object resource,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var result = await _transport.LogFhirAsync(patientId, resource, idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (result.Accepted == 0)
        {
            var msg = result.Errors.Count > 0
                ? result.Errors[0].Message
                : "FHIR resource produced no accepted events";
            throw new ValidationError(msg);
        }

        return result;
    }

    /// <summary>
    /// Backward-compatible overload for callers still passing a <see cref="CancellationToken"/>
    /// positionally as the third argument (pre-idempotencyKey call shape). Prefer the
    /// <c>idempotencyKey</c>-accepting overload for new code.
    /// </summary>
    public Task<BatchResult> LogFhirAsync(string patientId, object resource, CancellationToken cancellationToken) =>
        LogFhirAsync(patientId, resource, idempotencyKey: null, cancellationToken: cancellationToken);

    /// <summary>Async direct batch send.</summary>
    public async Task<BatchResult> LogBatchAsync(
        IReadOnlyList<LogSpec> events,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (events.Count == 0)
        {
            return new BatchResult { Accepted = 0, Failed = 0 };
        }

        var wireEvents = new List<object>(events.Count);
        foreach (var spec in events)
        {
            wireEvents.Add(ToWireObject(LogWire.FromSpec(spec, _context)));
        }

        return await _transport.SendBatchDirectAsync(wireEvents, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Async flush of the background queue.</summary>
    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Flush();
        return Task.CompletedTask;
    }

    /// <summary>Async create patient.</summary>
    public Task<Patient> CreatePatientAsync(
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
        Dictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default)
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
        return _transport.CreatePatientAsync(req.ToDictionary(), cancellationToken);
    }

    /// <summary>Async batch create patients.</summary>
    public Task<PatientBatchResult> CreatePatientsBatchAsync(
        IReadOnlyList<CreatePatientRequest> patients,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var wire = patients.Select(p => (object)p.ToDictionary()).ToList();
        return _transport.CreatePatientsBatchAsync(wire, cancellationToken);
    }

    /// <summary>Async get patient.</summary>
    public Task<Patient> GetPatientAsync(string patientId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetPatientAsync(patientId, cancellationToken);
    }

    /// <summary>Async list patients.</summary>
    public Task<PatientListResult> ListPatientsAsync(
        int limit = 100,
        int offset = 0,
        string? externalSystem = null,
        string? externalValue = null,
        CancellationToken cancellationToken = default)
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

        return _transport.ListPatientsAsync(parameters, cancellationToken);
    }

    /// <summary>Async update patient.</summary>
    public Task<Patient> UpdatePatientAsync(
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
        Dictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default)
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
        return _transport.UpdatePatientAsync(patientId, ToBody(req), cancellationToken);
    }

    /// <summary>Async delete patient.</summary>
    public Task DeletePatientAsync(
        string patientId,
        bool permanent = false,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.DeletePatientAsync(patientId, permanent, cancellationToken);
    }

    /// <summary>Async patient token.</summary>
    public Task<PatientToken> GetPatientTokenAsync(
        string patientId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetPatientTokenAsync(
            new Dictionary<string, object?> { ["patient_id"] = patientId },
            cancellationToken);
    }

    /// <summary>Async create project.</summary>
    public Task<Project> CreateProjectAsync(
        string name,
        string? slug = null,
        string? description = null,
        string? environment = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var body = new Dictionary<string, object?> { ["name"] = name };
        if (slug is not null) body["slug"] = slug;
        if (description is not null) body["description"] = description;
        if (environment is not null) body["environment"] = environment;
        return _transport.CreateProjectAsync(body, cancellationToken);
    }

    /// <summary>Async list projects.</summary>
    public Task<ProjectListResult> ListProjectsAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.ListProjectsAsync(cancellationToken);
    }

    /// <summary>Async get project.</summary>
    public Task<Project> GetProjectAsync(string project, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetProjectAsync(project, cancellationToken);
    }

    /// <summary>Async duplicate project.</summary>
    public Task<Project> DuplicateProjectAsync(
        string project,
        string name,
        string? slug = null,
        string? description = null,
        string? environment = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var body = new Dictionary<string, object?> { ["name"] = name };
        if (slug is not null) body["slug"] = slug;
        if (description is not null) body["description"] = description;
        if (environment is not null) body["environment"] = environment;
        return _transport.DuplicateProjectAsync(project, body, cancellationToken);
    }

    /// <summary>Async rename project.</summary>
    public Task<Project> RenameProjectAsync(
        string project,
        string? name = null,
        string? description = null,
        string? environment = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var body = new Dictionary<string, object?>();
        if (name is not null) body["name"] = name;
        if (description is not null) body["description"] = description;
        if (environment is not null) body["environment"] = environment;
        return _transport.UpdateProjectAsync(project, body, cancellationToken);
    }

    /// <summary>Async deprecate project.</summary>
    public Task<Project> DeprecateProjectAsync(string project, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.DeprecateProjectAsync(project, cancellationToken);
    }

    /// <summary>Async restore project.</summary>
    public Task<Project> RestoreProjectAsync(string project, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.RestoreProjectAsync(project, cancellationToken);
    }

    /// <summary>Async delete project.</summary>
    public Task DeleteProjectAsync(string project, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.DeleteProjectAsync(project, cancellationToken);
    }

    /// <inheritdoc cref="CreateActionDestination"/>
    public Task<ActionDestination> CreateActionDestinationAsync(
        WebhookDestinationConfig? webhookConfig = null,
        EmailDestinationConfig? emailConfig = null,
        IReadOnlyList<string>? subscribedTriggers = null,
        string? description = null,
        IReadOnlyDictionary<string, string>? staticHeaders = null,
        int? rateLimitPerMinute = null,
        DigestSchedule? digestSchedule = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var body = BuildCreateActionDestinationBody(
            webhookConfig, emailConfig, subscribedTriggers, description, staticHeaders, rateLimitPerMinute,
            digestSchedule);
        return _transport.CreateActionDestinationAsync(body, cancellationToken);
    }

    /// <inheritdoc cref="ListActionDestinations"/>
    public Task<ActionDestinationListResult> ListActionDestinationsAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.ListActionDestinationsAsync(cancellationToken);
    }

    /// <inheritdoc cref="GetActionDestination"/>
    public Task<ActionDestination> GetActionDestinationAsync(
        string destinationId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetActionDestinationAsync(destinationId, cancellationToken);
    }

    /// <inheritdoc cref="UpdateActionDestination"/>
    public Task<ActionDestination> UpdateActionDestinationAsync(
        string destinationId,
        string? url = null,
        string? toEmail = null,
        string? subject = null,
        string? description = null,
        IReadOnlyList<string>? subscribedTriggers = null,
        string? status = null,
        IReadOnlyDictionary<string, string>? staticHeaders = null,
        DigestSchedule? digestSchedule = null,
        bool clearDigestSchedule = false,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var body = BuildUpdateActionDestinationBody(
            url, toEmail, subject, description, subscribedTriggers, status, staticHeaders, digestSchedule,
            clearDigestSchedule);
        return _transport.UpdateActionDestinationAsync(destinationId, body, cancellationToken);
    }

    /// <inheritdoc cref="DeleteActionDestination"/>
    public Task<ActionDestinationDeleteResult> DeleteActionDestinationAsync(
        string destinationId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.DeleteActionDestinationAsync(destinationId, cancellationToken);
    }

    /// <inheritdoc cref="RotateActionDestinationSecret"/>
    public Task<ActionDestination> RotateActionDestinationSecretAsync(
        string destinationId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.RotateActionDestinationSecretAsync(destinationId, cancellationToken);
    }

    /// <inheritdoc cref="ListActionDeliveries"/>
    public Task<ActionDeliveryListResult> ListActionDeliveriesAsync(
        string? destinationId = null,
        string? status = null,
        string? trigger = null,
        string? cursor = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var parameters = BuildListActionDeliveriesParams(destinationId, status, trigger, cursor, limit);
        return _transport.ListActionDeliveriesAsync(parameters, cancellationToken);
    }

    /// <inheritdoc cref="GetActionDelivery"/>
    public Task<ActionDelivery> GetActionDeliveryAsync(string deliveryId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetActionDeliveryAsync(deliveryId, cancellationToken);
    }

    /// <inheritdoc cref="RedeliverActionDelivery"/>
    public Task<ActionDelivery> RedeliverActionDeliveryAsync(
        string deliveryId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.RedeliverActionDeliveryAsync(deliveryId, cancellationToken);
    }

    /// <summary>Async create cohort.</summary>
    public Task<Cohort> CreateCohortAsync(
        string name,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var body = new Dictionary<string, object?> { ["name"] = name };
        if (description is not null) body["description"] = description;
        return _transport.CreateCohortAsync(body, cancellationToken);
    }

    /// <summary>Async list cohorts.</summary>
    public Task<CohortListResult> ListCohortsAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.ListCohortsAsync(cancellationToken);
    }

    /// <summary>Async get cohort.</summary>
    public Task<Cohort> GetCohortAsync(string cohortId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetCohortAsync(cohortId, cancellationToken);
    }

    /// <summary>Async update cohort.</summary>
    public Task<Cohort> UpdateCohortAsync(
        string cohortId,
        string? name = null,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var body = new Dictionary<string, object?>();
        if (name is not null) body["name"] = name;
        if (description is not null) body["description"] = description;
        return _transport.UpdateCohortAsync(cohortId, body, cancellationToken);
    }

    /// <summary>Async delete cohort.</summary>
    public Task<CohortDeleteResult> DeleteCohortAsync(
        string cohortId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.DeleteCohortAsync(cohortId, cancellationToken);
    }

    /// <summary>Async add patients to cohort.</summary>
    public Task<CohortPatientMutationResult> AddPatientsToCohortAsync(
        string cohortId,
        IReadOnlyList<string> patientIds,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.AddPatientsToCohortAsync(
            cohortId,
            new Dictionary<string, object?> { ["patient_ids"] = patientIds.ToList() },
            cancellationToken);
    }

    /// <summary>Async remove patients from cohort.</summary>
    public Task<CohortPatientMutationResult> RemovePatientsFromCohortAsync(
        string cohortId,
        IReadOnlyList<string> patientIds,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.RemovePatientsFromCohortAsync(
            cohortId,
            new Dictionary<string, object?> { ["patient_ids"] = patientIds.ToList() },
            cancellationToken);
    }

    /// <summary>Async assign cohort template.</summary>
    public Task<CohortTemplateAssignment> AssignCohortTemplateAsync(
        string cohortId,
        string summaryType,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.AssignCohortTemplateAsync(
            cohortId,
            new Dictionary<string, object?> { ["summary_type"] = summaryType },
            cancellationToken);
    }

    /// <summary>Async unassign cohort template.</summary>
    public Task<Dictionary<string, JsonElement>> UnassignCohortTemplateAsync(
        string cohortId,
        string summaryType,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.UnassignCohortTemplateAsync(cohortId, summaryType, cancellationToken);
    }

    /// <summary>Async list cohort templates.</summary>
    public Task<CohortTemplatesResult> ListCohortTemplatesAsync(
        string cohortId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.ListCohortTemplatesAsync(cohortId, cancellationToken);
    }

    /// <summary>Async register schema.</summary>
    public Task<SchemaRegistrationResult> RegisterSchemaAsync(
        string subtype,
        string description = "",
        IReadOnlyList<Dictionary<string, object?>>? inputExamples = null,
        Dictionary<string, object?>? schema = null,
        Dictionary<string, object?>? mapping = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var body = new Dictionary<string, object?>
        {
            ["subtype"] = subtype,
            ["description"] = description,
        };
        if (inputExamples is not null) body["input_examples"] = inputExamples;
        if (schema is not null) body["payload_schema"] = schema;
        if (mapping is not null) body["mapping"] = mapping;
        return _transport.RegisterSchemaAsync(body, cancellationToken);
    }

    /// <summary>Async list schemas.</summary>
    public Task<List<SchemaSummary>> ListSchemasAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.ListSchemasAsync(cancellationToken);
    }

    /// <summary>Async get schema.</summary>
    public Task<SchemaDetail> GetSchemaAsync(string subtype, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetSchemaAsync(subtype, cancellationToken);
    }

    /// <summary>Async check schema.</summary>
    public Task<SchemaCheckResult> CheckSchemaAsync(
        IReadOnlyList<Dictionary<string, object?>> examples,
        string? subtype = null,
        int? version = null,
        Dictionary<string, object?>? schema = null,
        Dictionary<string, object?>? mapping = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var body = new Dictionary<string, object?> { ["examples"] = examples };
        if (subtype is not null) body["subtype"] = subtype;
        if (version is not null) body["version"] = version;
        if (schema is not null) body["payload_schema"] = schema;
        if (mapping is not null) body["mapping"] = mapping;
        return _transport.CheckSchemaAsync(body, cancellationToken);
    }

    /// <summary>Async edit schema.</summary>
    public Task<SchemaRegistrationResult> EditSchemaAsync(
        string subtype,
        string? description = null,
        IReadOnlyList<Dictionary<string, object?>>? inputExamples = null,
        Dictionary<string, object?>? schema = null,
        Dictionary<string, object?>? mapping = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var body = new Dictionary<string, object?>();
        if (description is not null) body["description"] = description;
        if (inputExamples is not null) body["input_examples"] = inputExamples;
        if (schema is not null) body["payload_schema"] = schema;
        if (mapping is not null) body["mapping"] = mapping;
        return _transport.EditSchemaAsync(subtype, body, cancellationToken);
    }

    /// <summary>Async deprecate schema.</summary>
    public Task<SchemaActionResult> DeprecateSchemaAsync(
        string subtype,
        int? version = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var parameters = new Dictionary<string, object?>();
        if (version is not null) parameters["version"] = version;
        return _transport.DeprecateSchemaAsync(subtype, parameters, cancellationToken);
    }

    /// <summary>Async activate schema version.</summary>
    public Task<SchemaActionResult> ActivateSchemaVersionAsync(
        string subtype,
        int version,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.ActivateSchemaVersionAsync(subtype, version, cancellationToken);
    }

    /// <summary>Async list log types.</summary>
    public Task<LogTypeListResult> ListLogTypesAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.ListLogTypesAsync(cancellationToken);
    }

    /// <summary>Async get log type.</summary>
    public Task<LogType> GetLogTypeAsync(string subtype, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetLogTypeAsync(subtype, cancellationToken);
    }

    /// <summary>Async get stable data.</summary>
    public Task<StableDataResult> GetStableDataAsync(
        string patientId,
        IReadOnlyList<string>? modules = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var parameters = new Dictionary<string, object?>();
        if (modules is { Count: > 0 })
        {
            parameters["modules"] = string.Join(",", modules);
        }

        return _transport.GetStableDataAsync(patientId, parameters, cancellationToken);
    }

    /// <summary>Async list event state modules.</summary>
    public async Task<List<EventStateModuleSummary>> ListEventStateModulesAsync(
        string patientId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var raw = await _transport.ListEventStateModulesAsync(patientId, cancellationToken).ConfigureAwait(false);
        return raw.Select(DeserializeRequired<EventStateModuleSummary>).ToList();
    }

    /// <summary>Async get event state module.</summary>
    public Task<EventStateModuleResult> GetEventStateModuleAsync(
        string patientId,
        string moduleType,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetEventStateModuleAsync(patientId, moduleType, cancellationToken);
    }

    /// <summary>Async list views.</summary>
    public async Task<List<ViewMeta>> ListViewsAsync(
        string patientId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var raw = await _transport.ListViewsAsync(patientId, cancellationToken).ConfigureAwait(false);
        return raw.Select(DeserializeRequired<ViewMeta>).ToList();
    }

    /// <summary>Async list view blocks.</summary>
    public Task<ViewBlocksListResult> ListViewBlocksAsync(
        string patientId,
        string viewType,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.ListViewBlocksAsync(patientId, viewType, cancellationToken);
    }

    /// <summary>Async get view.</summary>
    public Task<ViewResult> GetViewAsync(
        string patientId,
        string viewType,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetViewAsync(patientId, viewType, cancellationToken);
    }

    /// <summary>Async get view block.</summary>
    public Task<ViewBlockResult> GetViewBlockAsync(
        string patientId,
        string viewType,
        string blockId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetViewBlockAsync(patientId, viewType, blockId, cancellationToken);
    }

    /// <summary>Async get view recent events.</summary>
    public Task<ViewRecentEventsResult> GetViewRecentEventsAsync(
        string patientId,
        string viewType,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetViewRecentEventsAsync(
            patientId,
            viewType,
            new Dictionary<string, object?> { ["limit"] = limit },
            cancellationToken);
    }

    /// <summary>Async get logs.</summary>
    public Task<LogsResult> GetLogsAsync(
        string patientId,
        string? since = null,
        int limit = 50,
        IReadOnlyList<string>? logTypes = null,
        string? traceType = null,
        string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var parameters = new Dictionary<string, object?> { ["limit"] = limit };
        if (!string.IsNullOrEmpty(since)) parameters["since"] = since;
        if (logTypes is { Count: > 0 }) parameters["event_types"] = string.Join(",", logTypes);
        if (!string.IsNullOrEmpty(traceType)) parameters["trace_type"] = traceType;
        if (!string.IsNullOrEmpty(traceId)) parameters["trace_id"] = traceId;
        return _transport.GetLogsAsync(patientId, parameters, cancellationToken);
    }

    /// <summary>Async get events.</summary>
    public Task<EventsResult> GetEventsAsync(
        string patientId,
        string? since = null,
        string? logType = null,
        string? traceType = null,
        string? traceId = null,
        string status = "complete",
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var parameters = new Dictionary<string, object?>
        {
            ["status"] = status,
            ["limit"] = limit,
        };
        if (!string.IsNullOrEmpty(since)) parameters["since"] = since;
        if (!string.IsNullOrEmpty(logType)) parameters["log_type"] = logType;
        if (!string.IsNullOrEmpty(traceType)) parameters["trace_type"] = traceType;
        if (!string.IsNullOrEmpty(traceId)) parameters["trace_id"] = traceId;
        return _transport.GetEventsAsync(patientId, parameters, cancellationToken);
    }

    /// <summary>Async read memories.</summary>
    public Task<MemoriesResult> ReadMemoriesAsync(
        string patientId,
        string? query = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var parameters = new Dictionary<string, object?> { ["limit"] = limit };
        if (!string.IsNullOrEmpty(query)) parameters["query"] = query;
        return _transport.ReadMemoriesAsync(patientId, parameters, cancellationToken);
    }

    /// <summary>Async create ingestion job (runs sync helper on a worker thread for file I/O).</summary>
    public Task<IngestionJob> CreateIngestionJobAsync(
        string? file = null,
        IReadOnlyList<IngestRecord>? records = null,
        IReadOnlyList<IngestDocument>? documents = null,
        string? idempotencyKey = null,
        bool requireConfirmation = true,
        IReadOnlyList<string>? summaryTypes = null,
        int? maxEventLogs = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(
            () => CreateIngestionJob(
                file, records, documents, idempotencyKey, requireConfirmation, summaryTypes, maxEventLogs),
            cancellationToken);

    /// <summary>Async get ingestion job.</summary>
    public Task<IngestionJob> GetIngestionJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetIngestionJobAsync(jobId, cancellationToken);
    }

    /// <summary>Async list ingestion jobs.</summary>
    public Task<IngestionJobListResult> ListIngestionJobsAsync(
        string? idempotencyKey = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var parameters = new Dictionary<string, object?>
        {
            ["page"] = page,
            ["pageSize"] = pageSize,
        };
        if (!string.IsNullOrEmpty(idempotencyKey)) parameters["idempotency_key"] = idempotencyKey;
        return _transport.ListIngestionJobsAsync(parameters, cancellationToken);
    }

    /// <summary>Async confirm ingestion job with resilient 409 handling.</summary>
    public Task<IngestionJob> ConfirmIngestionJobAsync(
        string jobId,
        bool initializeMissingTemplates = false,
        bool skipBackfill = false,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return IngestionConfirm.ConfirmIngestionJobResilientAsync(
            skipBackfill,
            () => PatchIngestionJobAsync(jobId, skipBackfill: true, cancellationToken: cancellationToken),
            () => GetIngestionJobAsync(jobId, cancellationToken),
            () => _transport.ConfirmIngestionJobAsync(jobId, initializeMissingTemplates, cancellationToken));
    }

    /// <summary>Async cancel ingestion job.</summary>
    public Task<IngestionJob> CancelIngestionJobAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.CancelIngestionJobAsync(jobId, cancellationToken);
    }

    /// <summary>Async delete ingestion job patient.</summary>
    public Task DeleteIngestionJobPatientAsync(
        string jobId,
        string patientId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.DeleteIngestionJobPatientAsync(jobId, patientId, cancellationToken);
    }

    /// <summary>Async patch ingestion job.</summary>
    public Task<IngestionJob> PatchIngestionJobAsync(
        string jobId,
        IReadOnlyList<string>? summaryTypes = null,
        bool? skipBackfill = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var body = new Dictionary<string, object?>();
        if (summaryTypes is not null) body["summary_types"] = summaryTypes.ToList();
        if (skipBackfill is not null) body["skip_backfill"] = skipBackfill;
        return _transport.PatchIngestionJobAsync(jobId, body, cancellationToken);
    }

    /// <summary>Async retry view backfill.</summary>
    public Task<IngestionJob> RetryViewBackfillAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.RetryViewBackfillAsync(jobId, cancellationToken);
    }

    /// <summary>Async upload document.</summary>
    public async Task<DocumentHandle> UploadDocumentAsync(
        string patientId,
        string path,
        DocumentLogType logType,
        DateTimeOffset timestamp,
        string idempotencyKey,
        string? documentType = null,
        string? noteType = null,
        object? source = null,
        string? contentType = null,
        bool wait = false,
        double waitTimeoutSeconds = 600.0,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var handle = await Task.Run(
            () => Documents.UploadDocumentViaTransport(
                _transport,
                patientId,
                path,
                logType,
                timestamp,
                idempotencyKey,
                documentType,
                noteType,
                source,
                contentType),
            cancellationToken).ConfigureAwait(false);
        if (wait)
        {
            await handle.WaitAsync(waitTimeoutSeconds, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return handle;
    }

    /// <summary>Async get document.</summary>
    public Task<DocumentResource> GetDocumentAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetDocumentAsync(documentId, cancellationToken);
    }

    /// <summary>Async send signals.</summary>
    public Task<SignalJobHandle> SendSignalsAsync(
        string patientId,
        SignalSensorType sensorType,
        string sourceDevice,
        IReadOnlyList<Dictionary<string, object?>>? records = null,
        byte[]? parquet = null,
        string? schemaVersion = null,
        double? sampleRateHz = null,
        IReadOnlyDictionary<string, string>? units = null,
        string? timestampUnit = null,
        string? deviceTimezone = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(
            () => SendSignals(
                patientId,
                sensorType,
                sourceDevice,
                records,
                parquet,
                schemaVersion,
                sampleRateHz,
                units,
                timestampUnit,
                deviceTimezone),
            cancellationToken);

    /// <summary>Async get signal job.</summary>
    public Task<SignalJob> GetSignalJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetSignalJobAsync(jobId, cancellationToken);
    }

    /// <summary>Async create batch export.</summary>
    public Task<ExportJob> CreateExportAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        ExportInclude include,
        IReadOnlyList<string>? patientIds = null,
        string? cohortId = null,
        string? scope = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(include);

        var selectors = 0;
        if (patientIds is { Count: > 0 }) selectors++;
        if (!string.IsNullOrEmpty(cohortId)) selectors++;
        if (!string.IsNullOrEmpty(scope)) selectors++;
        if (selectors != 1)
        {
            throw new ValidationError(
                "Provide exactly one of patientIds, cohortId, or scope=\"project\"");
        }

        if (!string.IsNullOrEmpty(scope) &&
            !string.Equals(scope, "project", StringComparison.Ordinal))
        {
            throw new ValidationError("scope must be \"project\" when provided");
        }

        var body = new Dictionary<string, object?>
        {
            ["start"] = start.ToUniversalTime().ToString("o", System.Globalization.CultureInfo.InvariantCulture),
            ["end"] = end.ToUniversalTime().ToString("o", System.Globalization.CultureInfo.InvariantCulture),
            ["include"] = include,
        };
        if (patientIds is { Count: > 0 }) body["patient_ids"] = patientIds.ToList();
        if (!string.IsNullOrEmpty(cohortId)) body["cohort_id"] = cohortId;
        if (!string.IsNullOrEmpty(scope)) body["scope"] = scope;

        return _transport.CreateExportAsync(body, cancellationToken);
    }

    /// <summary>Async get export.</summary>
    public Task<ExportJob> GetExportAsync(string exportId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(exportId))
        {
            throw new ValidationError("exportId is required");
        }

        return _transport.GetExportAsync(exportId, cancellationToken);
    }

    /// <summary>Async list exports.</summary>
    public Task<ExportJobListResult> ListExportsAsync(
        int limit = 50,
        int offset = 0,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var parameters = new Dictionary<string, object?>
        {
            ["limit"] = limit,
            ["offset"] = offset,
        };
        if (!string.IsNullOrEmpty(status)) parameters["status"] = status;
        return _transport.ListExportsAsync(parameters, cancellationToken);
    }

    /// <summary>Async download export (presigned URL).</summary>
    public Task<ExportDownload> DownloadExportAsync(
        string exportId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(exportId))
        {
            throw new ValidationError("exportId is required");
        }

        return _transport.DownloadExportAsync(exportId, cancellationToken);
    }
}
