using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Olira;
using Olira.Examples;

/*
 * Olira SDK — EHR Integrations
 *
 * Olira connects to a growing pool of EHR and clinical-data providers — Epic,
 * Healthie, Vivlio, and more (browse them with GET /v1/integrations/catalog).
 * Every provider follows the same pattern shown here: connect → probe →
 * subscribe data points → sync → write back. This walkthrough focuses on Epic;
 * swap the integration_type and credential fields for any other provider.
 *
 *   A. Manage integrations via the /v1/integrations REST routes — browse the
 *      catalog, connect an instance, watch the connection check, subscribe
 *      data points, trigger syncs, look up a patient's EHR-side id, rename.
 *   B. Write back into the EHR from the log APIs — writeBack=true on Log() and
 *      LogSpec/LogBatch(), with writeBackIntegrationId targeting a specific
 *      instance.
 *
 * Typed C# wrappers for the management routes are planned; until then they
 * are plain REST calls (this script uses HttpClient).
 *
 * Part A needs real provider credentials to get past the connection check.
 * Set EPIC_CLIENT_ID / EPIC_TOKEN_ENDPOINT / EPIC_FHIR_BASE_URL in .env, or
 * run against a sandbox. Without them the script skips ahead to Part B.
 *
 * Requires: sdk:integrations (management) + sdk:event-log & sdk:integration-write
 *           (write-back) + api:manage-patients (patient setup)
 * Run: dotnet run --project 09_EhrIntegrations
 */

ExampleEnv.Load();
var apiKey = ExampleEnv.Require("OLIRA_API_KEY");
var baseUrl = ExampleEnv.BaseUrl.TrimEnd('/');

using var api = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
api.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

string? integrationId = null;

// ═════════════════════════════════════════════════════════════════════════════
// Part A — Integration management (raw REST, sdk:integrations scope)
// ═════════════════════════════════════════════════════════════════════════════

// ── A1. Browse the provider catalog ──────────────────────────────────────────
var catalogDoc = await GetJsonAsync(api, $"{baseUrl}/v1/integrations/catalog");
var catalog = catalogDoc.GetProperty("data");
Console.WriteLine("Available providers:");
foreach (var entry in catalog.EnumerateArray())
{
    var type = entry.GetProperty("integration_type").GetString();
    var name = entry.GetProperty("name").GetString();
    var authMode = entry.GetProperty("auth_mode").GetString();
    Console.WriteLine($"  {type,-10} {name} ({authMode})");
}

// ── A2. List existing connections ────────────────────────────────────────────
var integrationsDoc = await GetJsonAsync(api, $"{baseUrl}/v1/integrations");
var integrations = integrationsDoc.GetProperty("data");
Console.WriteLine($"\nConnected integrations: {integrations.GetArrayLength()}");
foreach (var i in integrations.EnumerateArray())
{
    var id = i.GetProperty("id").GetString();
    var displayName = i.TryGetProperty("display_name", out var dn) && dn.ValueKind != JsonValueKind.Null
        ? dn.GetString()
        : i.GetProperty("integration_type").GetString();
    var status = i.GetProperty("status").GetString();
    var connection = i.TryGetProperty("connection_status", out var cs) ? cs.ToString() : "";
    Console.WriteLine($"  {id}  {displayName}  status={status} connection={connection}");
}

