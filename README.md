# Olira .NET SDK

Log ingestion, patient management, cohort management, org schema management, log-type catalog discovery, historical backfill, and patient state client for the Olira platform.

## Install

```bash
dotnet add package Olira
```

## Documentation

Full API reference: [https://docs.olira.ai/reference/sdk](https://docs.olira.ai/reference/sdk).

This package targets **.NET 8** and mirrors the Python `olira` SDK (API parity with **1.15.0**).

**Async note:** .NET `*Async` methods share the sync client's background worker (`onError`, queue-full notification). Python's `AsyncOliraClient` has no `on_error` and silently drops when its queue is full; it also lacks document/signal APIs that are available on `OliraClient.*Async` here.

---

## Authentication

All SDK methods authenticate with an **Olira API key** (`olira_prod_...`). Create keys from the Olira Console under **Settings → API Keys**, selecting the scopes you need:

| Scope                     | What it unlocks                                        |
| ------------------------- | ------------------------------------------------------ |
| `sdk:event-log`           | Log events                                             |
| `api:manage-patients`     | Create, read, update, delete patients and cohorts      |
| `sdk:patient-token`       | Mint short-lived patient-scoped JWTs                   |
| `sdk:historical-ingest`   | Create and manage historical data ingestion jobs       |
| `sdk:state-read`          | Read Patient State (modules, views, logs, memories)    |
| `api:org-config`          | Register, view, check, edit, deprecate, and activate org-native event schemas/mappings |
| `sdk:actions`             | Manage outbound-actions destinations and read the delivery ledger |
| `mcp:patient-state`       | Query Patient State via the MCP Patient State server   |

See [API key scopes](https://docs.olira.ai/cli/scopes) for the full list.

Pass the key to `OliraClient` or to `OliraModule.Init()`:

```csharp
using Olira;

OliraModule.Init(apiKey: "olira_prod_..."); // or set OLIRA_API_KEY
// Prefer OliraClient directly for multi-key / DI scenarios:
using var client = new OliraClient(apiKey: "olira_prod_...");
```

---

## Patient Management

Patients must exist before you can log events against them. Use the `api:manage-patients` scope.

Olira assigns a stable `Id` to each patient at creation time. The `Id` returned on the `Patient` object is what you use in all subsequent calls.

```csharp
using Olira;

using var client = new OliraClient(apiKey: "olira_prod_...");

// Create — Olira assigns the id; store it for future calls
var patient = client.CreatePatient(
    firstName: "Jane",
    lastName: "Smith",
    timezone: "America/New_York",
    primaryDiseaseSite: "breast",
    diseaseStage: "II");
var patientId = patient.Id;

// Get
patient = client.GetPatient(patientId);

// List (paginated)
var result = client.ListPatients(limit: 50, offset: 0);
foreach (var p in result.Patients)
{
    Console.WriteLine($"{p.Id} {p.FirstName} {p.LastName}");
}

// Update (only supplied fields are changed)
patient = client.UpdatePatient(patientId, diseaseStage: "III");

// Soft-delete
client.DeletePatient(patientId);
```

---

## Cohort Management

Group patients into named cohorts and assign summary templates to them. Requires the `api:manage-patients` scope.

```csharp
using Olira;

using var client = new OliraClient(apiKey: "olira_prod_...");

// Create a cohort
var cohort = client.CreateCohort(name: "High-Risk Patients", description: "Weekly review");
var cohortId = cohort.Id;

// Enrol patients (up to 500 per call, idempotent)
client.AddPatientsToCohort(cohortId, [patientId]);

// Assign a summary type — patients in the cohort get this template
client.AssignCohortTemplate(cohortId, summaryType: "symptom_overview");

// List, get, update
var result = client.ListCohorts();
cohort = client.GetCohort(cohortId);
client.UpdateCohort(cohortId, description: "Updated description");

// Remove patients / unassign template
client.RemovePatientsFromCohort(cohortId, [patientId]);
client.UnassignCohortTemplate(cohortId, summaryType: "symptom_overview");

// Delete (cascades template assignments; patient records are unaffected)
client.DeleteCohort(cohortId);
```

---

## Org Schema Management

Register your own event subtypes (e.g. `myorg_widget_reading`) and their mapping into Olira's platform catalog, self-service. Requires the `api:org-config` scope.

Registering always lands as a **pending request** — Olira reviews and materializes the actual schema + mapping before it can be activated.

```csharp
using Olira;

using var client = new OliraClient(apiKey: "olira_prod_...");

// Register a new subtype — examples + description only ("assisted": Olira authors the schema/mapping)
var registration = client.RegisterSchema(
    subtype: "widget_ping",
    description: "Widget sensor ping events",
    inputExamples:
    [
        new Dictionary<string, object?> { ["reading_value"] = 42, ["unit"] = "lux" },
    ]);
Console.WriteLine(registration.Status); // "pending_review"

// Check status any time
var detail = client.GetSchema(subtype: "widget_ping");
Console.WriteLine(detail.Status); // "pending" until Olira materializes + activates a version

// Dry-run a candidate schema+mapping before registering it at all — no writes
var result = client.CheckSchema(
    examples: [new Dictionary<string, object?> { ["reading_value"] = 42, ["unit"] = "lux" }],
    schema: new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["required"] = new List<object> { "reading_value" },
        ["properties"] = new Dictionary<string, object?>
        {
            ["reading_value"] = new Dictionary<string, object?> { ["type"] = "number" },
        },
    },
    mapping: new Dictionary<string, object?>
    {
        ["targets"] = new List<object>
        {
            new Dictionary<string, object?>
            {
                ["target_subtype"] = "heart_rate_data",
                ["field_mappings"] = new List<object>
                {
                    new Dictionary<string, object?>
                    {
                        ["target"] = "avg_bpm",
                        ["source"] = "reading_value",
                    },
                },
            },
        },
    });
Console.WriteLine(result.Ok);

// Once Olira has activated a version, log against it like any other event type
client.LogBatch(
[
    new LogSpec(
        logType: "widget_ping",
        patientId: patientId,
        payload: new Dictionary<string, object?> { ["reading_value"] = 42, ["unit"] = "lux" }),
]);

// Propose a change — always opens a new pending version, never mutates the active one
client.EditSchema(subtype: "widget_ping", description: "Updated description");

// List everything you've registered
foreach (var summary in client.ListSchemas())
{
    Console.WriteLine($"{summary.Subtype} {summary.Status} {summary.ActiveVersion}");
}

// Roll back to (or promote) an already-materialized version
client.ActivateSchemaVersion(subtype: "widget_ping", version: 1);

// Deprecate a version (or withdraw a still-pending request) — never a hard delete
client.DeprecateSchema(subtype: "widget_ping");
```

---

## Outbound Actions

Get notified when something happens on the platform: a patient's data updated, a log arrived that changed nothing, a mapping error, an ingestion job finished, or an integration failed to sync. Register a **destination** (a signed HTTPS webhook, or an email) and subscribe it to the triggers you care about. Requires the `sdk:actions` scope.

```csharp
using Olira;

using var client = new OliraClient(apiKey: "olira_prod_...");

// Register a destination (the signing secret is shown once, store it now)
var destination = client.CreateActionDestination(
    webhookConfig: new WebhookDestinationConfig { Url = "https://hooks.example.com/olira" },
    subscribedTriggers: [ActionTrigger.PatientStateChanged, ActionTrigger.IngestionFailed]);
Console.WriteLine(destination.SigningSecret);

// Inspect the delivery ledger
var deliveries = client.ListActionDeliveries(destinationId: destination.Id, status: "delivered");
foreach (var d in deliveries.Data)
{
    Console.WriteLine($"{d.Id} {d.Trigger} {d.Status}");
}

// Resend a delivery: the same body as the original, not a newly generated one
if (deliveries.Data.Count > 0)
{
    client.RedeliverActionDelivery(deliveries.Data[0].Id);
}

// Rotate the signing secret (old one stays valid 24h for dual-signing)
client.RotateActionDestinationSecret(destination.Id);
```

`ActionTrigger` lists the currently available triggers as string constants (for autocomplete); a plain string still works everywhere too (nothing validates it client-side, so a typo'd string still reaches the server as a 422). `PatientStateChanged` is frequent enough that `ActionTrigger.RecommendedDigestTriggers` flags it as a candidate for daily batching instead of one delivery per event; pass `DigestSchedule` to `CreateActionDestination`/`UpdateActionDestination` to opt in. A destination subscribed to `ActionTrigger.All` (`"*"`) could start receiving additional trigger types later, since that value is evaluated by the platform rather than by this list.

### Delivery payload

Your webhook endpoint receives a fixed envelope:

```json
{ "id": "del_123", "type": "patient.state.changed", "created": "2026-08-12T09:14:05Z", "api_version": "2026-08-01", "data": { "...": "..." } }
```

`type` carries the trigger you subscribed with; it's `Trigger` on `ActionDelivery` (the ledger record this SDK reads back) and `type` in the payload itself.

### Verifying the signature

Every delivery carries an `Olira-Signature` header: `t=<unix_ts>,v1=<hex_hmac>`. Recompute it with your destination's signing secret and compare; this proves the request came from Olira and wasn't altered in transit:

```csharp
using System.Security.Cryptography;
using System.Text;

static bool VerifySignature(string secret, string header, byte[] rawBody, int maxSkewSeconds = 300)
{
    var parts = header.Split(',');
    var tPart = parts.FirstOrDefault(p => p.StartsWith("t="));
    if (tPart is null || !long.TryParse(tPart.Substring(2), out var timestamp))
        return false;
    if (Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - timestamp) > maxSkewSeconds)
        return false;

    var signatures = parts.Where(p => p.StartsWith("v1=")).Select(p => p.Substring(3));

    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var signedPayload = Encoding.UTF8.GetBytes($"{timestamp}.").Concat(rawBody).ToArray();
    var expected = Convert.ToHexString(hmac.ComputeHash(signedPayload)).ToLowerInvariant();

    return signatures.Any(sig => CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(sig)));
}
```

During secret rotation the header carries **two** `v1=` entries; check if *any* matches, don't assume there's exactly one. The timestamp is fresh on every attempt (including retries); reject a missing/malformed timestamp, one too far in the past (replay), or one unreasonably far in the future (clock skew or forgery) before checking the signature at all.

### Digest batching is not fast

A destination with `DigestSchedule` set doesn't deliver a batched trigger right after it fires: it sits at `Status: "buffered"` until the destination's `TimeOfDay` next arrives in its `Timezone`, which can be close to a full day later. Don't poll `ListActionDeliveries` expecting a quick result the way you would for an immediate trigger.

---

## Log Types

`OliraLogType` is a static reference of log types shipped with this SDK version — accurate as of
release, but it can lag the platform if new log types ship between SDK releases. For agent-guided
mapping (matching your own data model to Olira's) or anything that needs the current,
authoritative catalog — including each type's full payload JSON Schema — call the live catalog
instead. Requires the `sdk:event-log` scope.

```csharp
using Olira;

using var client = new OliraClient(apiKey: "olira_prod_...");

// List every log type in the platform catalog
foreach (var lt in client.ListLogTypes().Data)
{
    Console.WriteLine($"{lt.Subtype} {lt.DisplayName}");
}

// Look up one type by subtype (or a known deprecated alias)
var moodReport = client.GetLogType(subtype: "mood_report");
Console.WriteLine(moodReport.PayloadSchema);
```

---

## Logging

Log a single event in the background (fire-and-forget):

```csharp
using Olira;

OliraModule.Init(apiKey: "olira_prod_...");

OliraModule.Log(
    logType: OliraLogType.UserLogin,
    patientId: patientId); // id from patient.Id
OliraModule.Flush(); // block until delivery
```

Send a batch directly and get back a result:

```csharp
using Olira;

using var client = new OliraClient(apiKey: "olira_prod_...");
var result = client.LogBatch(
[
    new LogSpec(OliraLogType.UserLogin, patientId),
    new LogSpec(
        OliraLogType.SymptomReport,
        patientId,
        payload: new Dictionary<string, object?>
        {
            ["instrument"] = "esas_r",
            ["symptoms"] = new List<object>
            {
                new Dictionary<string, object?> { ["name"] = "pain", ["score"] = 4 },
            },
        }),
]);
Console.WriteLine($"accepted={result.Accepted}, failed={result.Failed}");
```

If your source data is already in **FHIR R4 format**, use `LogFhir` — Olira maps the resource to the right log type automatically using the same absorber as Epic/Cerner integrations:

```csharp
using Olira;

using var client = new OliraClient(apiKey: "olira_prod_...");
try
{
    var result = client.LogFhir(
        patientId,
        resource: new Dictionary<string, object?>
        {
            ["resourceType"] = "Condition",
            ["clinicalStatus"] = new Dictionary<string, object?>
            {
                ["coding"] = new List<object>
                {
                    new Dictionary<string, object?> { ["code"] = "active" },
                },
            },
            ["code"] = new Dictionary<string, object?> { ["text"] = "Breast cancer" },
            ["subject"] = new Dictionary<string, object?>
            {
                ["reference"] = $"Patient/{patientId}",
            },
            ["onsetDateTime"] = "2025-01-10T00:00:00Z",
        });
    Console.WriteLine($"accepted={result.Accepted}");
}
catch (ValidationError e)
{
    Console.WriteLine($"Resource not mappable: {e.Message}");
}
```

Pass `idempotencyKey` if you might retry after a network error or 5xx. Send the same key you used the first time — one key per resource, not per mapped event. A treatment plan from an EHR can produce several Olira events; Olira records `your-key:clinical_plan_item` and `your-key:treatment_phase` internally so a retry does not duplicate either:

```csharp
var resource = new Dictionary<string, object?>
{
    ["resourceType"] = "Condition",
    ["code"] = new Dictionary<string, object?> { ["text"] = "Type 2 diabetes" },
    ["subject"] = new Dictionary<string, object?> { ["reference"] = $"Patient/{patientId}" },
};

// Safe to send this exact call again if the response is lost to a network error.
var result = client.LogFhir(patientId, resource, idempotencyKey: "condition-2026-01-10");
```

---

## Historical Ingestion

Backfill months or years of existing patient data. Requires the `sdk:historical-ingest` scope.

```csharp
using Olira;

using var client = new OliraClient(apiKey: "olira_prod_...");

// Submit a JSONL file — the SDK uploads it to S3 and creates the job
var job = client.CreateIngestionJob(
    file: "patients_and_logs.jsonl",
    idempotencyKey: "initial-onboarding-2026",
    requireConfirmation: true); // pause to review before replay

// Poll until awaiting confirmation (or complete)
while (job.Status is not ("completed" or "failed" or "awaiting_confirmation"))
{
    Thread.Sleep(TimeSpan.FromSeconds(5));
    job = client.GetIngestionJob(job.JobId);
    Console.WriteLine($"{job.Stage}  {job.ProgressPct:0.0}%");
}

// Review, then confirm to trigger graph replay and view backfill
Console.WriteLine($"Patients: {job.PatientsProcessed}  Logs: {job.LogsProcessed}");
client.ConfirmIngestionJob(job.JobId);
```

See the [Backfilling historical data](https://docs.olira.ai/send-data/historical-backfill) guide for the full walkthrough including inline payloads, validation, cancellation, and error recovery.

---

## Batch export

Compile selected patients into a zip of typed Parquets (logs, state modules, view blocks, events, extracted). Requires the `sdk:state-read` scope. Provide exactly one of `patientIds`, `cohortId`, or `scope: "project"`.

```csharp
using Olira;

using var client = new OliraClient(apiKey: "olira_prod_...");
var patientId = "patient_123";

var job = client.CreateExport(
    start: DateTimeOffset.UtcNow.AddDays(-30),
    end: DateTimeOffset.UtcNow,
    include: new ExportInclude
    {
        Logs = true,
        StateModules = true,
        ViewBlocks = true,
        Events = true,
        Extracted = true,
    },
    patientIds: [patientId]);

while (job.Status is not (
    ExportJobStatus.Completed or
    ExportJobStatus.CompletedWithErrors or
    ExportJobStatus.Failed or
    ExportJobStatus.Cancelled))
{
    Thread.Sleep(TimeSpan.FromSeconds(3));
    job = client.GetExport(job.ExportId);
    Console.WriteLine($"{job.Stage} {job.ProgressPct:0.0}%");
}

if (job.Downloadable)
{
    var dl = client.DownloadExport(job.ExportId);
    Console.WriteLine(dl.DownloadUrl);
}
```

---

## Patient Token

Mint a short-lived JWT scoped to a single patient. Requires the `sdk:patient-token` scope.

Use this when a patient device needs to communicate with the [Olira MCP Patient State server](https://docs.olira.ai/mcp-server) — pass the token as a Bearer header. The token expires after 15 minutes and is locked to the specified patient.

```csharp
using Olira;

using var client = new OliraClient(apiKey: "olira_prod_...");
var token = client.GetPatientToken(patientId);

Console.WriteLine(token.AccessToken); // forward this to the patient device
Console.WriteLine(token.ExpiresIn);   // 900 (seconds)
```

---

## Patient State

Read structured Patient State (modules, views, logs, and memories). Requires the `sdk:state-read` scope.

```csharp
using Olira;

using var client = new OliraClient(apiKey: "olira_prod_...");

// Stable profile data (demographics, medications, care team, etc.)
var stable = client.GetStableData(patientId);

// Event-driven module (symptoms, labs, vitals, etc.)
var module = client.GetEventStateModule(patientId, moduleType: "symptoms");

// Generated view (template-driven summary your agents consume)
var view = client.GetView(patientId, viewType: "weekly_health_summary");

// Memories
var memories = client.ReadMemories(patientId);
```

---

## Async client

All methods are available as `*Async` coroutines on the same `OliraClient`:

```csharp
using Olira;

await using var client = new OliraClient(apiKey: "olira_prod_...");
var patient = await client.CreatePatientAsync(
    firstName: "Jane",
    lastName: "Smith");
await client.LogAsync(
    logType: OliraLogType.UserLogin,
    patientId: patient.Id);
await client.FlushAsync();
```

---

## Error handling

```csharp
using Olira;

try
{
    client.LogBatch([...]);
}
catch (AuthError)
{
    // Invalid or revoked API key, or missing scope
}
catch (ValidationError)
{
    // Bad request (400/404/422) — e.g. unknown event type or missing required field
}
catch (RateLimitError e)
{
    // Retry after e.RetryAfter seconds
}
catch (ServerError)
{
    // Transient server error after all retries exhausted
}
```

---

## Examples

Runnable console apps under `examples/` (see [`examples/README.md`](examples/README.md) for setup: `cp examples/.env.example examples/.env`, then `dotnet run --project examples/00_Quickstart`):

| Project | What it covers |
| ------- | -------------- |
| `00_Quickstart` | `OliraModule.Init()`, create a patient, log an event |
| `01_PatientManagement` | Create, get, list, update, delete patients |
| `02_EventLogging` | `Log()`, `LogBatch()`, traces, flush |
| `03_FhirIngestion` | `LogFhir()` with Condition, MedicationRequest, Appointment; error handling |
| `04_HistoricalIngestion` | File upload, polling, confirm/cancel flow |
| `05_LogsOnlyWorkflow` | Historical ingestion with log-only records when patients already exist |
| `06_ReadPatientState` | Stable data, event modules, views, logs, memories |
| `07_PatientToken` | Mint token, MCP Bearer forwarding |
| `08_CohortManagement` | Create cohorts, enrol patients, assign templates, full lifecycle |
| `09_OrgSchemaManagement` | Register, check, edit, list, view, and deprecate an org-native schema/mapping request |
| `09_EhrIntegrations` | Connected integrations (catalog, connect, probe, sync, write-back) |
| `10_ProjectManagement` | Project (workspace) lifecycle: create, select, duplicate, rename, deprecate, restore, delete |
| `11_Signals` | Passive accelerometer batch via `SendSignals`, wait for absorb |
| `12_DocumentResourceIngestion` | Live `UploadDocument()` / OCR poll + historical `CreateIngestionJob(documents: …)` |

`06_ReadPatientState` and `07_PatientToken` require a patient with existing data — run `00_Quickstart` or `02_EventLogging` first and use the printed patient id (or set `PATIENT_ID` in `.env`).

---

## Contributing

Requires the .NET 8 SDK (see `global.json`).

```bash
export PATH="$HOME/.dotnet:$PATH"
./scripts/pre-pr.sh   # version check + build + tests
# or: ./scripts/test.sh
```

CI runs `pre-pr.sh` on every PR to `main` (version bump required). Publishing to NuGet runs from `main` via `.github/workflows/publish.yml`.
