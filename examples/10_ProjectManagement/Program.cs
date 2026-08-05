/*
 * Olira SDK — Project (Workspace) Management
 *
 * A project is a self-contained, isolated workspace within your organization:
 * its own patients, event logs, patient state, views, cohorts, and configuration.
 *
 * Covers the full project lifecycle:
 *   - List projects (every org has a "default" project)
 *   - Create a new (empty) project
 *   - Select a project for data operations via new OliraClient(..., project: slug)
 *   - Duplicate a project's *configuration* into a new one (never its patients/data)
 *   - Rename / retag a project
 *   - Deprecate (soft-delete → recoverable) and Restore
 *   - Permanently delete a deprecated project (irreversible)
 *
 * All project-management calls require:
 *   - the api:manage-projects scope, AND
 *   - an org-wide API key (a project-locked key is confined to its own
 *     workspace and gets 403 on these routes).
 *
 * Run: dotnet run --project examples/10_ProjectManagement
 */

using Olira;
using Olira.Examples;

ExampleEnv.Load();

var apiKey = ExampleEnv.Require("OLIRA_API_KEY"); // must be org-wide with api:manage-projects
var baseUrl = ExampleEnv.BaseUrl;
var run = Guid.NewGuid().ToString("N")[..6];

using var client = new OliraClient(
    apiKey: apiKey,
    baseUrl: baseUrl,
    environment: OliraEnv.Development,
    asyncFlush: false,
    timeout: 30.0);

// Track creations so cleanup removes them even if a step fails partway through.
var createdProjectIds = new List<string>();
var createdPatients = new List<(OliraClient Scoped, string PatientId)>();
OliraClient? devClient = null;

try
{
    // ── 1. List projects ──────────────────────────────────────────────────────
    Console.WriteLine("\n── 1. List projects ────────────────────────────────────────────────────");
    var projects = client.ListProjects();
    foreach (var p in projects.Data)
    {
        var def = p.IsDefault ? " (default)" : "";
        Console.WriteLine($"  • {p.Slug,-24} status={p.Status}{def}");
    }

    // ── 2. Create a new, empty project ────────────────────────────────────────
    Console.WriteLine("\n── 2. Create project ───────────────────────────────────────────────────");
    var dev = client.CreateProject(
        name: $"Dev Sandbox {run}",
        slug: $"dev-sandbox-{run}",
        description: "Created by 10_ProjectManagement",
        environment: "dev");
    createdProjectIds.Add(dev.Id);
    Console.WriteLine($"  id          = {dev.Id}");
    Console.WriteLine($"  slug        = {dev.Slug}   ← pass this to new OliraClient(..., project: ...)");
    Console.WriteLine($"  environment = {dev.Environment}");

    // ── 3. Operate inside a project ───────────────────────────────────────────
    Console.WriteLine("\n── 3. Write data scoped to the project ─────────────────────────────────");
    devClient = new OliraClient(
        apiKey: apiKey,
        baseUrl: baseUrl,
        environment: OliraEnv.Development,
        project: dev.Slug,
        asyncFlush: false,
        timeout: 30.0);
    var patient = devClient.CreatePatient(firstName: "Sandbox", lastName: $"Patient{run}");
    createdPatients.Add((devClient, patient.Id));
    Console.WriteLine($"  created patient {patient.Id} inside project '{dev.Slug}'");

    // ── 4. Duplicate a project (config only) ──────────────────────────────────
    Console.WriteLine("\n── 4. Duplicate project (config only) ──────────────────────────────────");
    var prod = client.DuplicateProject(
        project: dev.Slug,
        name: $"Prod {run}",
        slug: $"prod-{run}",
        environment: "prod");
    createdProjectIds.Add(prod.Id);
    Console.WriteLine($"  duplicated '{dev.Slug}' → '{prod.Slug}' (env={prod.Environment})");
    var prodDetail = client.GetProject(project: prod.Slug);
    Console.WriteLine(
        $"  duplicate status = {prodDetail.Status}  (config only — patients/logs/state are never copied)");

    // ── 5. Rename / retag ─────────────────────────────────────────────────────
    Console.WriteLine("\n── 5. Rename / retag project ───────────────────────────────────────────");
    var renamed = client.RenameProject(
        project: dev.Id,
        name: $"Dev Sandbox {run} (renamed)",
        description: "Renamed by the example");
    Console.WriteLine($"  name = '{renamed.Name}'");

    // ── 6. Deprecate (soft-delete) ────────────────────────────────────────────
    Console.WriteLine("\n── 6. Deprecate project ────────────────────────────────────────────────");
    var deprecated = client.DeprecateProject(project: prod.Id);
    Console.WriteLine($"  '{prod.Slug}' status = {deprecated.Status}");

    // ── 7. Restore ────────────────────────────────────────────────────────────
    Console.WriteLine("\n── 7. Restore project ──────────────────────────────────────────────────");
    var restored = client.RestoreProject(project: prod.Id);
    Console.WriteLine($"  '{prod.Slug}' status = {restored.Status}");

    // ── 8. Permanent delete (irreversible) ────────────────────────────────────
    Console.WriteLine("\n── 8. Permanently delete a project ─────────────────────────────────────");
    client.DeprecateProject(project: prod.Id); // must be deprecated before permanent delete
    client.DeleteProject(project: prod.Id);
    createdProjectIds.Remove(prod.Id);
    Console.WriteLine($"  permanently deleted '{prod.Slug}'");
}
finally
{
    // Best-effort cleanup (demo-only — remove when adapting this code).
    Console.WriteLine("\n── Cleanup ─────────────────────────────────────────────────────────────");
    foreach (var (scoped, patientId) in createdPatients)
    {
        try
        {
            scoped.DeletePatient(patientId: patientId);
        }
        catch (Exception e)
        {
            Console.WriteLine($"  ! could not delete patient {patientId}: {e.Message}");
        }
    }

    foreach (var projectId in createdProjectIds)
    {
        try
        {
            client.DeprecateProject(project: projectId);
            client.DeleteProject(project: projectId);
            Console.WriteLine($"  cleaned up project {projectId}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"  ! could not delete project {projectId}: {e.Message}");
        }
    }

    devClient?.Dispose();
}

Console.WriteLine("\nDone.");
