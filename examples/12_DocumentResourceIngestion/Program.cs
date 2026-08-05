/*
 * Olira SDK — Document resource ingestion
 *
 * Upload clinical PDFs/images so Olira can OCR them and emit EventLog rows.
 *
 * Two paths:
 *   A — Live upload: UploadDocument() → poll DocumentHandle / DocumentResource
 *       Scope: sdk:event-log
 *   B — Historical package: CreateIngestionJob(records:…, documents:[IngestDocument(…)])
 *       Scope: sdk:historical-ingest
 *
 * Set DOCUMENT_PATH in .env to a real PDF/PNG/JPEG, or the bundled
 * examples/sample_data/demo_clinical_note.pdf is used (born-digital text PDF
 * that exercises OCR end-to-end).
 *
 * Run: dotnet run --project examples/12_DocumentResourceIngestion
 */

using Olira;
using Olira.Examples;

ExampleEnv.Load();

var apiKey = ExampleEnv.Require("OLIRA_API_KEY");
var baseUrl = ExampleEnv.BaseUrl;

using var client = new OliraClient(
    apiKey: apiKey,
    baseUrl: baseUrl,
    environment: ExampleEnv.EnvForBaseUrl(baseUrl),
    asyncFlush: false);
Console.WriteLine($"base_url={baseUrl}");

// ── Sample PDF ───────────────────────────────────────────────────────────────
var documentPathEnv = Environment.GetEnvironmentVariable("DOCUMENT_PATH");
string pdfPath;

if (!string.IsNullOrWhiteSpace(documentPathEnv))
{
    pdfPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(documentPathEnv));
    if (!File.Exists(pdfPath))
        throw new FileNotFoundException($"DOCUMENT_PATH not found: {pdfPath}");
}
else
{
    // Bundled born-digital clinical note (examples/sample_data/demo_clinical_note.pdf).
    pdfPath = FindBundledSamplePdf();
}

Console.WriteLine($"Using document: {pdfPath} ({new FileInfo(pdfPath).Length} bytes)");

var runId = Guid.NewGuid().ToString("N")[..8];
string? livePatientId = null;
string? packageExtId = null;

