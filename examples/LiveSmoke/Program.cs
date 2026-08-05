using Olira;
using Olira.Examples;

static string? Env(string k) => Environment.GetEnvironmentVariable(k);

// Prefer /tmp smoke env (CI), else examples/.env via ExampleEnv.
var envFile = "/tmp/olira-csharp-smoke.env";
if (File.Exists(envFile))
{
    foreach (var line in File.ReadAllLines(envFile))
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
        var i = line.IndexOf('=');
        if (i <= 0) continue;
        Environment.SetEnvironmentVariable(line[..i].Trim(), line[(i + 1)..].Trim());
    }
}
else
{
    ExampleEnv.Load();
}

var apiKey = Env("OLIRA_API_KEY") ?? throw new InvalidOperationException("OLIRA_API_KEY required (set in /tmp/olira-csharp-smoke.env or examples/.env)");
var baseUrl = Env("OLIRA_BASE_URL") ?? "http://localhost:8080/app-api";

var pass = 0;
var fail = 0;
string? patientId = null;
string? cohortId = null;

void Ok(string name, string detail = "")
{
    pass++;
    Console.WriteLine($"PASS  {name}{(detail.Length > 0 ? " — " + detail : "")}");
}

void Fail(string name, string detail)
{
    fail++;
    Console.WriteLine($"FAIL  {name} — {detail}");
}

void FailEx(string name, Exception ex) => Fail(name, $"{ex.GetType().Name}: {ex.Message}");

Console.WriteLine($"Live smoke against {baseUrl}");
Console.WriteLine($"Key prefix: {apiKey[..Math.Min(18, apiKey.Length)]}…");

using var client = new OliraClient(
    apiKey: apiKey,
    baseUrl: baseUrl,
    environment: OliraEnv.Development,
    asyncFlush: false,
    timeout: 30,
    serviceName: "csharp-live-smoke");

try
{
    var listed = client.ListPatients(limit: 3);
    patientId = listed.Patients.FirstOrDefault()?.Id;
    Ok("ListPatients", $"total={listed.Total} sample={patientId}");
}
catch (Exception ex) { FailEx("ListPatients", ex); }

try
{
    var created = client.CreatePatient(
        firstName: "CsharpSmoke",
        lastName: $"Test{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
        timezone: "America/New_York",
        externalIdentifiers:
        [
            new ExternalIdentifier { System = "csharp-smoke", Value = $"cs-{Guid.NewGuid():N}"[..20] },
        ]);
    patientId = created.Id;
    Ok("CreatePatient", $"id={created.Id}");
}
catch (Exception ex) { FailEx("CreatePatient", ex); }

if (patientId is not null)
{
    try
    {
        var p = client.GetPatient(patientId);
        Ok("GetPatient", $"{p.FirstName} {p.LastName}");
    }
    catch (Exception ex) { FailEx("GetPatient", ex); }

    try
    {
        var p = client.UpdatePatient(patientId, diseaseStage: "II");
        Ok("UpdatePatient", $"stage={p.DiseaseStage}");
    }
    catch (Exception ex) { FailEx("UpdatePatient", ex); }

    try
    {
        var result = client.LogBatch(
        [
            new LogSpec(
                OliraLogType.SymptomReport,
                patientId,
                payload: new Dictionary<string, object?>
                {
                    ["instrument"] = "esas_r",
                    ["symptoms"] = new object[]
                    {
                        new Dictionary<string, object?> { ["name"] = "pain", ["score"] = 3 },
                    },
                },
                timestamp: DateTime.UtcNow.ToString("o")),
        ]);
        if (result.Accepted >= 1 && result.Failed == 0)
            Ok("LogBatch", $"accepted={result.Accepted}");
        else
            Fail("LogBatch", $"accepted={result.Accepted} failed={result.Failed} errors={string.Join("; ", result.Errors.Select(e => e.Message))}");
    }
    catch (Exception ex) { FailEx("LogBatch", ex); }

    try
    {
        var logs = client.GetLogs(patientId, limit: 5);
        Ok("GetLogs", $"count={logs.Count}");
    }
    catch (Exception ex) { FailEx("GetLogs", ex); }

    try
    {
        var q = client.Logs(patientId).Eq("type", OliraLogType.SymptomReport).Limit(5).Execute();
        Ok("LogQuery.Execute", $"count={q.Count} rows={q.Rows.Count}");
    }
    catch (Exception ex) { FailEx("LogQuery.Execute", ex); }

    try
    {
        var views = client.ListViews(patientId);
        Ok("ListViews", $"n={views.Count}");
    }
    catch (Exception ex) { FailEx("ListViews", ex); }

    try
    {
        var stable = client.GetStableData(patientId);
        Ok("GetStableData", $"modules={stable.Modules.Count}");
    }
    catch (Exception ex) { FailEx("GetStableData", ex); }

    try
    {
        var tok = client.GetPatientToken(patientId);
        Ok("GetPatientToken", $"expires_in={tok.ExpiresIn} type={tok.TokenType}");
    }
    catch (Exception ex) { FailEx("GetPatientToken", ex); }
}

try
{
    var cohorts = client.ListCohorts();
    Ok("ListCohorts", $"n={cohorts.Data.Count}");
}
catch (Exception ex) { FailEx("ListCohorts", ex); }

try
{
    var cohort = client.CreateCohort(
        name: $"csharp-smoke-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
        description: "temp");
    cohortId = cohort.Id;
    Ok("CreateCohort", $"id={cohort.Id}");
    if (patientId is not null)
    {
        var mut = client.AddPatientsToCohort(cohort.Id, [patientId]);
        Ok("AddPatientsToCohort", $"patient_count={mut.PatientCount}");
    }
}
catch (Exception ex) { FailEx("CreateCohort/Add", ex); }

try
{
    var schemas = client.ListSchemas();
    Ok("ListSchemas", $"n={schemas.Count}");
}
catch (Exception ex) { FailEx("ListSchemas", ex); }

try
{
    var jobs = client.ListIngestionJobs(pageSize: 2);
    Ok("ListIngestionJobs", $"total={jobs.Total} n={jobs.Jobs.Count}");
}
catch (Exception ex) { FailEx("ListIngestionJobs", ex); }

if (cohortId is not null)
{
    try
    {
        client.DeleteCohort(cohortId);
        Ok("DeleteCohort", cohortId);
    }
    catch (Exception ex) { FailEx("DeleteCohort", ex); }
}

if (patientId is not null)
{
    try
    {
        client.DeletePatient(patientId);
        Ok("DeletePatient", patientId);
    }
    catch (Exception ex) { FailEx("DeletePatient", ex); }
}

Console.WriteLine();
Console.WriteLine($"Done: {pass} passed, {fail} failed");
Environment.Exit(fail == 0 ? 0 : 1);