// ── A3. Connect an Epic instance (M2M — three non-secret values) ─────────────
var epicEnvVars = new[] { "EPIC_CLIENT_ID", "EPIC_TOKEN_ENDPOINT", "EPIC_FHIR_BASE_URL" };
var missing = epicEnvVars.Where(name => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))).ToList();
if (missing.Count > 0)
{
    Console.WriteLine($"\n{string.Join(", ", missing)} not set — skipping connect/subscribe, jumping to write-back.");
}
else
{
    var connectBody = new Dictionary<string, object?>
    {
        ["integration_type"] = "epic",
        // Cosmetic instance label — distinguishes this connection from other
        // Epic instances your org may add later. Renameable via PATCH.
        ["display_name"] = "Epic — Example Hospital",
        ["auth_mode"] = "m2m",
        ["credentials"] = new Dictionary<string, object?>
        {
            ["type"] = "m2m_jwt",
            ["client_id"] = Environment.GetEnvironmentVariable("EPIC_CLIENT_ID"),
            ["token_endpoint"] = Environment.GetEnvironmentVariable("EPIC_TOKEN_ENDPOINT"),
            ["api_base_url"] = Environment.GetEnvironmentVariable("EPIC_FHIR_BASE_URL"),
        },
    };

    using var connectResp = await PostJsonAsync(api, $"{baseUrl}/v1/integrations", connectBody);
    var connectText = await connectResp.Content.ReadAsStringAsync();
    using var connectDoc = JsonDocument.Parse(connectText);

    if ((int)connectResp.StatusCode == 409)
    {
        // This exact provider instance (same FHIR base URL) is already connected.
        var detail = connectDoc.RootElement.TryGetProperty("detail", out var d)
            ? d.ToString()
            : connectText;
        Console.WriteLine($"\nAlready connected: {detail}");
    }
    else
    {
        connectResp.EnsureSuccessStatusCode();
        var integration = connectDoc.RootElement.GetProperty("data");
        integrationId = integration.GetProperty("id").GetString();
        var displayName = integration.GetProperty("display_name").GetString();
        var status = integration.GetProperty("status").GetString();
        Console.WriteLine($"\nConnected: {integrationId} ({displayName}) status={status}");

        // ── A4. Wait for the async connection probe ──────────────────────────
        JsonElement doc = integration;
        for (var _ = 0; _ < 12; _++)
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            var probeDoc = await GetJsonAsync(api, $"{baseUrl}/v1/integrations/{integrationId}");
            doc = probeDoc.GetProperty("data").Clone();
            var connStatus = doc.TryGetProperty("connection_status", out var cs) ? cs.GetString() : null;
            if (connStatus is "valid" or "invalid")
                break;
        }

        var finalStatus = doc.TryGetProperty("connection_status", out var fcs) ? fcs.GetString() : null;
        var errorReason = doc.TryGetProperty("error_reason", out var er) && er.ValueKind != JsonValueKind.Null
            ? er.ToString()
            : null;
        Console.WriteLine(
            $"Connection probe: {finalStatus}"
            + (errorReason is not null ? $" — {errorReason}" : ""));

        // ── A5. Data points ───────────────────────────────────────────────────
        // The catalog below reflects what YOUR connected Epic app is entitled
        // to (its approved scopes/tier) — other orgs may see a different list.
        var dpCatalogDoc = await GetJsonAsync(
            api, $"{baseUrl}/v1/integrations/{integrationId}/data-points/catalog");
        var dpCatalog = dpCatalogDoc.GetProperty("data");
        var dpNames = dpCatalog.EnumerateArray()
            .Select(d => d.GetProperty("name").GetString())
            .ToList();
        Console.WriteLine($"\nData points available to your Epic app: [{string.Join(", ", dpNames)}]");

        using var subResp = await PostJsonAsync(
            api,
            $"{baseUrl}/v1/integrations/{integrationId}/data-points",
            new Dictionary<string, object?> { ["name"] = "Patients" }); // roster sync — subscribe this first, always
        var subText = await subResp.Content.ReadAsStringAsync();
        using var subDoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(subText) ? "{}" : subText);

        if ((int)subResp.StatusCode == 422)
        {
            var detail = subDoc.RootElement.TryGetProperty("detail", out var d)
                ? d.ToString()
                : subText;
            Console.WriteLine(
                $"Subscribe rejected (Olira has not activated the integration yet): {detail}");
        }
        else
        {
            subResp.EnsureSuccessStatusCode();
            var dp = subDoc.RootElement.GetProperty("data");
            var dpName = dp.GetProperty("name").GetString();
            var dpId = dp.GetProperty("id").GetString();
            Console.WriteLine($"Subscribed: {dpName} ({dpId})");

            // Trigger an immediate sync instead of waiting for the scheduler tick.
            using var syncResp = await api.PostAsync(
                $"{baseUrl}/v1/integrations/{integrationId}/data-points/{dpId}/sync",
                content: null);
            var syncText = await syncResp.Content.ReadAsStringAsync();
            using var syncDoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(syncText) ? "{}" : syncText);
            var syncInfo = (int)syncResp.StatusCode == 202
                ? (syncDoc.RootElement.TryGetProperty("workflow_id", out var wid) ? wid.ToString() : syncText)
                : (syncDoc.RootElement.TryGetProperty("detail", out var det) ? det.ToString() : syncText);
            Console.WriteLine($"Sync now → {(int)syncResp.StatusCode} ({syncInfo})");

            // Poll subscription status / last sync summary.
            await Task.Delay(TimeSpan.FromSeconds(10));
            var pointsDoc = await GetJsonAsync(api, $"{baseUrl}/v1/integrations/{integrationId}/data-points");
            foreach (var p in pointsDoc.GetProperty("data").EnumerateArray())
            {
                var pName = p.GetProperty("name").GetString();
                var pStatus = p.GetProperty("status").GetString();
                var summary = p.TryGetProperty("last_sync_summary", out var lss) ? lss.ToString() : "";
                Console.WriteLine($"  {pName}: status={pStatus} summary={summary}");
            }
        }

        // ── A6. Per-instance patient lookup ───────────────────────────────────
        // After a Patients sync, resolve an Olira patient's EHR-side id AT THIS
        // instance (404 = this instance doesn't know the patient; others might):
        //   GET {baseUrl}/v1/integrations/{integrationId}/patients/{oliraPatientId}
        //   → {"system": "epic", "external_id": "<FHIR Patient id>", ...}

        // ── A7. Rename (PATCH also updates credentials / endpoint) ───────────
        using var patchContent = new StringContent(
            """{"display_name":"Epic — Hospital A"}""",
            Encoding.UTF8,
            "application/json");
        using var patchResp = await api.PatchAsync(
            $"{baseUrl}/v1/integrations/{integrationId}",
            patchContent);
        patchResp.EnsureSuccessStatusCode();
        Console.WriteLine("Renamed instance to 'Epic — Hospital A'");
        // Disconnect cascades data points and cancels in-flight syncs:
        // await api.DeleteAsync($"{baseUrl}/v1/integrations/{integrationId}");
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// Part B — Write-back from the log APIs (sdk:event-log + sdk:integration-write)
// ═════════════════════════════════════════════════════════════════════════════