try
{
    // ── Create a demo patient (Path A) ───────────────────────────────────────
    var patient = client.CreatePatient(
        firstName: "Doc",
        lastName: $"Resource{runId}",
        timezone: "America/New_York",
        externalIdentifiers:
        [
            new ExternalIdentifier { System = "demo", Value = $"DOC-RES-{runId}" },
        ]);
    livePatientId = patient.Id;
    Console.WriteLine($"Patient created: {patient.Id}");

    // ── Path A — Live document upload ────────────────────────────────────────
    Console.WriteLine("\n── Path A — Live document upload ───────────────────────────────────────");
    var handle = client.UploadDocument(
        patientId: patient.Id,
        path: pdfPath,
        logType: DocumentLogType.UnstructuredReport,
        documentType: "pathology_report",
        timestamp: new DateTimeOffset(2025, 6, 15, 14, 30, 0, TimeSpan.Zero),
        idempotencyKey: $"demo-live-doc-{runId}");
    Console.WriteLine($"Uploaded document_id={handle.DocumentId} status={handle.Document.Status}");

    // Block until OCR finishes and the EventLog is emitted (or fails).
    var doc = handle.Wait(timeoutSeconds: 600.0, pollIntervalSeconds: 3.0);
    Console.WriteLine($"Final status={doc.Status}");
    Console.WriteLine($"  event_log_id={doc.EventLogId}");
    Console.WriteLine($"  ocr_page_count={doc.OcrPageCount} ocr_confidence={doc.OcrConfidence}");
    if (!string.IsNullOrEmpty(doc.Error))
        Console.WriteLine($"  error={doc.Error}");

    if (doc.Status is not (DocumentStatus.LogEmitted or DocumentStatus.OcrFailed))
        throw new InvalidOperationException($"Unexpected document status: {doc.Status}");

    // Optional — clinical note target (same live path, different labels).
    Console.WriteLine("\n── Path A (optional) — clinical note ───────────────────────────────────");
    var noteHandle = client.UploadDocument(
        patientId: patient.Id,
        path: pdfPath,
        logType: DocumentLogType.ClinicalNote,
        noteType: "progress_note",
        source: "manual_entry",
        timestamp: new DateTimeOffset(2025, 6, 16, 9, 0, 0, TimeSpan.Zero),
        idempotencyKey: $"demo-live-note-{runId}",
        wait: true,
        waitTimeoutSeconds: 600.0);
    var note = noteHandle.Document;
    Console.WriteLine(
        $"document_id={note.DocumentId} status={note.Status} event_log_id={note.EventLogId}");

    // ── Path B — Historical document package ─────────────────────────────────
    Console.WriteLine("\n── Path B — Historical document package ────────────────────────────────");
    packageExtId = $"PKG-{runId}";
    var records = new List<IngestRecord>
    {
        IngestRecord.Patient(new CreatePatientRequest
        {
            FirstName = "Pkg",
            LastName = $"Demo{runId}",
            Timezone = "UTC",
            ExternalIdentifiers =
            [
                new ExternalIdentifier { System = "demo", Value = packageExtId },
            ],
        }),
        IngestRecord.Log(new IngestLogSpec(
            eventType: "moods_report",
            patientId: packageExtId,
            timestamp: "2025-05-01T09:00:00Z",
            payload: new Dictionary<string, object?>
            {
                ["moods"] = new object[]
                {
                    new Dictionary<string, object?> { ["mood"] = "hopeful", ["intensity"] = 6 },
                },
                ["source"] = "checkin",
            },
            idempotencyKey: $"pkg-mood-{runId}")),
    };

    var documents = new List<IngestDocument>
    {
        new(
            path: pdfPath,
            patientId: packageExtId,
            logType: "unstructured_report",
            documentType: "radiology_imaging",
            timestamp: "2025-05-02T10:00:00Z",
            refId: "d1",
            idempotencyKey: $"pkg-doc-{runId}-d1"),
    };

    var job = client.CreateIngestionJob(
        records: records,
        documents: documents,
        idempotencyKey: $"demo-doc-package-{runId}",
        requireConfirmation: true);
    Console.WriteLine($"Job created: {job.JobId} (status={job.Status})");

    job = PollUntil(
        client,
        job.JobId,
        [IngestionJobStatus.AwaitingConfirmation, IngestionJobStatus.Failed, IngestionJobStatus.Completed],
        intervalSeconds: 5,
        timeoutSeconds: 600);

    if (job.Status == IngestionJobStatus.AwaitingConfirmation)
    {
        Console.WriteLine("Phase 1 review:");
        Console.WriteLine($"  patients_processed     : {job.PatientsProcessed}");
        Console.WriteLine($"  logs_processed         : {job.LogsProcessed}  (failed: {job.LogsFailed})");
        Console.WriteLine($"  documents_total        : {job.DocumentsTotal}");
        Console.WriteLine($"  documents_registered   : {job.DocumentsRegistered}");
        foreach (var err in job.ErrorSummary.Take(5))
            Console.WriteLine($"  error line {err.Line}: [{err.Code}] {err.Message}");

        job = client.ConfirmIngestionJob(jobId: job.JobId);
        Console.WriteLine("\nConfirmed — Phase 2 (OCR + replay) started…");
        job = PollUntil(
            client,
            job.JobId,
            [
                IngestionJobStatus.Completed,
                IngestionJobStatus.CompletedWithErrors,
                IngestionJobStatus.Failed,
            ],
            intervalSeconds: 15,
            timeoutSeconds: 1800);
    }

    Console.WriteLine($"\nFinal status: {job.Status}");
    Console.WriteLine(
        $"  documents: total={job.DocumentsTotal} " +
        $"ocr_ok={job.DocumentsOcrSucceeded} ocr_failed={job.DocumentsOcrFailed}");
}
finally
{
    // ── Cleanup (demo-only) ──────────────────────────────────────────────────
    Console.WriteLine("\n── Cleanup ─────────────────────────────────────────────────────────────");
    if (livePatientId is not null)
    {
        try
        {
            client.DeletePatient(patientId: livePatientId);
            Console.WriteLine($"Deleted live-path patient {livePatientId}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"  ! could not delete live patient: {e.Message}");
        }
    }

    if (packageExtId is not null)
    {
        try
        {
            var pkg = client.ListPatients(externalSystem: "demo", externalValue: packageExtId);
            if (pkg.Patients.Count > 0)
            {
                client.DeletePatient(patientId: pkg.Patients[0].Id);
                Console.WriteLine($"Deleted package-path patient {pkg.Patients[0].Id}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"  ! could not delete package patient: {e.Message}");
        }
    }

}

Console.WriteLine("Done.");

static string FindBundledSamplePdf()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        var candidate = Path.Combine(dir.FullName, "sample_data", "demo_clinical_note.pdf");
        if (File.Exists(candidate))
            return candidate;
        // bin/Debug/net8.0 → project → examples/
        var fromProject = Path.GetFullPath(Path.Combine(dir.FullName, "..", "..", "..", "..", "sample_data", "demo_clinical_note.pdf"));
        if (File.Exists(fromProject))
            return fromProject;
        dir = dir.Parent;
    }

    throw new FileNotFoundException(
        "Bundled sample_data/demo_clinical_note.pdf not found. Set DOCUMENT_PATH in examples/.env.");
}

static IngestionJob PollUntil(
    OliraClient client,
    string jobId,
    HashSet<string> targetStatuses,
    int intervalSeconds,
    int timeoutSeconds)
{
    var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
    while (DateTime.UtcNow < deadline)
    {
        var job = client.GetIngestionJob(jobId: jobId);
        var eta = job.EstimatedSecondsRemaining is { } rem ? $"  ETA ~{rem}s" : "";
        Console.WriteLine($"  [{job.Status}] {job.ProgressPct:0}%  {job.Stage}{eta}");
        if (targetStatuses.Contains(job.Status))
            return job;
        Thread.Sleep(TimeSpan.FromSeconds(intervalSeconds));
    }

    throw new TimeoutException($"Job {jobId} did not reach [{string.Join(", ", targetStatuses)}] within {timeoutSeconds}s");
}
