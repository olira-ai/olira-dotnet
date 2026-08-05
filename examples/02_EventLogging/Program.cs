/*
 * Olira SDK — Event Logging
 *
 * Two logging patterns:
 *   - Log() + Flush()   — background queue, best for real-time events
 *   - LogBatch()        — single HTTP call, best for bursts or scripted pipelines
 *
 * Also covers:
 *   - Representative payloads for common event types
 *   - OliraTrace for provenance (linking an event to its originating object)
 *   - IdempotencyKey to prevent duplicates on retry
 *
 * Requires: sdk:event-log scope (logging) + api:manage-patients scope (patient setup)
 * Run: dotnet run --project 02_EventLogging
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
    environment: env
    // project: "dev-sandbox",  // ← select a project (workspace). Logs inherit their
    //   patient's project automatically — you never pass a project when logging;
    //   just target a patient in the workspace you want. Omit for the org default.
    //   See examples/10_ProjectManagement and SDK documentation on projects.
);

// Setup — create a demo patient
var patient = client.CreatePatient(
    firstName: "Logging",
    lastName: "Demo",
    timezone: "America/New_York",
    externalIdentifiers: [new ExternalIdentifier { System = "demo", Value = "LOG-DEMO-001" }]);
var pid = patient.Id;
Console.WriteLine($"Demo patient: {pid}");

// ── Log() + Flush() — background queue ───────────────────────────────────────
// Events are enqueued and batched automatically. Call Flush() before process exit.
client.Log(
    logType: OliraLogType.UserLogin,
    patientId: pid);

client.Log(
    logType: OliraLogType.SymptomReport,
    patientId: pid,
    payload: new Dictionary<string, object?>
    {
        ["instrument"] = "esas_r",
        ["symptoms"] = new List<object?>
        {
            new Dictionary<string, object?> { ["name"] = "pain", ["score"] = 4 },
            new Dictionary<string, object?> { ["name"] = "fatigue", ["score"] = 6 },
            new Dictionary<string, object?> { ["name"] = "nausea", ["score"] = 2 },
        },
    },
    // Trace links this event back to the conversation that produced it.
    // Useful when an AI agent or a clinical form generates the event.
    trace: new OliraTrace { ObjectType = "conversation", ObjectId = "conv-abc-123" });

client.Flush();
Console.WriteLine("Queued events delivered.");

// ── LogBatch() — single request, multiple events ────────────────────────────
// Use when you have several events ready at once (e.g. end-of-session sync).
var result = client.LogBatch(
    [
        new LogSpec(
            logType: OliraLogType.VitalsMeasurement,
            patientId: pid,
            payload: new Dictionary<string, object?>
            {
                ["measurements"] = new Dictionary<string, object?>
                {
                    ["systolic_bp_mmhg"] = 128,
                    ["diastolic_bp_mmhg"] = 82,
                    ["heart_rate_bpm"] = 74,
                },
                ["context"] = new Dictionary<string, object?> { ["position"] = "sitting" },
                ["source"] = "manual_entry",
                ["collection_datetime"] = "2026-01-15T09:00:00Z",
            },
            idempotencyKey: $"{pid}:vitals:2026-01-15T09:00:00Z"),
        new LogSpec(
            logType: OliraLogType.MedicationAction,
            patientId: pid,
            payload: new Dictionary<string, object?>
            {
                ["medications"] = new List<object?>
                {
                    new Dictionary<string, object?>
                    {
                        ["action"] = "add",
                        ["medication_name"] = "Ondansetron 4mg",
                        ["dose"] = "4 mg",
                        ["frequency"] = "every 8h as needed",
                        ["route"] = "oral",
                    },
                },
            },
            idempotencyKey: $"{pid}:med-add:ondansetron-2026-01-15"),
        new LogSpec(
            logType: OliraLogType.LabResultsReceived,
            patientId: pid,
            payload: new Dictionary<string, object?>
            {
                ["panel_name"] = "CBC",
                ["collection_datetime"] = "2026-01-15T08:00:00Z",
                ["results"] = new List<object?>
                {
                    new Dictionary<string, object?>
                    {
                        ["test_name"] = "Hemoglobin",
                        ["value"] = 10.8,
                        ["unit"] = "g/dL",
                        ["reference_range"] = "12.0–16.0",
                        ["status"] = "low",
                    },
                },
            },
            idempotencyKey: $"{pid}:cbc:2026-01-15"),
        new LogSpec(
            logType: OliraLogType.ConversationCompleted,
            patientId: pid,
            payload: new Dictionary<string, object?>
            {
                ["conversation_id"] = "conv-abc-123",
                ["channel"] = "chat",
                ["transcript"] = new List<object?>
                {
                    new Dictionary<string, object?>
                    {
                        ["speaker_label"] = "agent",
                        ["text"] = "How are you feeling today?",
                    },
                    new Dictionary<string, object?>
                    {
                        ["speaker_label"] = "patient",
                        ["text"] = "Still quite fatigued, pain is about a 4.",
                    },
                },
            },
            trace: new OliraTrace { ObjectType = "conversation", ObjectId = "conv-abc-123" },
            idempotencyKey: $"{pid}:conv:conv-abc-123"),
    ]);
Console.WriteLine($"LogBatch(): accepted={result.Accepted}, failed={result.Failed}");
if (result.Errors.Count > 0)
{
    foreach (var err in result.Errors)
        Console.WriteLine($"  [{err.Index}] {err.Code}: {err.Message}");
}

// ── Demo cleanup — remove the test patient so your org stays clean ────────────
// Not part of a real integration.
client.DeletePatient(patientId: pid);
Console.WriteLine("Done.");
