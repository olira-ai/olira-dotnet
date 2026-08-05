/*
 * Olira SDK — Patient Management
 *
 * Covers the full patient lifecycle:
 *   - Create with full demographics
 *   - Create a shell patient (external ID only)
 *   - Batch create up to 500 patients at once
 *   - Look up by external identifier
 *   - Update demographics
 *   - Delete — soft (default) vs. permanent (hard-delete + cascade)
 *
 * Requires: api:manage-patients scope
 * Run: dotnet run --project 01_PatientManagement
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
    asyncFlush: false  // this example doesn't use Log() — disable the background queue thread
    // project: "dev-sandbox",  // ← isolate every patient op to a project (workspace).
    //   Omit for the org's default project. Patients created here are invisible to
    //   other projects, and ListPatients() returns only this project's patients.
    //   See examples/10_ProjectManagement and SDK documentation on projects.
);

var createdIds = new List<string>();

// ── Full demographics ──────────────────────────────────────────────────────────
var patient = client.CreatePatient(
    firstName: "Alice",
    lastName: "Nguyen",
    dateOfBirth: "1978-09-03T00:00:00Z",
    sex: "female",
    timezone: "America/Chicago",
    primaryDiseaseSite: "breast",
    diseaseStage: "Stage II",
    externalIdentifiers: [new ExternalIdentifier { System = "epic", Value = "MRN-10001" }],
    metadata: new Dictionary<string, object?> { ["trial_arm"] = "A", ["site"] = "CHI-01" });
createdIds.Add(patient.Id);
Console.WriteLine($"Created patient: {patient.Id} — {patient.FirstName} {patient.LastName}");

// ── Shell patient — external ID only, no demographics yet ────────────────────
// Useful when you only have a system ID and will sync demographics later.
var shell = client.CreatePatient(
    externalIdentifiers: [new ExternalIdentifier { System = "flatiron", Value = "FLT-99002" }]);
createdIds.Add(shell.Id);
Console.WriteLine($"Shell patient:  {shell.Id} (no name yet)");

// ── Update — fill in demographics after the fact ─────────────────────────────
var updated = client.UpdatePatient(
    patientId: shell.Id,
    firstName: "Bob",
    lastName: "Chen",
    dateOfBirth: "1990-02-14T00:00:00Z",
    timezone: "America/Los_Angeles");
Console.WriteLine($"Updated shell:  {updated.FirstName} {updated.LastName}");

// ── Look up by external identifier ───────────────────────────────────────────
var result = client.ListPatients(externalSystem: "epic", externalValue: "MRN-10001");
if (result.Patients.Count > 0)
{
    var found = result.Patients[0];
    Console.WriteLine($"Lookup by EID:  found {found.Id} ({found.FirstName} {found.LastName})");
}

// ── Batch create — up to 500 patients in one call ────────────────────────────
var batchResult = client.CreatePatientsBatch(
    [
        new CreatePatientRequest
        {
            FirstName = "Carol",
            LastName = "Davis",
            Timezone = "UTC",
            ExternalIdentifiers = [new ExternalIdentifier { System = "epic", Value = "BATCH-C001" }],
        },
        new CreatePatientRequest
        {
            FirstName = "David",
            LastName = "Park",
            Timezone = "UTC",
            ExternalIdentifiers = [new ExternalIdentifier { System = "epic", Value = "BATCH-D002" }],
        },
    ]);
createdIds.AddRange(batchResult.Items.Select(item => item.Id));
Console.WriteLine($"Batch create:   {batchResult.Count} created, {batchResult.Errors.Count} errors");

// ── Delete — soft (default) vs. permanent ────────────────────────────────────
// Soft-delete sets status=deleted. The record and all its logs/state are retained
// for audit purposes, and the patient stops appearing in ListPatients() — but its
// external identifiers are also freed up, so a new create can reuse the same value.
client.DeletePatient(patientId: shell.Id);
Console.WriteLine($"Soft-deleted:   {shell.Id} (record + logs retained, hidden from listings)");

// permanent: true hard-deletes the patient AND cascade-deletes every associated
// document (event logs, state, conversations, notes, etc). Irreversible. Use this
// once you're sure a record was created in error (e.g. test data, or a duplicate)
// and you need its logs gone entirely, not just hidden.
client.DeletePatient(patientId: patient.Id, permanent: true);
createdIds.Remove(patient.Id);
Console.WriteLine($"Permanently deleted: {patient.Id} (record + all associated data removed)");

// ── Demo cleanup — remove remaining test patients so your org stays clean ────
// Not part of a real integration. permanent: true here so the demo leaves no residue.
foreach (var pid in createdIds)
{
    if (pid == shell.Id)
        continue;  // already soft-deleted above
    client.DeletePatient(patientId: pid, permanent: true);
}

Console.WriteLine($"Cleaned up {createdIds.Count} patients.");
