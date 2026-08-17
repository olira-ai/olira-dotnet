/*
 * Olira SDK — FHIR R4 Ingestion
 *
 * LogFhir() accepts a single FHIR R4 resource and maps it to Olira log types
 * using the same absorber as Epic/Cerner integrations. You don't choose a
 * log_type or build Olira-shaped payloads — the absorber handles the mapping.
 *
 * Also covers:
 *   - Error handling for unsupported resource types
 *   - Error handling for missing resourceType
 *
 * Requires: sdk:event-log scope (FHIR ingest) + api:manage-patients scope (patient setup)
 * Run: dotnet run --project 03_FhirIngestion
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
    asyncFlush: false);

// Setup — create a demo patient
var patient = client.CreatePatient(
    firstName: "FHIR",
    lastName: "Demo",
    timezone: "America/New_York",
    externalIdentifiers: [new ExternalIdentifier { System = "demo", Value = "FHIR-DEMO-001" }]);
var pid = patient.Id;
Console.WriteLine($"Demo patient: {pid}");

// ── Condition ─────────────────────────────────────────────────────────────────
var result = client.LogFhir(
    patientId: pid,
    resource: new Dictionary<string, object?>
    {
        ["resourceType"] = "Condition",
        ["id"] = "condition-1",
        ["clinicalStatus"] = new Dictionary<string, object?>
        {
            ["coding"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["system"] = "http://terminology.hl7.org/CodeSystem/condition-clinical",
                    ["code"] = "active",
                },
            },
        },
        ["code"] = new Dictionary<string, object?>
        {
            ["coding"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["system"] = "http://snomed.info/sct",
                    ["code"] = "254837009",
                    ["display"] = "Breast cancer",
                },
            },
            ["text"] = "Breast cancer",
        },
        ["subject"] = new Dictionary<string, object?> { ["reference"] = $"Patient/{pid}" },
        ["onsetDateTime"] = "2025-01-10T00:00:00Z",
    });
Console.WriteLine($"Condition        — accepted={result.Accepted}");

// ── MedicationRequest ─────────────────────────────────────────────────────────
result = client.LogFhir(
    patientId: pid,
    resource: new Dictionary<string, object?>
    {
        ["resourceType"] = "MedicationRequest",
        ["id"] = "med-1",
        ["status"] = "active",
        ["intent"] = "order",
        ["medicationCodeableConcept"] = new Dictionary<string, object?>
        {
            ["coding"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["system"] = "http://www.nlm.nih.gov/research/umls/rxnorm",
                    ["code"] = "1049502",
                },
            },
            ["text"] = "Ondansetron 4mg",
        },
        ["subject"] = new Dictionary<string, object?> { ["reference"] = $"Patient/{pid}" },
        ["authoredOn"] = "2025-03-01T00:00:00Z",
        ["dosageInstruction"] = new List<object?>
        {
            new Dictionary<string, object?> { ["text"] = "4mg orally every 8 hours as needed" },
        },
    });
Console.WriteLine($"MedicationRequest — accepted={result.Accepted}");

// ── Appointment ───────────────────────────────────────────────────────────────
result = client.LogFhir(
    patientId: pid,
    resource: new Dictionary<string, object?>
    {
        ["resourceType"] = "Appointment",
        ["id"] = "appt-1",
        ["status"] = "booked",
        ["serviceType"] = new List<object?>
        {
            new Dictionary<string, object?>
            {
                ["coding"] = new List<object?>
                {
                    new Dictionary<string, object?>
                    {
                        ["code"] = "oncology",
                        ["display"] = "Oncology",
                    },
                },
            },
        },
        ["start"] = "2026-06-15T09:00:00Z",
        ["end"] = "2026-06-15T09:30:00Z",
        ["participant"] = new List<object?>
        {
            new Dictionary<string, object?>
            {
                ["actor"] = new Dictionary<string, object?> { ["reference"] = $"Patient/{pid}" },
                ["status"] = "accepted",
            },
        },
    });
Console.WriteLine($"Appointment       — accepted={result.Accepted}");

// ── Safe retry with idempotencyKey ────────────────────────────────────────────
// If a call's response is lost to a network error or 5xx, resend it verbatim —
// the same idempotencyKey guarantees no duplicate event is created.
var retryKey = "condition-2026-01-10";
var conditionResource = new Dictionary<string, object?>
{
    ["resourceType"] = "Condition",
    ["id"] = "condition-retry-demo",
    ["code"] = new Dictionary<string, object?> { ["text"] = "Type 2 diabetes" },
    ["subject"] = new Dictionary<string, object?> { ["reference"] = $"Patient/{pid}" },
};
result = client.LogFhir(patientId: pid, resource: conditionResource, idempotencyKey: retryKey);
Console.WriteLine($"Retry demo (1st)  — accepted={result.Accepted}");
result = client.LogFhir(patientId: pid, resource: conditionResource, idempotencyKey: retryKey);
Console.WriteLine($"Retry demo (2nd)  — accepted={result.Accepted} (deduped, no new event created)");

// ── Error handling — unsupported resource type ────────────────────────────────
try
{
    client.LogFhir(
        patientId: pid,
        resource: new Dictionary<string, object?>
        {
            ["resourceType"] = "SupplyDelivery",
            ["status"] = "completed",
        });
}
catch (ValidationError e)
{
    Console.WriteLine($"Unsupported type  — ValidationError: {e}");
}

// ── Error handling — missing resourceType ────────────────────────────────────
try
{
    client.LogFhir(
        patientId: pid,
        resource: new Dictionary<string, object?>
        {
            ["status"] = "final",
            ["code"] = new Dictionary<string, object?> { ["text"] = "BP" },
        });
}
catch (ValidationError e)
{
    Console.WriteLine($"Missing type      — ValidationError: {e}");
}

// ── Demo cleanup — remove the test patient so your org stays clean ────────────
// Not part of a real integration.
client.DeletePatient(patientId: pid);
Console.WriteLine("Done.");
