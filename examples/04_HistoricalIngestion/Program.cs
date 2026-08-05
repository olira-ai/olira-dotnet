/*
 * Olira SDK — Historical Data Ingestion
 *
 * Two paths to bulk-load existing patient data before going live:
 *
 *   Path A — File upload (recommended for large datasets)
 *     SDK uploads a JSONL file to S3 and creates the ingestion job in one call.
 *     No size cap beyond the org limit (default 100 MB, configurable server-side).
 *
 *   Path B — Inline records (for smaller datasets built programmatically)
 *     Pass a list of IngestRecord objects directly — no file on disk needed.
 *     Capped at 50,000 records per job. Optional OliraTrace on individual logs.
 *
 * Both paths go through the same pipeline:
 *   QUEUED → VALIDATING → INSERTING_PATIENTS → INSERTING_LOGS → AWAITING_CONFIRMATION
 *   (then, after confirm)
 *   EXTRACTING → REPLAYING → LOADING → REBASING → EMBEDDING → BACKFILLING → COMPLETED
 *   (EXTRACTING / REBASING / EMBEDDING are skipped when the job has nothing for that stage)
 *
 * Requires: sdk:historical-ingest scope
 * Run: dotnet run --project 04_HistoricalIngestion
 */

using System.Text.Json;
using Olira;
using Olira.Examples;

ExampleEnv.Load();
var apiKey = ExampleEnv.Require("OLIRA_API_KEY");
var baseUrl = ExampleEnv.BaseUrl;
var env = ExampleEnv.EnvForBaseUrl(baseUrl);

using var client = new OliraClient(
    apiKey: apiKey,
    baseUrl: baseUrl,
    environment: env,
    asyncFlush: false);  // ingestion uses direct HTTP calls, not the background log queue

static IngestionJob PollUntil(
    OliraClient client,
    string jobId,
    HashSet<string> targetStatuses,
    int intervalSeconds = 10,
    int timeoutSeconds = 300)
{
    // Poll GetIngestionJob until status is in targetStatuses.
    var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
    while (DateTime.UtcNow < deadline)
    {
        var job = client.GetIngestionJob(jobId: jobId);
        var eta = job.EstimatedSecondsRemaining is { } secs ? $"  ETA ~{secs}s" : "";
        Console.WriteLine($"  [{job.Status}] {job.ProgressPct:0}%  {job.Stage}{eta}");
        if (targetStatuses.Contains(job.Status))
            return job;
        Thread.Sleep(TimeSpan.FromSeconds(intervalSeconds));
    }

    throw new TimeoutException($"Job {jobId} did not reach {{{string.Join(", ", targetStatuses)}}} within {timeoutSeconds}s");
}

// ── Path A: File upload ────────────────────────────────────────────────────────

var examplesDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
var jsonlFile = Path.Combine(examplesDir, "sample_data.jsonl");

// Write a minimal sample file if one doesn't exist
if (!File.Exists(jsonlFile))
{
    var lines = new List<Dictionary<string, object?>>
    {
        new()
        {
            ["type"] = "patient",
            ["data"] = new Dictionary<string, object?>
            {
                ["first_name"] = "Jane",
                ["last_name"] = "FileDemo",
                ["timezone"] = "UTC",
                ["external_identifiers"] = new List<object?>
                {
                    new Dictionary<string, object?> { ["system"] = "demo", ["value"] = "FILE-001" },
                },
            },
        },
        new()
        {
            ["type"] = "log",
            ["data"] = new Dictionary<string, object?>
            {
                ["event_type"] = "moods_report",
                ["patient_id"] = "FILE-001",
                ["timestamp"] = "2025-06-01T09:00:00Z",
                ["payload"] = new Dictionary<string, object?>
                {
                    ["moods"] = new List<object?>
                    {
                        new Dictionary<string, object?> { ["mood"] = "hopeful", ["intensity"] = 6 },
                    },
                    ["source"] = "checkin",
                },
                ["idempotency_key"] = "file-001-mood-01",
                ["trace"] = new Dictionary<string, object?>
                {
                    ["object_type"] = "emr_record",
                    ["object_id"] = "epic-encounter-98765",
                },
            },
        },
    };
    File.WriteAllText(jsonlFile, string.Join("\n", lines.Select(row => JsonSerializer.Serialize(row))));
    Console.WriteLine($"Created sample file: {jsonlFile}");
}

