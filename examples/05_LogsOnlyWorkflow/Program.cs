/*
 * Olira SDK — Logs-Only Ingestion Workflow
 *
 * Common use case: patients already exist in your org (created via CreatePatientsBatch
 * or the Console), and you want to ingest historical logs for them without re-creating
 * the patient records.
 *
 * The ingestion job's Stage 3 resolves patient_id values against existing org patients
 * by external_identifier — no patient records needed in the JSONL.
 *
 * Steps:
 *   1. Create patients in advance via CreatePatientsBatch()
 *   2. Submit an ingestion job containing only log records
 *   3. Confirm and poll to COMPLETED
 *
 * Requires: api:manage-patients + sdk:historical-ingest scopes
 * Run: dotnet run --project 05_LogsOnlyWorkflow
 */

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
    var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
    while (DateTime.UtcNow < deadline)
    {
        var job = client.GetIngestionJob(jobId: jobId);
        Console.WriteLine($"  [{job.Status}] {job.ProgressPct:0}%  {job.Stage}");
        if (targetStatuses.Contains(job.Status))
            return job;
        Thread.Sleep(TimeSpan.FromSeconds(intervalSeconds));
    }

    throw new TimeoutException($"Job {jobId} did not reach {{{string.Join(", ", targetStatuses)}}} within {timeoutSeconds}s");
}

// ── Step 1: Create patients upfront via batch API ─────────────────────────────
Console.WriteLine("Step 1: Creating patients via CreatePatientsBatch()…");
var batch = client.CreatePatientsBatch(
    [
        new CreatePatientRequest
        {
            FirstName = "Emma",
            LastName = "Rossi",
            DateOfBirth = "1972-11-20T00:00:00Z",
            Timezone = "America/New_York",
            ExternalIdentifiers = [new ExternalIdentifier { System = "epic", Value = "LOGS-ONLY-E001" }],
        },
        new CreatePatientRequest
        {
            FirstName = "Marco",
            LastName = "Silva",
            DateOfBirth = "1985-03-07T00:00:00Z",
            Timezone = "America/Chicago",
            ExternalIdentifiers = [new ExternalIdentifier { System = "epic", Value = "LOGS-ONLY-M002" }],
        },
    ]);
var patientIds = batch.Items.Select(item => item.Id).ToList();
Console.WriteLine($"  Created {batch.Count} patients: [{string.Join(", ", patientIds.Select(i => i[..Math.Min(8, i.Length)] + "…"))}]");

// ── Step 2: Submit logs-only ingestion job ─────────────────────────────────────
// patient_id in each log uses the external_identifier value ("LOGS-ONLY-E001" etc.)
// Stage 3 resolves these against the org's existing patients — no patient records needed.
Console.WriteLine("\nStep 2: Submitting logs-only ingestion job…");
var records = new List<IngestRecord>
{
    IngestRecord.Log(
        new IngestLogSpec(
            eventType: "symptom_report",
            patientId: "LOGS-ONLY-E001",  // external_identifier value
            timestamp: "2025-01-10T09:00:00Z",
            payload: new Dictionary<string, object?>
            {
                ["instrument"] = "esas_r",
                ["symptoms"] = new List<object?>
                {
                    new Dictionary<string, object?> { ["name"] = "pain", ["score"] = 5 },
                },
            },
            idempotencyKey: "e001-symptom-2025-01-10")),
    IngestRecord.Log(
        new IngestLogSpec(
            eventType: "moods_report",
            patientId: "LOGS-ONLY-E001",
            timestamp: "2025-01-11T08:00:00Z",
            payload: new Dictionary<string, object?>
            {
                ["moods"] = new List<object?>
                {
                    new Dictionary<string, object?> { ["mood"] = "tired", ["intensity"] = 6 },
                },
                ["source"] = "checkin",
            },
            idempotencyKey: "e001-mood-2025-01-11")),
    IngestRecord.Log(
        new IngestLogSpec(
            eventType: "symptom_report",
            patientId: "LOGS-ONLY-M002",
            timestamp: "2025-02-05T14:00:00Z",
            payload: new Dictionary<string, object?>
            {
                ["instrument"] = "esas_r",
                ["symptoms"] = new List<object?>
                {
                    new Dictionary<string, object?> { ["name"] = "nausea", ["score"] = 3 },
                },
            },
            idempotencyKey: "m002-symptom-2025-02-05")),
    IngestRecord.Log(
        new IngestLogSpec(
            eventType: "moods_report",
            patientId: "LOGS-ONLY-M002",
            timestamp: "2025-02-06T09:00:00Z",
            payload: new Dictionary<string, object?>
            {
                ["moods"] = new List<object?>
                {
                    new Dictionary<string, object?> { ["mood"] = "calm", ["intensity"] = 7 },
                },
                ["source"] = "checkin",
            },
            idempotencyKey: "m002-mood-2025-02-06")),
};

var job = client.CreateIngestionJob(
    records: records,
    idempotencyKey: "logs-only-demo-2026",
    requireConfirmation: true);
Console.WriteLine($"  Job created: {job.JobId} (status={job.Status})");

// ── Step 3: Review and confirm ─────────────────────────────────────────────────
Console.WriteLine("\nStep 3: Polling to AWAITING_CONFIRMATION…");
job = PollUntil(
    client,
    job.JobId,
    [IngestionJobStatus.AwaitingConfirmation, IngestionJobStatus.Failed],
    intervalSeconds: 5);

if (job.Status == IngestionJobStatus.AwaitingConfirmation)
{
    Console.WriteLine($"\n  patients_processed : {job.PatientsProcessed}  (expected 0 — no patient records in file)");
    Console.WriteLine($"  logs_processed     : {job.LogsProcessed}");
    Console.WriteLine($"  logs_failed        : {job.LogsFailed}");
    if (job.ErrorSummary.Count > 0)
    {
        foreach (var e in job.ErrorSummary)
            Console.WriteLine($"  Error: [{e.Code}] {e.Message}");
    }

    job = client.ConfirmIngestionJob(jobId: job.JobId);
    Console.WriteLine("\nConfirmed — polling to COMPLETED…");
    job = PollUntil(
        client,
        job.JobId,
        [IngestionJobStatus.Completed, IngestionJobStatus.CompletedWithErrors, IngestionJobStatus.Failed],
        intervalSeconds: 15);
    Console.WriteLine($"\nFinal: {job.Status}  replay_statuses={string.Join(", ", job.PatientReplayStatuses.Select(kv => $"{kv.Key}={kv.Value}"))}");
}

// ── Demo cleanup — remove test patients so your org stays clean ───────────────
// Not part of a real integration.
foreach (var pid in patientIds)
    client.DeletePatient(patientId: pid);
Console.WriteLine($"\nCleaned up {patientIds.Count} patients.");
