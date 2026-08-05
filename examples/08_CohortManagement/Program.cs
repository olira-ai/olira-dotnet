using System.Text.Json;
using Olira;
using Olira.Examples;

/*
 * Olira SDK — Cohort Management
 *
 * Covers the full cohort lifecycle:
 *   - Create a cohort
 *   - List cohorts
 *   - Get cohort detail
 *   - Update cohort metadata
 *   - Enrol a patient
 *   - Assign a summary template
 *   - List template assignments
 *   - Unassign a template
 *   - Remove a patient
 *   - Delete the cohort
 *
 * All operations require: api:manage-patients scope.
 * Run: dotnet run --project 08_CohortManagement
 */

ExampleEnv.Load();
var apiKey = ExampleEnv.Require("OLIRA_API_KEY");
var baseUrl = ExampleEnv.BaseUrl;

// Optional: set to a real summary_type slug your org has active (e.g. "symptom_overview").
// If empty, template assignment steps are skipped.
var summaryType = ExampleEnv.Get("OLIRA_EXAMPLE_SUMMARY_TYPE", "");

using var client = new OliraClient(
    apiKey: apiKey,
    baseUrl: baseUrl,
    environment: OliraEnv.Development,
    asyncFlush: false,
    timeout: 30.0);

// ── 1. Create a test patient to enrol ─────────────────────────────────────────
Console.WriteLine("\n── 1. Create test patient ──────────────────────────────────────────────");
var patient = client.CreatePatient(
    firstName: "Cohort",
    lastName: $"Example{Guid.NewGuid().ToString("N")[..6]}",
    timezone: "America/New_York");
Console.WriteLine($"  patient.id = {patient.Id}");

// ── 2. Create a cohort ────────────────────────────────────────────────────────
Console.WriteLine("\n── 2. Create cohort ────────────────────────────────────────────────────");
var cohort = client.CreateCohort(
    name: $"Example Cohort {Guid.NewGuid().ToString("N")[..8]}",
    description: "Created by 08_CohortManagement");
Console.WriteLine($"  cohort.id          = {cohort.Id}");
Console.WriteLine($"  cohort.name        = {cohort.Name}");
Console.WriteLine($"  cohort.patient_ids = [{string.Join(", ", cohort.PatientIds)}]");

// ── 3. List cohorts ───────────────────────────────────────────────────────────
Console.WriteLine("\n── 3. List cohorts ─────────────────────────────────────────────────────");
var result = client.ListCohorts();
Console.WriteLine($"  total cohorts in org: {result.Data.Count}");
foreach (var item in result.Data)
{
    Console.WriteLine($"  • {item.Id}  '{item.Name}'  patients={item.PatientCount}");
}

// ── 4. Get cohort detail ──────────────────────────────────────────────────────
Console.WriteLine("\n── 4. Get cohort ───────────────────────────────────────────────────────");
var fetched = client.GetCohort(cohort.Id);
Console.WriteLine($"  fetched.name        = {fetched.Name}");
Console.WriteLine($"  fetched.description = {fetched.Description}");

// ── 5. Update cohort ──────────────────────────────────────────────────────────
Console.WriteLine("\n── 5. Update cohort ────────────────────────────────────────────────────");
var updated = client.UpdateCohort(
    cohortId: cohort.Id,
    description: "Updated by 08_CohortManagement");
Console.WriteLine($"  updated.description = {updated.Description}");

// ── 6. Add patient to cohort ──────────────────────────────────────────────────
Console.WriteLine("\n── 6. Add patient to cohort ────────────────────────────────────────────");
var addResult = client.AddPatientsToCohort(cohort.Id, [patient.Id]);
Console.WriteLine($"  patient_count after add = {addResult.PatientCount}");

// verify via get
var detail = client.GetCohort(cohort.Id);
Console.WriteLine($"  patient_ids = [{string.Join(", ", detail.PatientIds)}]");

// ── 7. Template assignment (optional) ─────────────────────────────────────────
if (!string.IsNullOrWhiteSpace(summaryType))
{
    Console.WriteLine($"\n── 7. Assign template '{summaryType}' ─────────────────────────────────");
    var assignment = client.AssignCohortTemplate(cohort.Id, summaryType);
    Console.WriteLine($"  assignment.id           = {assignment.Id}");
    Console.WriteLine($"  assignment.summary_type = {assignment.SummaryType}");
    Console.WriteLine($"  assignment.template_id  = {assignment.TemplateId}");

    Console.WriteLine("\n── 7b. List cohort templates ───────────────────────────────────────");
    var templates = client.ListCohortTemplates(cohort.Id);
    foreach (var t in templates.Data)
    {
        Console.WriteLine($"  • {t.SummaryType}  assigned_at={t.AssignedAt}");
    }

    Console.WriteLine($"\n── 7c. Unassign template '{summaryType}' ──────────────────────────────");
    var unassignResult = client.UnassignCohortTemplate(cohort.Id, summaryType);
    object? deleted = null;
    if (unassignResult.TryGetValue("deleted", out var deletedEl))
    {
        deleted = deletedEl.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => deletedEl.ToString(),
        };
    }

    Console.WriteLine($"  deleted = {deleted}");
}
else
{
    Console.WriteLine("\n── 7. Template steps skipped (set OLIRA_EXAMPLE_SUMMARY_TYPE to enable) ──");
}

// ── 8. Remove patient from cohort ────────────────────────────────────────────
Console.WriteLine("\n── 8. Remove patient from cohort ───────────────────────────────────────");
var removeResult = client.RemovePatientsFromCohort(cohort.Id, [patient.Id]);
Console.WriteLine($"  patient_count after remove = {removeResult.PatientCount}");

// ── 9. Delete cohort ──────────────────────────────────────────────────────────
Console.WriteLine("\n── 9. Delete cohort ────────────────────────────────────────────────────");
var deleteResult = client.DeleteCohort(cohort.Id);
Console.WriteLine($"  deleted    = {deleteResult.Deleted}");
Console.WriteLine($"  cohort_id  = {deleteResult.CohortId}");

// ── Cleanup ───────────────────────────────────────────────────────────────────
Console.WriteLine("\n── Cleanup ─────────────────────────────────────────────────────────────");
client.DeletePatient(patient.Id);
Console.WriteLine($"  Soft-deleted patient {patient.Id}");

Console.WriteLine("\nDone.");
