#nullable enable

using System.Text.Json;

namespace Olira;

/// <summary>
/// Process-wide singleton client and convenience proxies (Python <c>olira.init()</c> / <c>olira.log()</c>).
/// Prefer constructing <see cref="OliraClient"/> directly for multi-key / DI scenarios.
/// </summary>
public static class OliraModule
{
    private static OliraClient? _client;
    private static readonly object Gate = new();

    /// <summary>
    /// Initialize the SDK. API key via <paramref name="apiKey"/> or <c>OLIRA_API_KEY</c>;
    /// project via <paramref name="project"/> or <c>OLIRA_PROJECT</c>.
    /// </summary>
    public static void Init(
        string? apiKey = null,
        OliraEnv environment = OliraEnv.Production,
        string? serviceName = null,
        string? project = null,
        string baseUrl = OliraClient.DefaultBaseUrl,
        int batchSize = 50,
        double flushInterval = 1.5,
        int maxQueueSize = 10_000,
        double timeout = 5.0,
        int maxRetries = 3,
        object? onError = null,
        bool asyncFlush = true)
    {
        var key = apiKey ?? Environment.GetEnvironmentVariable("OLIRA_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new OliraError("api_key is required; pass it to Init() or set OLIRA_API_KEY");
        }

        lock (Gate)
        {
            _client?.Dispose();
            _client = new OliraClient(
                apiKey: key,
                environment: environment,
                serviceName: serviceName,
                project: project ?? Environment.GetEnvironmentVariable("OLIRA_PROJECT"),
                baseUrl: baseUrl,
                batchSize: batchSize,
                flushInterval: flushInterval,
                maxQueueSize: maxQueueSize,
                timeout: timeout,
                maxRetries: maxRetries,
                onError: onError ?? "drop",
                asyncFlush: asyncFlush);
        }
    }

    /// <summary>The current singleton client, or null if <see cref="Init"/> has not been called.</summary>
    public static OliraClient? Client
    {
        get
        {
            lock (Gate)
            {
                return _client;
            }
        }
    }

    private static OliraClient GetClient()
    {
        lock (Gate)
        {
            return _client ?? throw new OliraError("OliraModule.Init() must be called before logging");
        }
    }

    /// <summary>Block until all queued logs are sent.</summary>
    public static void Flush() => GetClient().Flush();

    /// <summary>Enqueue a log for background delivery.</summary>
    public static void Log(
        string logType,
        string patientId,
        Dictionary<string, object?>? payload = null,
        OliraTrace? trace = null,
        string? timestamp = null,
        Dictionary<string, object?>? metadata = null,
        bool writeBack = false,
        string? writeBackIntegrationId = null) =>
        GetClient().Log(
            logType,
            patientId,
            payload,
            trace,
            timestamp,
            metadata,
            writeBack,
            writeBackIntegrationId);

    /// <summary>Send a batch of logs directly.</summary>
    public static BatchResult LogBatch(IReadOnlyList<LogSpec> events) => GetClient().LogBatch(events);

    /// <summary>Submit a FHIR R4 resource for immediate ingestion.</summary>
    public static BatchResult LogFhir(string patientId, object resource) =>
        GetClient().LogFhir(patientId, resource);

    /// <summary>Create a patient.</summary>
    public static Patient CreatePatient(
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
        Dictionary<string, object?>? metadata = null) =>
        GetClient().CreatePatient(
            firstName,
            lastName,
            email,
            phoneNumber,
            dateOfBirth,
            sex,
            timezone,
            primaryDiseaseSite,
            diseaseStage,
            externalIdentifiers,
            metadata);

    /// <summary>Batch-create patients.</summary>
    public static PatientBatchResult CreatePatientsBatch(IReadOnlyList<CreatePatientRequest> patients) =>
        GetClient().CreatePatientsBatch(patients);

    /// <summary>Get a patient by id.</summary>
    public static Patient GetPatient(string patientId) => GetClient().GetPatient(patientId);

    /// <summary>List patients.</summary>
    public static PatientListResult ListPatients(
        int limit = 100,
        int offset = 0,
        string? externalSystem = null,
        string? externalValue = null) =>
        GetClient().ListPatients(limit, offset, externalSystem, externalValue);

    /// <summary>Update a patient.</summary>
    public static Patient UpdatePatient(
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
        Dictionary<string, object?>? metadata = null) =>
        GetClient().UpdatePatient(
            patientId,
            firstName,
            lastName,
            email,
            phoneNumber,
            dateOfBirth,
            sex,
            timezone,
            primaryDiseaseSite,
            diseaseStage,
            externalIdentifiers,
            metadata);

    /// <summary>Delete a patient.</summary>
    public static void DeletePatient(string patientId, bool permanent = false) =>
        GetClient().DeletePatient(patientId, permanent);

    /// <summary>Mint a patient-scoped JWT.</summary>
    public static PatientToken GetPatientToken(string patientId) => GetClient().GetPatientToken(patientId);

    /// <summary>Get stable patient data.</summary>
    public static StableDataResult GetStableData(string patientId, IReadOnlyList<string>? modules = null) =>
        GetClient().GetStableData(patientId, modules);

