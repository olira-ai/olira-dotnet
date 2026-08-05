/*
 * Olira SDK — Quickstart
 *
 * The shortest path to a working integration:
 *   1. Initialise the SDK
 *   2. Create a patient
 *   3. Log a health event
 *
 * Requirements: copy .env.example → .env and fill in OLIRA_API_KEY.
 * Run: dotnet run --project 00_Quickstart
 */

using Olira;
using Olira.Examples;

ExampleEnv.Load();
var apiKey = ExampleEnv.Require("OLIRA_API_KEY");
var baseUrl = ExampleEnv.BaseUrl;
var env = ExampleEnv.EnvForBaseUrl(baseUrl);

// Initialise once at startup — all module-level functions (OliraModule.Log, OliraModule.CreatePatient…)
// use this singleton client.
OliraModule.Init(
    apiKey: apiKey,
    baseUrl: baseUrl,
    environment: env
    // project: "dev-sandbox",  // ← optional: isolate to a project (workspace); or set
    //   OLIRA_PROJECT. Omit for the org's default project. See 10_ProjectManagement.
);

// 1. Create a patient
var patient = OliraModule.CreatePatient(
    firstName: "Jane",
    lastName: "Demo",
    dateOfBirth: "1985-04-12T00:00:00Z",
    timezone: "America/New_York");
Console.WriteLine($"Patient created: {patient.Id}");

// 2. Log a health event — enqueued for background delivery
OliraModule.Log(
    logType: OliraLogType.SymptomReport,
    patientId: patient.Id,
    payload: new Dictionary<string, object?>
    {
        ["instrument"] = "esas_r",
        ["symptoms"] = new List<object?>
        {
            new Dictionary<string, object?> { ["name"] = "pain", ["score"] = 3 },
        },
    });

// 3. Flush drains the background queue before the process exits.
// In a long-running server, call OliraModule.Flush() in your shutdown handler instead
// of inline like this — you don't need to flush after every Log() call.
OliraModule.Flush();
Console.WriteLine("Event delivered.");

// ── Demo cleanup — remove the test patient so your org stays clean ────────────
// Not part of a real integration.
OliraModule.DeletePatient(patientId: patient.Id);
Console.WriteLine("Done.");
