/*
 * Olira SDK — Passive signal ingestion
 *
 * Upload a tiny accelerometer batch and wait for absorb to finish.
 *
 * Requirements:
 *   - copy .env.example → .env and fill in OLIRA_API_KEY (sdk:event-log)
 *
 * Run: dotnet run --project examples/11_Signals
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

var patient = client.CreatePatient(
    firstName: "Signal",
    lastName: "Demo",
    dateOfBirth: "1990-01-01T00:00:00Z",
    timezone: "America/New_York");
Console.WriteLine($"Patient created: {patient.Id}");

try
{
    var t0 = DateTimeOffset.UtcNow.AddHours(-1);
    // Truncate to whole seconds to match the Python example's replace(microsecond=0).
    t0 = new DateTimeOffset(t0.Year, t0.Month, t0.Day, t0.Hour, t0.Minute, t0.Second, TimeSpan.Zero);

    var records = new List<Dictionary<string, object?>>();
    for (var i = 0; i < 20; i++)
    {
        records.Add(new Dictionary<string, object?>
        {
            ["ts"] = t0.AddMilliseconds(i * 50),
            ["x"] = 0.0,
            ["y"] = 0.0,
            ["z"] = 9.81,
        });
    }

    try
    {
        var handle = client.SendSignals(
            patientId: patient.Id,
            sensorType: "accelerometer",
            sourceDevice: "example-phone-imu",
            sampleRateHz: 20.0,
            records: records);
        Console.WriteLine($"Job accepted: {handle.JobId}");

        var job = handle.Wait(timeout: 120.0);
        Console.WriteLine(
            $"status={job.Status} written={job.RecordsWritten} " +
            $"deduped={job.RecordsDeduplicated} quarantined={job.RecordsQuarantined}");
    }
    catch (ServerError ex) when (ex.Message.Contains("500", StringComparison.Ordinal))
    {
        // Local app-api without Timescale returns 500 after a valid Parquet upload.
        Console.WriteLine($"Signal upload reached the API but absorb failed: {ex.Message}");
        Console.WriteLine("  (Parquet serialization succeeded — check Timescale / signals backend availability.)");
    }
}
finally
{
    // ── Demo cleanup ─────────────────────────────────────────────────────────
    client.DeletePatient(patientId: patient.Id);
}

Console.WriteLine("Done.");
