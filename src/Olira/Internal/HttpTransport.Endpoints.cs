#nullable enable

using System.Net.Http.Headers;
using System.Text.Json;

namespace Olira.Internal;

public sealed partial class HttpTransport
{
    // ------------------------------------------------------------------
    // Logs batch
    // ------------------------------------------------------------------

    /// <summary>Send a batch of logs (background worker path). Returns raw response dict.</summary>
    public Dictionary<string, JsonElement> SendBatch(IReadOnlyList<object> logs) =>
        SendBatchAsync(logs).GetAwaiter().GetResult();

    /// <inheritdoc cref="SendBatch"/>
    public async Task<Dictionary<string, JsonElement>> SendBatchAsync(
        IReadOnlyList<object> logs,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
            HttpMethod.Post,
            "/v1/logs/batch",
            json: new Dictionary<string, object?> { ["logs"] = logs },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return ElementToDictionary(raw);
    }

    /// <summary>Send a batch directly (<c>log_batch()</c> path). Returns parsed <see cref="BatchResult"/>.</summary>
    public BatchResult SendBatchDirect(IReadOnlyList<object> logs) =>
        SendBatchDirectAsync(logs).GetAwaiter().GetResult();

    /// <inheritdoc cref="SendBatchDirect"/>
    public async Task<BatchResult> SendBatchDirectAsync(
        IReadOnlyList<object> logs,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
            HttpMethod.Post,
            "/v1/logs/batch",
            json: new Dictionary<string, object?> { ["logs"] = logs },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return DeserializeRequired<BatchResult>(raw);
    }

    // ------------------------------------------------------------------
    // Patients
    // ------------------------------------------------------------------

    /// <summary>Create a patient (POST /v1/patients). Requires api:manage-patients scope.</summary>
    public Patient CreatePatient(object body) =>
        CreatePatientAsync(body).GetAwaiter().GetResult();