    /// <summary>List event state modules.</summary>
    public static List<EventStateModuleSummary> ListEventStateModules(string patientId) =>
        GetClient().ListEventStateModules(patientId);

    /// <summary>Get an event state module.</summary>
    public static EventStateModuleResult GetEventStateModule(string patientId, string moduleType) =>
        GetClient().GetEventStateModule(patientId, moduleType);

    /// <summary>List views.</summary>
    public static List<ViewMeta> ListViews(string patientId) => GetClient().ListViews(patientId);

    /// <summary>List view blocks.</summary>
    public static ViewBlocksListResult ListViewBlocks(string patientId, string viewType) =>
        GetClient().ListViewBlocks(patientId, viewType);

    /// <summary>Get a view snapshot.</summary>
    public static ViewResult GetView(string patientId, string viewType) =>
        GetClient().GetView(patientId, viewType);

    /// <summary>Get a view block.</summary>
    public static ViewBlockResult GetViewBlock(string patientId, string viewType, string blockId) =>
        GetClient().GetViewBlock(patientId, viewType, blockId);

    /// <summary>Get recent TEMP events for a view.</summary>
    public static ViewRecentEventsResult GetViewRecentEvents(
        string patientId,
        string viewType,
        int limit = 50) =>
        GetClient().GetViewRecentEvents(patientId, viewType, limit);

    /// <summary>Get logs for a patient.</summary>
    public static LogsResult GetLogs(
        string patientId,
        string? since = null,
        int limit = 50,
        IReadOnlyList<string>? logTypes = null,
        string? traceType = null,
        string? traceId = null) =>
        GetClient().GetLogs(patientId, since, limit, logTypes, traceType, traceId);

    /// <summary>Get events for a patient.</summary>
    public static EventsResult GetEvents(
        string patientId,
        string? since = null,
        string? logType = null,
        string? traceType = null,
        string? traceId = null,
        string status = "complete",
        int limit = 50) =>
        GetClient().GetEvents(patientId, since, logType, traceType, traceId, status, limit);

    /// <summary>Read memories for a patient.</summary>
    public static MemoriesResult ReadMemories(string patientId, string? query = null, int limit = 100) =>
        GetClient().ReadMemories(patientId, query, limit);

    /// <summary>Create a historical ingestion job.</summary>
    public static IngestionJob CreateIngestionJob(
        string? file = null,
        IReadOnlyList<IngestRecord>? records = null,
        string? idempotencyKey = null,
        bool requireConfirmation = true,
        IReadOnlyList<string>? summaryTypes = null,
        int? maxEventLogs = null) =>
        GetClient().CreateIngestionJob(
            file,
            records,
            documents: null,
            idempotencyKey,
            requireConfirmation,
            summaryTypes,
            maxEventLogs);

    /// <summary>Poll an ingestion job.</summary>
    public static IngestionJob GetIngestionJob(string jobId) => GetClient().GetIngestionJob(jobId);

    /// <summary>List ingestion jobs.</summary>
    public static IngestionJobListResult ListIngestionJobs(
        string? idempotencyKey = null,
        int page = 1,
        int pageSize = 20) =>
        GetClient().ListIngestionJobs(idempotencyKey, page, pageSize);

    /// <summary>Confirm an ingestion job.</summary>
    public static IngestionJob ConfirmIngestionJob(string jobId) =>
        GetClient().ConfirmIngestionJob(jobId);

    /// <summary>Cancel an ingestion job.</summary>
    public static IngestionJob CancelIngestionJob(string jobId) =>
        GetClient().CancelIngestionJob(jobId);

    /// <summary>Delete a patient from an ingestion job during review.</summary>
    public static void DeleteIngestionJobPatient(string jobId, string patientId) =>
        GetClient().DeleteIngestionJobPatient(jobId, patientId);

    /// <summary>Patch mutable ingestion job fields.</summary>
    public static IngestionJob PatchIngestionJob(string jobId, IReadOnlyList<string>? summaryTypes = null) =>
        GetClient().PatchIngestionJob(jobId, summaryTypes);

    /// <summary>Retry view backfill.</summary>
    public static IngestionJob RetryViewBackfill(string jobId) => GetClient().RetryViewBackfill(jobId);

    /// <summary>Build a structured log query for one patient.</summary>
    public static LogQuery Logs(string patientId) => GetClient().Logs(patientId);

    /// <summary>Build a population log query.</summary>
    public static LogQuery PopulationLogs(IReadOnlyList<string>? patientIds = null) =>
        GetClient().PopulationLogs(patientIds);

    /// <summary>Create a project.</summary>
    public static Project CreateProject(
        string name,
        string? slug = null,
        string? description = null,
        string? environment = null) =>
        GetClient().CreateProject(name, slug, description, environment);

    /// <summary>List projects.</summary>
    public static ProjectListResult ListProjects() => GetClient().ListProjects();

    /// <summary>Get a project.</summary>
    public static Project GetProject(string project) => GetClient().GetProject(project);