Console.WriteLine("\n── Path A: File upload ──");
var job = client.CreateIngestionJob(
    file: jsonlFile,
    idempotencyKey: "demo-file-upload-2026",
    requireConfirmation: true,
    summaryTypes: ["emotional_state_snapshot"]);  // only backfill this view type
Console.WriteLine($"Job created: {job.JobId} (status={job.Status})");

// Poll Phase 1 — wait for AWAITING_CONFIRMATION
job = PollUntil(
    client,
    job.JobId,
    [IngestionJobStatus.AwaitingConfirmation, IngestionJobStatus.Failed, IngestionJobStatus.Completed],
    intervalSeconds: 5);

if (job.Status == IngestionJobStatus.AwaitingConfirmation)
{
    Console.WriteLine("\nReview summary:");
    Console.WriteLine($"  Patients processed : {job.PatientsProcessed}");
    Console.WriteLine($"  Logs inserted      : {job.LogsProcessed}  (failed: {job.LogsFailed})");
    Console.WriteLine($"  By event type      : {string.Join(", ", job.LogsByEventType.Select(kv => $"{kv.Key}={kv.Value}"))}");
    if (job.ErrorSummary.Count > 0)
    {
        foreach (var err in job.ErrorSummary)
            Console.WriteLine($"  Error  line {err.Line}: [{err.Code}] {err.Message}");
    }

    // Confirm to start Phase 2 (graph replay + view backfill)
    job = client.ConfirmIngestionJob(jobId: job.JobId);
    Console.WriteLine("\nConfirmed — Phase 2 started, polling…");
    job = PollUntil(
        client,
        job.JobId,
        [IngestionJobStatus.Completed, IngestionJobStatus.CompletedWithErrors, IngestionJobStatus.Failed],
        intervalSeconds: 15);
    Console.WriteLine($"\nFinal status: {job.Status}  (tokens_used={job.TokensUsed})");
}
else if (job.Status == IngestionJobStatus.Failed)
{
    var preview = string.Join("; ", job.ErrorSummary.Take(3).Select(e => $"[{e.Code}] {e.Message}"));
    Console.WriteLine($"Job FAILED: {preview}");
}

// ── Path B: Inline records ─────────────────────────────────────────────────────

Console.WriteLine("\n── Path B: Inline records ──");
var records = new List<IngestRecord>
{
    IngestRecord.Patient(
        new CreatePatientRequest
        {
            FirstName = "Bob",
            LastName = "InlineDemo",
            Timezone = "America/New_York",
            ExternalIdentifiers = [new ExternalIdentifier { System = "demo", Value = "INLINE-002" }],
        }),
    IngestRecord.Log(
        new IngestLogSpec(
            eventType: "symptom_report",
            patientId: "INLINE-002",  // matches external_identifier value above
            timestamp: "2025-07-15T10:00:00Z",
            payload: new Dictionary<string, object?>
            {
                ["instrument"] = "esas_r",
                ["symptoms"] = new List<object?>
                {
                    new Dictionary<string, object?> { ["name"] = "fatigue", ["score"] = 5 },
                    new Dictionary<string, object?> { ["name"] = "pain", ["score"] = 3 },
                },
            },
            idempotencyKey: "inline-002-symptom-01",
            trace: new OliraTrace { ObjectType = "emr_record", ObjectId = "epic-encounter-98765" })),
    IngestRecord.Log(
        new IngestLogSpec(
            eventType: "moods_report",
            patientId: "INLINE-002",
            timestamp: "2025-07-16T08:30:00Z",
            payload: new Dictionary<string, object?>
            {
                ["moods"] = new List<object?>
                {
                    new Dictionary<string, object?> { ["mood"] = "anxious", ["intensity"] = 4 },
                },
                ["source"] = "checkin",
            },
            idempotencyKey: "inline-002-mood-01")),
};

// requireConfirmation: false — run straight through without a review pause
job = client.CreateIngestionJob(
    records: records,
    idempotencyKey: "demo-inline-2026",
    requireConfirmation: false);
Console.WriteLine($"Job created: {job.JobId} (status={job.Status})");
job = PollUntil(
    client,
    job.JobId,
    [IngestionJobStatus.Completed, IngestionJobStatus.CompletedWithErrors, IngestionJobStatus.Failed],
    intervalSeconds: 10);
Console.WriteLine($"Final status: {job.Status}");
Console.WriteLine($"  patient_replay_statuses: {string.Join(", ", job.PatientReplayStatuses.Select(kv => $"{kv.Key}={kv.Value}"))}");