using var client = new OliraClient(
    apiKey: apiKey,
    baseUrl: baseUrl,
    environment: ExampleEnv.EnvForBaseUrl(baseUrl));

// In real use, write-back targets patients the EHR knows: synced from its
// roster (Part A) or chart-linked via Patient.Create write-back.
var patient = client.CreatePatient(firstName: "Writeback", lastName: "Demo", timezone: "UTC");
var pid = patient.Id;
Console.WriteLine($"\nDemo patient: {pid}");

// ── B1. writeBack on Log() — background queue ───────────────────────────────
// The vitals reading ingests into Olira normally AND is requested for
// write-back into the EHR (composed by the platform as a FHIR Observation).
client.Log(
    logType: OliraLogType.VitalsMeasurement,
    patientId: pid,
    payload: new Dictionary<string, object?>
    {
        ["measurements"] = new Dictionary<string, object?>
        {
            ["weight_kg"] = 72.5,
            ["systolic_bp_mmhg"] = 118,
            ["diastolic_bp_mmhg"] = 76,
        },
        ["collection_datetime"] = "2026-07-10T08:00:00Z",
    },
    writeBack: true,
    // null → platform infers the target (single write-configured integration,
    // else the patient's instance-linked identifiers). Pass the id explicitly
    // when several instances of the same type are write-configured.
    writeBackIntegrationId: integrationId);
client.Flush();
Console.WriteLine("Queued vitals log with writeBack=true");

// ── B2. writeBack on LogBatch() — per-event control ────────────────────────
var batchResult = client.LogBatch(
[
    new LogSpec(
        logType: OliraLogType.VitalsMeasurement,
        patientId: pid,
        payload: new Dictionary<string, object?>
        {
            ["measurements"] = new Dictionary<string, object?> { ["spo2_percent"] = 97 },
            ["collection_datetime"] = "2026-07-10T09:00:00Z",
        },
        writeBack: true,
        writeBackIntegrationId: integrationId),
    new LogSpec( // ingest-only — no write-back requested
        logType: OliraLogType.UserLogin,
        patientId: pid),
]);
Console.WriteLine($"Batch: accepted={batchResult.Accepted} failed={batchResult.Failed}");
Console.WriteLine(
    "Note: the response never reveals whether a write-back fired — verify in the " +
    "EHR or the Olira Console's write-requests view (platform admins).");

static async Task<JsonElement> GetJsonAsync(HttpClient http, string url)
{
    using var resp = await http.GetAsync(url);
    resp.EnsureSuccessStatusCode();
    await using var stream = await resp.Content.ReadAsStreamAsync();
    using var doc = await JsonDocument.ParseAsync(stream);
    return doc.RootElement.Clone();
}

static async Task<HttpResponseMessage> PostJsonAsync(HttpClient http, string url, object body)
{
    var json = JsonSerializer.Serialize(body);
    using var content = new StringContent(json, Encoding.UTF8, "application/json");
    return await http.PostAsync(url, content);
}