    /// <summary>Duplicate a project.</summary>
    public static Project DuplicateProject(
        string project,
        string name,
        string? slug = null,
        string? description = null,
        string? environment = null) =>
        GetClient().DuplicateProject(project, name, slug, description, environment);

    /// <summary>Rename a project.</summary>
    public static Project RenameProject(
        string project,
        string? name = null,
        string? description = null,
        string? environment = null) =>
        GetClient().RenameProject(project, name, description, environment);

    /// <summary>Deprecate a project.</summary>
    public static Project DeprecateProject(string project) => GetClient().DeprecateProject(project);

    /// <summary>Restore a project.</summary>
    public static Project RestoreProject(string project) => GetClient().RestoreProject(project);

    /// <summary>Delete a deprecated project.</summary>
    public static void DeleteProject(string project) => GetClient().DeleteProject(project);

    /// <summary>Create a cohort.</summary>
    public static Cohort CreateCohort(string name, string? description = null) =>
        GetClient().CreateCohort(name, description);

    /// <summary>List cohorts.</summary>
    public static CohortListResult ListCohorts() => GetClient().ListCohorts();

    /// <summary>Get a cohort.</summary>
    public static Cohort GetCohort(string cohortId) => GetClient().GetCohort(cohortId);

    /// <summary>Update a cohort.</summary>
    public static Cohort UpdateCohort(string cohortId, string? name = null, string? description = null) =>
        GetClient().UpdateCohort(cohortId, name, description);

    /// <summary>Delete a cohort.</summary>
    public static CohortDeleteResult DeleteCohort(string cohortId) => GetClient().DeleteCohort(cohortId);

    /// <summary>Add patients to a cohort.</summary>
    public static CohortPatientMutationResult AddPatientsToCohort(
        string cohortId,
        IReadOnlyList<string> patientIds) =>
        GetClient().AddPatientsToCohort(cohortId, patientIds);

    /// <summary>Remove patients from a cohort.</summary>
    public static CohortPatientMutationResult RemovePatientsFromCohort(
        string cohortId,
        IReadOnlyList<string> patientIds) =>
        GetClient().RemovePatientsFromCohort(cohortId, patientIds);

    /// <summary>Assign a cohort template.</summary>
    public static CohortTemplateAssignment AssignCohortTemplate(string cohortId, string summaryType) =>
        GetClient().AssignCohortTemplate(cohortId, summaryType);

    /// <summary>Unassign a cohort template.</summary>
    public static Dictionary<string, JsonElement> UnassignCohortTemplate(
        string cohortId,
        string summaryType) =>
        GetClient().UnassignCohortTemplate(cohortId, summaryType);

    /// <summary>List cohort templates.</summary>
    public static CohortTemplatesResult ListCohortTemplates(string cohortId) =>
        GetClient().ListCohortTemplates(cohortId);

    /// <summary>Register an org-native schema.</summary>
    public static SchemaRegistrationResult RegisterSchema(
        string subtype,
        string description = "",
        IReadOnlyList<Dictionary<string, object?>>? inputExamples = null,
        Dictionary<string, object?>? schema = null,
        Dictionary<string, object?>? mapping = null) =>
        GetClient().RegisterSchema(subtype, description, inputExamples, schema, mapping);

    /// <summary>List schemas.</summary>
    public static List<SchemaSummary> ListSchemas() => GetClient().ListSchemas();

    /// <summary>Get a schema.</summary>
    public static SchemaDetail GetSchema(string subtype) => GetClient().GetSchema(subtype);

    /// <summary>Check a schema.</summary>
    public static SchemaCheckResult CheckSchema(
        IReadOnlyList<Dictionary<string, object?>> examples,
        string? subtype = null,
        int? version = null,
        Dictionary<string, object?>? schema = null,
        Dictionary<string, object?>? mapping = null) =>
        GetClient().CheckSchema(examples, subtype, version, schema, mapping);

    /// <summary>Edit a schema.</summary>
    public static SchemaRegistrationResult EditSchema(
        string subtype,
        string? description = null,
        IReadOnlyList<Dictionary<string, object?>>? inputExamples = null,
        Dictionary<string, object?>? schema = null,
        Dictionary<string, object?>? mapping = null) =>
        GetClient().EditSchema(subtype, description, inputExamples, schema, mapping);

    /// <summary>Deprecate a schema.</summary>
    public static SchemaActionResult DeprecateSchema(string subtype, int? version = null) =>
        GetClient().DeprecateSchema(subtype, version);

    /// <summary>Activate a schema version.</summary>
    public static SchemaActionResult ActivateSchemaVersion(string subtype, int version) =>
        GetClient().ActivateSchemaVersion(subtype, version);

    /// <summary>List every log type in the platform catalog, with its full payload JSON Schema.</summary>
    public static LogTypeListResult ListLogTypes() => GetClient().ListLogTypes();

    /// <summary>Get one log type by subtype or alias.</summary>
    public static LogType GetLogType(string subtype) => GetClient().GetLogType(subtype);

    /// <summary>Dispose the singleton client (optional; process exit also flushes the worker).</summary>
    public static void Shutdown()
    {
        lock (Gate)
        {
            _client?.Dispose();
            _client = null;
        }
    }
}