    /// <inheritdoc cref="CreatePatient"/>
    public async Task<Patient> CreatePatientAsync(object body, CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(HttpMethod.Post, "/v1/patients", json: body, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<Patient>(raw);
    }

    /// <summary>Get a patient by id (GET /v1/patients/{patient_id}). Requires api:manage-patients scope.</summary>
    public Patient GetPatient(string patientId) =>
        GetPatientAsync(patientId).GetAwaiter().GetResult();

    /// <inheritdoc cref="GetPatient"/>
    public async Task<Patient> GetPatientAsync(string patientId, CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                $"/v1/patients/{Uri.EscapeDataString(patientId)}",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<Patient>(raw);
    }

    /// <summary>List patients (GET /v1/patients). Requires api:manage-patients scope.</summary>
    public PatientListResult ListPatients(IDictionary<string, object?> parameters) =>
        ListPatientsAsync(parameters).GetAwaiter().GetResult();

    /// <inheritdoc cref="ListPatients"/>
    public async Task<PatientListResult> ListPatientsAsync(
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                "/v1/patients",
                parameters: parameters,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<PatientListResult>(raw);
    }

    /// <summary>Update a patient (PUT /v1/patients/{patient_id}). Requires api:manage-patients scope.</summary>
    public Patient UpdatePatient(string patientId, object body) =>
        UpdatePatientAsync(patientId, body).GetAwaiter().GetResult();

    /// <inheritdoc cref="UpdatePatient"/>
    public async Task<Patient> UpdatePatientAsync(
        string patientId,
        object body,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Put,
                $"/v1/patients/{Uri.EscapeDataString(patientId)}",
                json: body,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<Patient>(raw);
    }

    /// <summary>
    /// Delete a patient (DELETE /v1/patients/{patient_id}). Soft-deletes by default.
    /// Pass <paramref name="permanent"/> to hard-delete (irreversible).
    /// </summary>
    public void DeletePatient(string patientId, bool permanent = false) =>
        DeletePatientAsync(patientId, permanent).GetAwaiter().GetResult();

    /// <inheritdoc cref="DeletePatient"/>
    public async Task DeletePatientAsync(
        string patientId,
        bool permanent = false,
        CancellationToken cancellationToken = default)
    {
        IDictionary<string, object?>? parameters = permanent
            ? new Dictionary<string, object?> { ["permanent"] = "true" }
            : null;
        await RequestAsync(
                HttpMethod.Delete,
                $"/v1/patients/{Uri.EscapeDataString(patientId)}",
                parameters: parameters,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Batch-create patients (POST /v1/patients/batch). Requires api:manage-patients scope.</summary>
    public PatientBatchResult CreatePatientsBatch(IReadOnlyList<object> patients) =>
        CreatePatientsBatchAsync(patients).GetAwaiter().GetResult();

    /// <inheritdoc cref="CreatePatientsBatch"/>
    public async Task<PatientBatchResult> CreatePatientsBatchAsync(
        IReadOnlyList<object> patients,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Post,
                "/v1/patients/batch",
                json: new Dictionary<string, object?> { ["patients"] = patients },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<PatientBatchResult>(raw);
    }

    // ------------------------------------------------------------------
    // Cohorts
    // ------------------------------------------------------------------

    /// <summary>Create a cohort (POST /v1/cohorts). Requires api:manage-patients scope.</summary>
    public Cohort CreateCohort(object body) =>
        CreateCohortAsync(body).GetAwaiter().GetResult();

    /// <inheritdoc cref="CreateCohort"/>
    public async Task<Cohort> CreateCohortAsync(object body, CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(HttpMethod.Post, "/v1/cohorts", json: body, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<Cohort>(raw);
    }

    /// <summary>List cohorts (GET /v1/cohorts). Requires api:manage-patients scope.</summary>
    public CohortListResult ListCohorts() =>
        ListCohortsAsync().GetAwaiter().GetResult();

    /// <inheritdoc cref="ListCohorts"/>
    public async Task<CohortListResult> ListCohortsAsync(CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(HttpMethod.Get, "/v1/cohorts", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<CohortListResult>(raw);
    }

    /// <summary>Get a cohort by id (GET /v1/cohorts/{cohort_id}). Requires api:manage-patients scope.</summary>
    public Cohort GetCohort(string cohortId) =>
        GetCohortAsync(cohortId).GetAwaiter().GetResult();

    /// <inheritdoc cref="GetCohort"/>
    public async Task<Cohort> GetCohortAsync(string cohortId, CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                $"/v1/cohorts/{Uri.EscapeDataString(cohortId)}",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<Cohort>(raw);
    }

    /// <summary>Update a cohort (PUT /v1/cohorts/{cohort_id}). Requires api:manage-patients scope.</summary>
    public Cohort UpdateCohort(string cohortId, object body) =>
        UpdateCohortAsync(cohortId, body).GetAwaiter().GetResult();

    /// <inheritdoc cref="UpdateCohort"/>
    public async Task<Cohort> UpdateCohortAsync(
        string cohortId,
        object body,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Put,
                $"/v1/cohorts/{Uri.EscapeDataString(cohortId)}",
                json: body,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<Cohort>(raw);
    }

    /// <summary>Delete a cohort (DELETE /v1/cohorts/{cohort_id}). Requires api:manage-patients scope.</summary>
    public CohortDeleteResult DeleteCohort(string cohortId) =>
        DeleteCohortAsync(cohortId).GetAwaiter().GetResult();

    /// <inheritdoc cref="DeleteCohort"/>
    public async Task<CohortDeleteResult> DeleteCohortAsync(
        string cohortId,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Delete,
                $"/v1/cohorts/{Uri.EscapeDataString(cohortId)}",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<CohortDeleteResult>(raw);
    }

    /// <summary>Add patients to a cohort (POST /v1/cohorts/{cohort_id}/patients).</summary>
    public CohortPatientMutationResult AddPatientsToCohort(string cohortId, object body) =>
        AddPatientsToCohortAsync(cohortId, body).GetAwaiter().GetResult();

    /// <inheritdoc cref="AddPatientsToCohort"/>
    public async Task<CohortPatientMutationResult> AddPatientsToCohortAsync(
        string cohortId,
        object body,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Post,
                $"/v1/cohorts/{Uri.EscapeDataString(cohortId)}/patients",
                json: body,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<CohortPatientMutationResult>(raw);
    }

    /// <summary>Remove patients from a cohort (DELETE /v1/cohorts/{cohort_id}/patients).</summary>
    public CohortPatientMutationResult RemovePatientsFromCohort(string cohortId, object body) =>
        RemovePatientsFromCohortAsync(cohortId, body).GetAwaiter().GetResult();

    /// <inheritdoc cref="RemovePatientsFromCohort"/>
    public async Task<CohortPatientMutationResult> RemovePatientsFromCohortAsync(
        string cohortId,
        object body,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Delete,
                $"/v1/cohorts/{Uri.EscapeDataString(cohortId)}/patients",
                json: body,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<CohortPatientMutationResult>(raw);
    }

    /// <summary>Assign a template to a cohort (POST /v1/cohorts/{cohort_id}/templates).</summary>
    public CohortTemplateAssignment AssignCohortTemplate(string cohortId, object body) =>
        AssignCohortTemplateAsync(cohortId, body).GetAwaiter().GetResult();

    /// <inheritdoc cref="AssignCohortTemplate"/>
    public async Task<CohortTemplateAssignment> AssignCohortTemplateAsync(
        string cohortId,
        object body,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Post,
                $"/v1/cohorts/{Uri.EscapeDataString(cohortId)}/templates",
                json: body,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<CohortTemplateAssignment>(raw);
    }

    /// <summary>Unassign a template from a cohort (DELETE /v1/cohorts/{cohort_id}/templates/{summary_type}).</summary>
    public Dictionary<string, JsonElement> UnassignCohortTemplate(string cohortId, string summaryType) =>
        UnassignCohortTemplateAsync(cohortId, summaryType).GetAwaiter().GetResult();

    /// <inheritdoc cref="UnassignCohortTemplate"/>
    public async Task<Dictionary<string, JsonElement>> UnassignCohortTemplateAsync(
        string cohortId,
        string summaryType,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Delete,
                $"/v1/cohorts/{Uri.EscapeDataString(cohortId)}/templates/{Uri.EscapeDataString(summaryType)}",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return ElementToDictionary(raw);
    }

    /// <summary>List templates assigned to a cohort (GET /v1/cohorts/{cohort_id}/templates).</summary>
    public CohortTemplatesResult ListCohortTemplates(string cohortId) =>
        ListCohortTemplatesAsync(cohortId).GetAwaiter().GetResult();

    /// <inheritdoc cref="ListCohortTemplates"/>
    public async Task<CohortTemplatesResult> ListCohortTemplatesAsync(
        string cohortId,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                $"/v1/cohorts/{Uri.EscapeDataString(cohortId)}/templates",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<CohortTemplatesResult>(raw);
    }

    // ------------------------------------------------------------------
    // Projects
    // ------------------------------------------------------------------

    /// <summary>Create a project (POST /v1/projects). Non-idempotent — not retried.</summary>
    public Project CreateProject(object body) =>
        CreateProjectAsync(body).GetAwaiter().GetResult();

    /// <inheritdoc cref="CreateProject"/>
    public async Task<Project> CreateProjectAsync(object body, CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Post,
                "/v1/projects",
                json: body,
                retryable: false,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<Project>(raw);
    }

    /// <summary>List projects (GET /v1/projects).</summary>
    public ProjectListResult ListProjects() =>
        ListProjectsAsync().GetAwaiter().GetResult();

    /// <inheritdoc cref="ListProjects"/>
    public async Task<ProjectListResult> ListProjectsAsync(CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(HttpMethod.Get, "/v1/projects", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<ProjectListResult>(raw);
    }

    /// <summary>Get a project by id or slug (GET /v1/projects/{id_or_slug}).</summary>
    public Project GetProject(string project) =>
        GetProjectAsync(project).GetAwaiter().GetResult();

    /// <inheritdoc cref="GetProject"/>
    public async Task<Project> GetProjectAsync(string project, CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                $"/v1/projects/{Uri.EscapeDataString(project)}",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<Project>(raw);
    }

    /// <summary>Duplicate a project's config into a new one. Non-idempotent — not retried.</summary>
    public Project DuplicateProject(string project, object body) =>
        DuplicateProjectAsync(project, body).GetAwaiter().GetResult();

    /// <inheritdoc cref="DuplicateProject"/>
    public async Task<Project> DuplicateProjectAsync(
        string project,
        object body,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Post,
                $"/v1/projects/{Uri.EscapeDataString(project)}/duplicate",
                json: body,
                retryable: false,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<Project>(raw);
    }

    /// <summary>Rename/retag a project (PATCH /v1/projects/{id}).</summary>
    public Project UpdateProject(string project, object body) =>
        UpdateProjectAsync(project, body).GetAwaiter().GetResult();

    /// <inheritdoc cref="UpdateProject"/>
    public async Task<Project> UpdateProjectAsync(
        string project,
        object body,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Patch,
                $"/v1/projects/{Uri.EscapeDataString(project)}",
                json: body,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<Project>(raw);
    }

    /// <summary>Soft-delete a project (POST /v1/projects/{id}/deprecate).</summary>
    public Project DeprecateProject(string project) =>
        DeprecateProjectAsync(project).GetAwaiter().GetResult();

    /// <inheritdoc cref="DeprecateProject"/>
    public async Task<Project> DeprecateProjectAsync(string project, CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Post,
                $"/v1/projects/{Uri.EscapeDataString(project)}/deprecate",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<Project>(raw);
    }

    /// <summary>Reactivate a deprecated project (POST /v1/projects/{id}/restore).</summary>
    public Project RestoreProject(string project) =>
        RestoreProjectAsync(project).GetAwaiter().GetResult();

    /// <inheritdoc cref="RestoreProject"/>
    public async Task<Project> RestoreProjectAsync(string project, CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Post,
                $"/v1/projects/{Uri.EscapeDataString(project)}/restore",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<Project>(raw);
    }

    /// <summary>Permanently delete a deprecated project (DELETE /v1/projects/{id}).</summary>
    public void DeleteProject(string project) =>
        DeleteProjectAsync(project).GetAwaiter().GetResult();

    /// <inheritdoc cref="DeleteProject"/>
    public async Task DeleteProjectAsync(string project, CancellationToken cancellationToken = default) =>
        await RequestAsync(
                HttpMethod.Delete,
                $"/v1/projects/{Uri.EscapeDataString(project)}",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

    // ------------------------------------------------------------------
    // Actions (requires sdk:actions scope)
    // ------------------------------------------------------------------

    /// <summary>Create an action destination (POST /v1/actions/destinations). Non-idempotent, not retried.</summary>
    public ActionDestination CreateActionDestination(object body) =>
        CreateActionDestinationAsync(body).GetAwaiter().GetResult();

    /// <inheritdoc cref="CreateActionDestination"/>
    public async Task<ActionDestination> CreateActionDestinationAsync(
        object body,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Post,
                "/v1/actions/destinations",
                json: body,
                retryable: false,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<ActionDestination>(raw);
    }

    /// <summary>List action destinations (GET /v1/actions/destinations).</summary>
    public ActionDestinationListResult ListActionDestinations() =>
        ListActionDestinationsAsync().GetAwaiter().GetResult();

    /// <inheritdoc cref="ListActionDestinations"/>
    public async Task<ActionDestinationListResult> ListActionDestinationsAsync(
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(HttpMethod.Get, "/v1/actions/destinations", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<ActionDestinationListResult>(raw);
    }

    /// <summary>Get one action destination (GET /v1/actions/destinations/{id}).</summary>
    public ActionDestination GetActionDestination(string destinationId) =>
        GetActionDestinationAsync(destinationId).GetAwaiter().GetResult();

    /// <inheritdoc cref="GetActionDestination"/>
    public async Task<ActionDestination> GetActionDestinationAsync(
        string destinationId,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                $"/v1/actions/destinations/{Uri.EscapeDataString(destinationId)}",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<ActionDestination>(raw);
    }

    /// <summary>Update an action destination (PATCH /v1/actions/destinations/{id}).</summary>
    public ActionDestination UpdateActionDestination(string destinationId, object body) =>
        UpdateActionDestinationAsync(destinationId, body).GetAwaiter().GetResult();

    /// <inheritdoc cref="UpdateActionDestination"/>
    public async Task<ActionDestination> UpdateActionDestinationAsync(
        string destinationId,
        object body,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Patch,
                $"/v1/actions/destinations/{Uri.EscapeDataString(destinationId)}",
                json: body,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<ActionDestination>(raw);
    }

    /// <summary>Disable an action destination (DELETE /v1/actions/destinations/{id}).</summary>
    public ActionDestinationDeleteResult DeleteActionDestination(string destinationId) =>
        DeleteActionDestinationAsync(destinationId).GetAwaiter().GetResult();

    /// <inheritdoc cref="DeleteActionDestination"/>
    public async Task<ActionDestinationDeleteResult> DeleteActionDestinationAsync(
        string destinationId,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Delete,
                $"/v1/actions/destinations/{Uri.EscapeDataString(destinationId)}",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<ActionDestinationDeleteResult>(raw);
    }

    /// <summary>
    /// Rotate a destination's signing secret (POST .../rotate-secret). Non-idempotent, not retried.
    /// </summary>
    public ActionDestination RotateActionDestinationSecret(string destinationId) =>
        RotateActionDestinationSecretAsync(destinationId).GetAwaiter().GetResult();

    /// <inheritdoc cref="RotateActionDestinationSecret"/>
    public async Task<ActionDestination> RotateActionDestinationSecretAsync(
        string destinationId,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Post,
                $"/v1/actions/destinations/{Uri.EscapeDataString(destinationId)}/rotate-secret",
                retryable: false,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<ActionDestination>(raw);
    }

    /// <summary>List deliveries, cursor-paginated (GET /v1/actions/deliveries).</summary>
    public ActionDeliveryListResult ListActionDeliveries(IDictionary<string, object?> parameters) =>
        ListActionDeliveriesAsync(parameters).GetAwaiter().GetResult();

    /// <inheritdoc cref="ListActionDeliveries"/>
    public async Task<ActionDeliveryListResult> ListActionDeliveriesAsync(
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                "/v1/actions/deliveries",
                parameters: parameters,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<ActionDeliveryListResult>(raw);
    }

    /// <summary>Get one delivery, including its payload (GET /v1/actions/deliveries/{id}).</summary>
    public ActionDelivery GetActionDelivery(string deliveryId) =>
        GetActionDeliveryAsync(deliveryId).GetAwaiter().GetResult();

    /// <inheritdoc cref="GetActionDelivery"/>
    public async Task<ActionDelivery> GetActionDeliveryAsync(
        string deliveryId,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                $"/v1/actions/deliveries/{Uri.EscapeDataString(deliveryId)}",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<ActionDelivery>(raw);
    }

    /// <summary>
    /// Redeliver the exact original bytes (POST .../redeliver). 409 if the destination is
    /// disabled. Non-idempotent, not retried.
    /// </summary>
    public ActionDelivery RedeliverActionDelivery(string deliveryId) =>
        RedeliverActionDeliveryAsync(deliveryId).GetAwaiter().GetResult();

    /// <inheritdoc cref="RedeliverActionDelivery"/>
    public async Task<ActionDelivery> RedeliverActionDeliveryAsync(
        string deliveryId,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Post,
                $"/v1/actions/deliveries/{Uri.EscapeDataString(deliveryId)}/redeliver",
                retryable: false,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<ActionDelivery>(raw);
    }

    // ------------------------------------------------------------------
    // Schemas
    // ------------------------------------------------------------------

    /// <summary>Register an org schema (POST /v1/schemas). Requires api:org-config scope.</summary>
    public SchemaRegistrationResult RegisterSchema(object body) =>
        RegisterSchemaAsync(body).GetAwaiter().GetResult();

    /// <inheritdoc cref="RegisterSchema"/>
    public async Task<SchemaRegistrationResult> RegisterSchemaAsync(
        object body,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(HttpMethod.Post, "/v1/schemas", json: body, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<SchemaRegistrationResult>(raw);
    }

    /// <summary>List org schemas (GET /v1/schemas). Requires api:org-config scope.</summary>
    public List<SchemaSummary> ListSchemas() =>
        ListSchemasAsync().GetAwaiter().GetResult();

    /// <inheritdoc cref="ListSchemas"/>
    public async Task<List<SchemaSummary>> ListSchemasAsync(CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(HttpMethod.Get, "/v1/schemas", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (raw.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return [];
        }

        return DeserializeRequired<List<SchemaSummary>>(raw);
    }

    /// <summary>Get one org schema's version history (GET /v1/schemas/{subtype}).</summary>
    public SchemaDetail GetSchema(string subtype) =>
        GetSchemaAsync(subtype).GetAwaiter().GetResult();

    /// <inheritdoc cref="GetSchema"/>
    public async Task<SchemaDetail> GetSchemaAsync(string subtype, CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                $"/v1/schemas/{Uri.EscapeDataString(subtype)}",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<SchemaDetail>(raw);
    }

    /// <summary>Dry-run a schema/mapping (POST /v1/schemas/check).</summary>
    public SchemaCheckResult CheckSchema(object body) =>
        CheckSchemaAsync(body).GetAwaiter().GetResult();

    /// <inheritdoc cref="CheckSchema"/>
    public async Task<SchemaCheckResult> CheckSchemaAsync(object body, CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Post,
                "/v1/schemas/check",
                json: body,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<SchemaCheckResult>(raw);
    }

    /// <summary>Propose a schema/mapping change (PATCH /v1/schemas/{subtype}).</summary>
    public SchemaRegistrationResult EditSchema(string subtype, object body) =>
        EditSchemaAsync(subtype, body).GetAwaiter().GetResult();

    /// <inheritdoc cref="EditSchema"/>
    public async Task<SchemaRegistrationResult> EditSchemaAsync(
        string subtype,
        object body,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Patch,
                $"/v1/schemas/{Uri.EscapeDataString(subtype)}",
                json: body,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<SchemaRegistrationResult>(raw);
    }

    /// <summary>Deprecate a version, or withdraw a pending request (DELETE /v1/schemas/{subtype}).</summary>
    public SchemaActionResult DeprecateSchema(string subtype, IDictionary<string, object?> parameters) =>
        DeprecateSchemaAsync(subtype, parameters).GetAwaiter().GetResult();

    /// <inheritdoc cref="DeprecateSchema"/>
    public async Task<SchemaActionResult> DeprecateSchemaAsync(
        string subtype,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Delete,
                $"/v1/schemas/{Uri.EscapeDataString(subtype)}",
                parameters: parameters,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<SchemaActionResult>(raw);
    }

    /// <summary>Activate a materialized version (POST /v1/schemas/{subtype}/versions/{version}/activate).</summary>
    public SchemaActionResult ActivateSchemaVersion(string subtype, int version) =>
        ActivateSchemaVersionAsync(subtype, version).GetAwaiter().GetResult();

    /// <inheritdoc cref="ActivateSchemaVersion"/>
    public async Task<SchemaActionResult> ActivateSchemaVersionAsync(
        string subtype,
        int version,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Post,
                $"/v1/schemas/{Uri.EscapeDataString(subtype)}/versions/{version}/activate",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<SchemaActionResult>(raw);
    }

    // ------------------------------------------------------------------
    // Log types
    // ------------------------------------------------------------------

    /// <summary>List the platform's log-type catalog (GET /v1/log-types). Requires sdk:event-log scope.</summary>
    public LogTypeListResult ListLogTypes() =>
        ListLogTypesAsync().GetAwaiter().GetResult();

    /// <inheritdoc cref="ListLogTypes"/>
    public async Task<LogTypeListResult> ListLogTypesAsync(CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(HttpMethod.Get, "/v1/log-types", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<LogTypeListResult>(raw);
    }

    /// <summary>Get one log type by subtype or alias (GET /v1/log-types/{subtype}). Requires sdk:event-log scope.</summary>
    public LogType GetLogType(string subtype) =>
        GetLogTypeAsync(subtype).GetAwaiter().GetResult();

    /// <inheritdoc cref="GetLogType"/>
    public async Task<LogType> GetLogTypeAsync(string subtype, CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                $"/v1/log-types/{Uri.EscapeDataString(subtype)}",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<LogType>(raw);
    }

    // ------------------------------------------------------------------
    // Confidence scoring config (api:org-config)
    // ------------------------------------------------------------------

    /// <summary>Get org default confidence scoring (GET /v1/confidence-scoring).</summary>
    public ConfidenceScoringResult GetConfidenceScoring() =>
        GetConfidenceScoringAsync().GetAwaiter().GetResult();

    /// <inheritdoc cref="GetConfidenceScoring"/>
    public async Task<ConfidenceScoringResult> GetConfidenceScoringAsync(
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(HttpMethod.Get, "/v1/confidence-scoring", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<ConfidenceScoringResult>(raw);
    }

    /// <summary>Set or clear org default confidence scoring (PUT /v1/confidence-scoring).</summary>
    public ConfidenceScoringResult SetConfidenceScoring(Dictionary<string, object?>? confidenceScoring) =>
        SetConfidenceScoringAsync(confidenceScoring).GetAwaiter().GetResult();

    /// <inheritdoc cref="SetConfidenceScoring"/>
    public async Task<ConfidenceScoringResult> SetConfidenceScoringAsync(
        Dictionary<string, object?>? confidenceScoring,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Put,
                "/v1/confidence-scoring",
                json: new Dictionary<string, object?> { ["confidence_scoring"] = confidenceScoring },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<ConfidenceScoringResult>(raw);
    }

    /// <summary>Get view-level confidence scoring (GET /v1/views/{summaryType}/confidence-scoring).</summary>
    public ConfidenceScoringResult GetViewConfidenceScoring(string summaryType) =>
        GetViewConfidenceScoringAsync(summaryType).GetAwaiter().GetResult();

    /// <inheritdoc cref="GetViewConfidenceScoring"/>
    public async Task<ConfidenceScoringResult> GetViewConfidenceScoringAsync(
        string summaryType,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                $"/v1/views/{Uri.EscapeDataString(summaryType)}/confidence-scoring",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<ConfidenceScoringResult>(raw);
    }

    /// <summary>Set or clear view-level confidence scoring.</summary>
    public ConfidenceScoringResult SetViewConfidenceScoring(
        string summaryType,
        Dictionary<string, object?>? confidenceScoring) =>
        SetViewConfidenceScoringAsync(summaryType, confidenceScoring).GetAwaiter().GetResult();

    /// <inheritdoc cref="SetViewConfidenceScoring"/>
    public async Task<ConfidenceScoringResult> SetViewConfidenceScoringAsync(
        string summaryType,
        Dictionary<string, object?>? confidenceScoring,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Put,
                $"/v1/views/{Uri.EscapeDataString(summaryType)}/confidence-scoring",
                json: new Dictionary<string, object?> { ["confidence_scoring"] = confidenceScoring },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<ConfidenceScoringResult>(raw);
    }

    /// <summary>Get block-level confidence scoring.</summary>
    public ConfidenceScoringResult GetBlockConfidenceScoring(string summaryType, string blockId) =>
        GetBlockConfidenceScoringAsync(summaryType, blockId).GetAwaiter().GetResult();

    /// <inheritdoc cref="GetBlockConfidenceScoring"/>
    public async Task<ConfidenceScoringResult> GetBlockConfidenceScoringAsync(
        string summaryType,
        string blockId,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                $"/v1/views/{Uri.EscapeDataString(summaryType)}/blocks/{Uri.EscapeDataString(blockId)}/confidence-scoring",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<ConfidenceScoringResult>(raw);
    }

    /// <summary>Set or clear block-level confidence scoring.</summary>
    public ConfidenceScoringResult SetBlockConfidenceScoring(
        string summaryType,
        string blockId,
        Dictionary<string, object?>? confidenceScoring) =>
        SetBlockConfidenceScoringAsync(summaryType, blockId, confidenceScoring).GetAwaiter().GetResult();

    /// <inheritdoc cref="SetBlockConfidenceScoring"/>
    public async Task<ConfidenceScoringResult> SetBlockConfidenceScoringAsync(
        string summaryType,
        string blockId,
        Dictionary<string, object?>? confidenceScoring,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Put,
                $"/v1/views/{Uri.EscapeDataString(summaryType)}/blocks/{Uri.EscapeDataString(blockId)}/confidence-scoring",
                json: new Dictionary<string, object?> { ["confidence_scoring"] = confidenceScoring },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<ConfidenceScoringResult>(raw);
    }

    // ------------------------------------------------------------------
    // Auth / patient token
    // ------------------------------------------------------------------

    /// <summary>Mint a patient-scoped JWT (POST /v1/auth/token). Requires sdk:patient-token scope.</summary>
    public PatientToken GetPatientToken(object body) =>
        GetPatientTokenAsync(body).GetAwaiter().GetResult();

    /// <inheritdoc cref="GetPatientToken"/>
    public async Task<PatientToken> GetPatientTokenAsync(object body, CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(HttpMethod.Post, "/v1/auth/token", json: body, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<PatientToken>(raw);
    }

    // ------------------------------------------------------------------
    // Patient state
    // ------------------------------------------------------------------

    /// <summary>GET /v1/state/{patient_id}/stable</summary>
    public StableDataResult GetStableData(string patientId, IDictionary<string, object?> parameters) =>
        GetStableDataAsync(patientId, parameters).GetAwaiter().GetResult();

    /// <inheritdoc cref="GetStableData"/>
    public async Task<StableDataResult> GetStableDataAsync(
        string patientId,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                $"/v1/state/{Uri.EscapeDataString(patientId)}/stable",
                parameters: parameters,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<StableDataResult>(raw);
    }

    /// <summary>GET /v1/state/{patient_id}/event-modules</summary>
    public List<JsonElement> ListEventStateModules(string patientId) =>
        ListEventStateModulesAsync(patientId).GetAwaiter().GetResult();

    /// <inheritdoc cref="ListEventStateModules"/>
    public async Task<List<JsonElement>> ListEventStateModulesAsync(
        string patientId,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                $"/v1/state/{Uri.EscapeDataString(patientId)}/event-modules",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return GetArrayProperty(raw, "modules");
    }

    /// <summary>GET /v1/state/{patient_id}/event-modules/{module_type}</summary>
    public EventStateModuleResult GetEventStateModule(string patientId, string moduleType) =>
        GetEventStateModuleAsync(patientId, moduleType).GetAwaiter().GetResult();

    /// <inheritdoc cref="GetEventStateModule"/>
    public async Task<EventStateModuleResult> GetEventStateModuleAsync(
        string patientId,
        string moduleType,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                $"/v1/state/{Uri.EscapeDataString(patientId)}/event-modules/{Uri.EscapeDataString(moduleType)}",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<EventStateModuleResult>(raw);
    }

    /// <summary>GET /v1/state/{patient_id}/views</summary>
    public List<JsonElement> ListViews(string patientId) =>
        ListViewsAsync(patientId).GetAwaiter().GetResult();

    /// <inheritdoc cref="ListViews"/>
    public async Task<List<JsonElement>> ListViewsAsync(
        string patientId,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                $"/v1/state/{Uri.EscapeDataString(patientId)}/views",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return GetArrayProperty(raw, "views");
    }

    /// <summary>GET /v1/state/{patient_id}/views/{view_type}/blocks</summary>
    public ViewBlocksListResult ListViewBlocks(string patientId, string viewType) =>
        ListViewBlocksAsync(patientId, viewType).GetAwaiter().GetResult();

    /// <inheritdoc cref="ListViewBlocks"/>
    public async Task<ViewBlocksListResult> ListViewBlocksAsync(
        string patientId,
        string viewType,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                $"/v1/state/{Uri.EscapeDataString(patientId)}/views/{Uri.EscapeDataString(viewType)}/blocks",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<ViewBlocksListResult>(raw);
    }

    /// <summary>GET /v1/state/{patient_id}/views/{view_type}</summary>
    public ViewResult GetView(string patientId, string viewType) =>
        GetViewAsync(patientId, viewType).GetAwaiter().GetResult();

    /// <inheritdoc cref="GetView"/>
    public async Task<ViewResult> GetViewAsync(
        string patientId,
        string viewType,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                $"/v1/state/{Uri.EscapeDataString(patientId)}/views/{Uri.EscapeDataString(viewType)}",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<ViewResult>(raw);
    }

    /// <summary>GET /v1/state/{patient_id}/views/{view_type}/blocks/{block_id}</summary>
    public ViewBlockResult GetViewBlock(string patientId, string viewType, string blockId) =>
        GetViewBlockAsync(patientId, viewType, blockId).GetAwaiter().GetResult();

    /// <inheritdoc cref="GetViewBlock"/>
    public async Task<ViewBlockResult> GetViewBlockAsync(
        string patientId,
        string viewType,
        string blockId,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                $"/v1/state/{Uri.EscapeDataString(patientId)}/views/{Uri.EscapeDataString(viewType)}/blocks/{Uri.EscapeDataString(blockId)}",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<ViewBlockResult>(raw);
    }

    /// <summary>GET /v1/state/{patient_id}/views/{view_type}/recent</summary>
    public ViewRecentEventsResult GetViewRecentEvents(
        string patientId,
        string viewType,
        IDictionary<string, object?> parameters) =>
        GetViewRecentEventsAsync(patientId, viewType, parameters).GetAwaiter().GetResult();

    /// <inheritdoc cref="GetViewRecentEvents"/>
    public async Task<ViewRecentEventsResult> GetViewRecentEventsAsync(
        string patientId,
        string viewType,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                $"/v1/state/{Uri.EscapeDataString(patientId)}/views/{Uri.EscapeDataString(viewType)}/recent",
                parameters: parameters,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<ViewRecentEventsResult>(raw);
    }

    /// <summary>GET /v1/state/{patient_id}/logs</summary>
    public LogsResult GetLogs(string patientId, IDictionary<string, object?> parameters) =>
        GetLogsAsync(patientId, parameters).GetAwaiter().GetResult();

    /// <inheritdoc cref="GetLogs"/>
    public async Task<LogsResult> GetLogsAsync(
        string patientId,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                $"/v1/state/{Uri.EscapeDataString(patientId)}/logs",
                parameters: parameters,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<LogsResult>(raw);
    }

    /// <summary>POST /v1/state/{patient_id}/logs/query</summary>
    public LogQueryResult QueryLogs(string patientId, object body) =>
        QueryLogsAsync(patientId, body).GetAwaiter().GetResult();

    /// <inheritdoc cref="QueryLogs"/>
    public async Task<LogQueryResult> QueryLogsAsync(
        string patientId,
        object body,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Post,
                $"/v1/state/{Uri.EscapeDataString(patientId)}/logs/query",
                json: body,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<LogQueryResult>(raw);
    }

    /// <summary>POST /v1/state/logs/query</summary>
    public LogQueryResult QueryPopulationLogs(object body) =>
        QueryPopulationLogsAsync(body).GetAwaiter().GetResult();

    /// <inheritdoc cref="QueryPopulationLogs"/>
    public async Task<LogQueryResult> QueryPopulationLogsAsync(
        object body,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Post,
                "/v1/state/logs/query",
                json: body,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<LogQueryResult>(raw);
    }

    /// <summary>GET /v1/state/{patient_id}/events</summary>
    public EventsResult GetEvents(string patientId, IDictionary<string, object?> parameters) =>
        GetEventsAsync(patientId, parameters).GetAwaiter().GetResult();

    /// <inheritdoc cref="GetEvents"/>
    public async Task<EventsResult> GetEventsAsync(
        string patientId,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                $"/v1/state/{Uri.EscapeDataString(patientId)}/events",
                parameters: parameters,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<EventsResult>(raw);
    }

    /// <summary>GET /v1/state/{patient_id}/memories</summary>
    public MemoriesResult ReadMemories(string patientId, IDictionary<string, object?> parameters) =>
        ReadMemoriesAsync(patientId, parameters).GetAwaiter().GetResult();

    /// <inheritdoc cref="ReadMemories"/>
    public async Task<MemoriesResult> ReadMemoriesAsync(
        string patientId,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                $"/v1/state/{Uri.EscapeDataString(patientId)}/memories",
                parameters: parameters,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<MemoriesResult>(raw);
    }

    // ------------------------------------------------------------------
    // SDK config / ingestion
    // ------------------------------------------------------------------

    /// <summary>Fetch the org's SDK configuration (GET /v1/sdk/config).</summary>
    public Dictionary<string, JsonElement> GetSdkConfig() =>
        GetSdkConfigAsync().GetAwaiter().GetResult();

    /// <inheritdoc cref="GetSdkConfig"/>
    public async Task<Dictionary<string, JsonElement>> GetSdkConfigAsync(
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(HttpMethod.Get, "/v1/sdk/config", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return ElementToDictionary(raw);
    }

    /// <summary>Get a presigned S3 PUT URL for file-based ingestion (POST /v1/ingestion/upload-url).</summary>
    public Dictionary<string, JsonElement> GetUploadUrl() =>
        GetUploadUrlAsync().GetAwaiter().GetResult();

    /// <inheritdoc cref="GetUploadUrl"/>
    public async Task<Dictionary<string, JsonElement>> GetUploadUrlAsync(
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(HttpMethod.Post, "/v1/ingestion/upload-url", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return ElementToDictionary(raw);
    }

    /// <summary>Allocate document-package upload URLs (POST /v1/ingestion/jobs:begin).</summary>
    public Dictionary<string, JsonElement> BeginIngestionJob(object body) =>
        BeginIngestionJobAsync(body).GetAwaiter().GetResult();

    /// <inheritdoc cref="BeginIngestionJob"/>
    public async Task<Dictionary<string, JsonElement>> BeginIngestionJobAsync(
        object body,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Post,
                "/v1/ingestion/jobs:begin",
                json: body,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return ElementToDictionary(raw);
    }

    /// <summary>Create a historical ingestion job (POST /v1/ingestion/jobs).</summary>
    public IngestionJob CreateIngestionJob(object body) =>
        CreateIngestionJobAsync(body).GetAwaiter().GetResult();

    /// <inheritdoc cref="CreateIngestionJob"/>
    public async Task<IngestionJob> CreateIngestionJobAsync(
        object body,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Post,
                "/v1/ingestion/jobs",
                json: body,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<IngestionJob>(raw);
    }

    /// <summary>Poll job status (GET /v1/ingestion/jobs/{job_id}).</summary>
    public IngestionJob GetIngestionJob(string jobId) =>
        GetIngestionJobAsync(jobId).GetAwaiter().GetResult();

    /// <inheritdoc cref="GetIngestionJob"/>
    public async Task<IngestionJob> GetIngestionJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                $"/v1/ingestion/jobs/{Uri.EscapeDataString(jobId)}",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<IngestionJob>(raw);
    }

    /// <summary>List ingestion jobs for the org (GET /v1/ingestion/jobs).</summary>
    public IngestionJobListResult ListIngestionJobs(IDictionary<string, object?> parameters) =>
        ListIngestionJobsAsync(parameters).GetAwaiter().GetResult();

    /// <inheritdoc cref="ListIngestionJobs"/>
    public async Task<IngestionJobListResult> ListIngestionJobsAsync(
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                "/v1/ingestion/jobs",
                parameters: parameters,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<IngestionJobListResult>(raw);
    }

    /// <summary>
    /// Confirm a job in AWAITING_CONFIRMATION to trigger Phase 2
    /// (POST /v1/ingestion/jobs/{job_id}/confirm).
    /// </summary>
    public IngestionJob ConfirmIngestionJob(string jobId, bool initializeMissingTemplates = false) =>
        ConfirmIngestionJobAsync(jobId, initializeMissingTemplates).GetAwaiter().GetResult();

    /// <inheritdoc cref="ConfirmIngestionJob"/>
    public async Task<IngestionJob> ConfirmIngestionJobAsync(
        string jobId,
        bool initializeMissingTemplates = false,
        CancellationToken cancellationToken = default)
    {
        object? body = null;
        if (initializeMissingTemplates)
        {
            body = new Dictionary<string, object?> { ["initialize_missing_templates"] = true };
        }

        var raw = await RequestAsync(
                HttpMethod.Post,
                $"/v1/ingestion/jobs/{Uri.EscapeDataString(jobId)}/confirm",
                json: body,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<IngestionJob>(raw);
    }

    /// <summary>Cancel a job (POST /v1/ingestion/jobs/{job_id}/cancel).</summary>
    public IngestionJob CancelIngestionJob(string jobId) =>
        CancelIngestionJobAsync(jobId).GetAwaiter().GetResult();

    /// <inheritdoc cref="CancelIngestionJob"/>
    public async Task<IngestionJob> CancelIngestionJobAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Post,
                $"/v1/ingestion/jobs/{Uri.EscapeDataString(jobId)}/cancel",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<IngestionJob>(raw);
    }

    /// <summary>
    /// Remove a patient during AWAITING_CONFIRMATION
    /// (DELETE /v1/ingestion/jobs/{job_id}/patients/{patient_id}).
    /// </summary>
    public void DeleteIngestionJobPatient(string jobId, string patientId) =>
        DeleteIngestionJobPatientAsync(jobId, patientId).GetAwaiter().GetResult();

    /// <inheritdoc cref="DeleteIngestionJobPatient"/>
    public async Task DeleteIngestionJobPatientAsync(
        string jobId,
        string patientId,
        CancellationToken cancellationToken = default) =>
        await RequestAsync(
                HttpMethod.Delete,
                $"/v1/ingestion/jobs/{Uri.EscapeDataString(jobId)}/patients/{Uri.EscapeDataString(patientId)}",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Update mutable fields while AWAITING_CONFIRMATION (PATCH /v1/ingestion/jobs/{job_id}).</summary>
    public IngestionJob PatchIngestionJob(string jobId, object body) =>
        PatchIngestionJobAsync(jobId, body).GetAwaiter().GetResult();

    /// <inheritdoc cref="PatchIngestionJob"/>
    public async Task<IngestionJob> PatchIngestionJobAsync(
        string jobId,
        object body,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Patch,
                $"/v1/ingestion/jobs/{Uri.EscapeDataString(jobId)}",
                json: body,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<IngestionJob>(raw);
    }

    /// <summary>
    /// Retry a failed ViewBackfillJob on a COMPLETED_WITH_ERRORS job
    /// (POST /v1/ingestion/jobs/{job_id}/retry-backfill).
    /// </summary>
    public IngestionJob RetryViewBackfill(string jobId) =>
        RetryViewBackfillAsync(jobId).GetAwaiter().GetResult();

    /// <inheritdoc cref="RetryViewBackfill"/>
    public async Task<IngestionJob> RetryViewBackfillAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Post,
                $"/v1/ingestion/jobs/{Uri.EscapeDataString(jobId)}/retry-backfill",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<IngestionJob>(raw);
    }

    /// <summary>Submit a single FHIR R4 resource (POST /v1/fhir/resource).</summary>
    public BatchResult LogFhir(string patientId, object resource, string? idempotencyKey = null) =>
        LogFhirAsync(patientId, resource, idempotencyKey).GetAwaiter().GetResult();

    /// <inheritdoc cref="LogFhir"/>
    public async Task<BatchResult> LogFhirAsync(
        string patientId,
        object resource,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["patient_id"] = patientId,
            ["resource"] = resource,
        };
        if (!string.IsNullOrEmpty(idempotencyKey)) body["idempotency_key"] = idempotencyKey;

        var raw = await RequestAsync(
                HttpMethod.Post,
                "/v1/fhir/resource",
                json: body,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<BatchResult>(raw);
    }

    // ------------------------------------------------------------------
    // Passive signal ingestion (requires sdk:event-log scope)
    // ------------------------------------------------------------------

    /// <summary>Sync door: POST /v1/signals:batch with a Parquet body.</summary>
    public Dictionary<string, JsonElement> SendSignalBatch(
        IDictionary<string, object?> parameters,
        byte[] content,
        IDictionary<string, string> headers) =>
        SendSignalBatchAsync(parameters, content, headers).GetAwaiter().GetResult();

    /// <inheritdoc cref="SendSignalBatch"/>
    public async Task<Dictionary<string, JsonElement>> SendSignalBatchAsync(
        IDictionary<string, object?> parameters,
        byte[] content,
        IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Post,
                "/v1/signals:batch",
                parameters: parameters,
                content: content,
                headers: headers,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return ElementToDictionary(raw);
    }

    /// <summary>Bulk door step 1: POST /v1/signals:upload-url.</summary>
    public Dictionary<string, JsonElement> GetSignalUploadUrls(object body) =>
        GetSignalUploadUrlsAsync(body).GetAwaiter().GetResult();

    /// <inheritdoc cref="GetSignalUploadUrls"/>
    public async Task<Dictionary<string, JsonElement>> GetSignalUploadUrlsAsync(
        object body,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Post,
                "/v1/signals:upload-url",
                json: body,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return ElementToDictionary(raw);
    }

    /// <summary>Bulk door step 2: POST /v1/signals:manifest (all-or-nothing commit).</summary>
    public SignalJob CommitSignalManifest(object body) =>
        CommitSignalManifestAsync(body).GetAwaiter().GetResult();

    /// <inheritdoc cref="CommitSignalManifest"/>
    public async Task<SignalJob> CommitSignalManifestAsync(
        object body,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Post,
                "/v1/signals:manifest",
                json: body,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<SignalJob>(raw);
    }

    /// <summary>Poll a signal ingestion job (GET /v1/signals/jobs/{job_id}).</summary>
    public SignalJob GetSignalJob(string jobId) =>
        GetSignalJobAsync(jobId).GetAwaiter().GetResult();

    /// <inheritdoc cref="GetSignalJob"/>
    public async Task<SignalJob> GetSignalJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                $"/v1/signals/jobs/{Uri.EscapeDataString(jobId)}",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<SignalJob>(raw);
    }

    /// <summary>
    /// PUT bytes to a presigned S3 URL (heavy payloads never traverse the API).
    /// Uses a separate client so the API Bearer key is never sent to S3.
    /// </summary>
    public void PutPresigned(string url, byte[] blob, IDictionary<string, string>? headers = null) =>
        PutPresignedAsync(url, blob, headers).GetAwaiter().GetResult();

    /// <inheritdoc cref="PutPresigned"/>
    public async Task PutPresignedAsync(
        string url,
        byte[] blob,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        // Separate client so the API Bearer key is never sent to S3 / CloudFront.
        // Still set a User-Agent — edge WAF returns HTML 403 when the header is absent.
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(300) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"olira-dotnet/{VersionInfo.Version}");
        using var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new ByteArrayContent(blob),
        };

        if (headers is not null)
        {
            foreach (var (key, value) in headers)
            {
                if (key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(value);
                }
                else if (!request.Headers.TryAddWithoutValidation(key, value))
                {
                    request.Content.Headers.TryAddWithoutValidation(key, value);
                }
            }
        }

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var status = (int)response.StatusCode;
        if (status >= 300)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new ServerError(
                $"Presigned upload failed (HTTP {status}): {TruncateBody(body)}",
                statusCode: status);
        }
    }

    // ------------------------------------------------------------------
    // Documents
    // ------------------------------------------------------------------

    /// <summary>POST /v1/documents:upload-url — create DocumentResource + presigned PUT.</summary>
    public Dictionary<string, JsonElement> GetDocumentUploadUrl(object body) =>
        GetDocumentUploadUrlAsync(body).GetAwaiter().GetResult();

    /// <inheritdoc cref="GetDocumentUploadUrl"/>
    public async Task<Dictionary<string, JsonElement>> GetDocumentUploadUrlAsync(
        object body,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Post,
                "/v1/documents:upload-url",
                json: body,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return ElementToDictionary(raw);
    }

    /// <summary>POST /v1/documents/{id}:commit — start OCR after PUT.</summary>
    public Dictionary<string, JsonElement> CommitDocument(string documentId) =>
        CommitDocumentAsync(documentId).GetAwaiter().GetResult();

    /// <inheritdoc cref="CommitDocument"/>
    public async Task<Dictionary<string, JsonElement>> CommitDocumentAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Post,
                $"/v1/documents/{Uri.EscapeDataString(documentId)}:commit",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return ElementToDictionary(raw);
    }

    /// <summary>GET /v1/documents/{id}.</summary>
    public DocumentResource GetDocument(string documentId) =>
        GetDocumentAsync(documentId).GetAwaiter().GetResult();

    /// <inheritdoc cref="GetDocument"/>
    public async Task<DocumentResource> GetDocumentAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                $"/v1/documents/{Uri.EscapeDataString(documentId)}",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<DocumentResource>(raw);
    }

    // ------------------------------------------------------------------
    // Batch exports
    // ------------------------------------------------------------------

    /// <summary>Create a batch export job (POST /v1/exports). Requires sdk:state-read.</summary>
    public ExportJob CreateExport(object body) =>
        CreateExportAsync(body).GetAwaiter().GetResult();

    /// <inheritdoc cref="CreateExport"/>
    public async Task<ExportJob> CreateExportAsync(
        object body,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Post,
                "/v1/exports",
                json: body,
                retryable: false,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<ExportJob>(raw);
    }

    /// <summary>Poll export status (GET /v1/exports/{export_id}).</summary>
    public ExportJob GetExport(string exportId) =>
        GetExportAsync(exportId).GetAwaiter().GetResult();

    /// <inheritdoc cref="GetExport"/>
    public async Task<ExportJob> GetExportAsync(
        string exportId,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                $"/v1/exports/{Uri.EscapeDataString(exportId)}",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<ExportJob>(raw);
    }

    /// <summary>List export jobs (GET /v1/exports).</summary>
    public ExportJobListResult ListExports(IDictionary<string, object?> parameters) =>
        ListExportsAsync(parameters).GetAwaiter().GetResult();

    /// <inheritdoc cref="ListExports"/>
    public async Task<ExportJobListResult> ListExportsAsync(
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                "/v1/exports",
                parameters: parameters,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<ExportJobListResult>(raw);
    }

    /// <summary>Get a presigned download URL (GET /v1/exports/{export_id}/download).</summary>
    public ExportDownload DownloadExport(string exportId) =>
        DownloadExportAsync(exportId).GetAwaiter().GetResult();

    /// <inheritdoc cref="DownloadExport"/>
    public async Task<ExportDownload> DownloadExportAsync(
        string exportId,
        CancellationToken cancellationToken = default)
    {
        var raw = await RequestAsync(
                HttpMethod.Get,
                $"/v1/exports/{Uri.EscapeDataString(exportId)}/download",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return DeserializeRequired<ExportDownload>(raw);
    }
}
