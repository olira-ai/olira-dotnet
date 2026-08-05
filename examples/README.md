# Olira SDK — C# Examples

Runnable .NET console apps that demonstrate the SDK's main workflows. Each project is self-contained and can be run after setup.

## Setup

From the repo root (`olira-dotnet/`):

```bash
export PATH="$HOME/.dotnet:$PATH"   # if needed
cp examples/.env.example examples/.env
# edit examples/.env — set OLIRA_API_KEY

dotnet run --project examples/00_Quickstart
```

`ExampleEnv.Load()` (in `examples/_shared/ExampleEnv.cs`) reads `examples/.env` into the process environment when a variable is not already set.

## Examples

| Project | What it shows | Required scope(s) |
| ------- | ------------- | ----------------- |
| `00_Quickstart` | Create a patient, log one event, flush | `api:manage-patients`, `sdk:event-log` |
| `01_PatientManagement` | Full patient lifecycle: create, shell patient, batch, lookup, update, delete | `api:manage-patients` |
| `02_EventLogging` | `Log()` + `Flush()` queue vs `LogBatch()`, representative payloads, `OliraTrace` | `sdk:event-log`, `api:manage-patients` |
| `03_FhirIngestion` | `LogFhir()` with Condition, MedicationRequest, Appointment; error handling for unsupported types | `sdk:event-log`, `api:manage-patients` |
| `04_HistoricalIngestion` | Bulk historical load: file upload and inline records, optional `OliraTrace` on logs, two-phase confirm flow | `sdk:historical-ingest` |
| `05_LogsOnlyWorkflow` | Historical ingestion when patients already exist — logs-only job, no patient records in file | `sdk:historical-ingest`, `api:manage-patients` |
| `06_ReadPatientState` | Read compiled patient state: stable data, event modules, views, logs, events, memories | `sdk:state-read` |
| `07_PatientToken` | Mint a patient-scoped JWT; use with MCP / patient APIs | `sdk:patient-token` |
| `08_CohortManagement` | Full cohort lifecycle: create, list, get, update, enrol patients, assign/unassign templates, delete | `api:manage-patients` |
| `09_EhrIntegrations` | EHR integrations end-to-end (catalog, connect, probe, sync-now, write-back from `Log()` / `LogBatch()`) | `sdk:integrations`, `sdk:event-log`, `sdk:integration-write`, `api:manage-patients` |
| `09_OrgSchemaManagement` | Org schema catalog / management helpers | (see program header) |
| `10_ProjectManagement` | Full project (workspace) lifecycle: list, create, select via `new OliraClient(..., project:)`, duplicate (config-only), rename, deprecate, restore, permanent delete | `api:manage-projects` (org-wide key), `api:manage-patients` |
| `11_Signals` | Passive accelerometer batch via `OliraClient.SendSignals`, wait for absorb | `sdk:event-log`, `api:manage-patients` |
| `12_DocumentResourceIngestion` | Document resource ingestion: live `UploadDocument()` / OCR poll, plus historical `CreateIngestionJob(documents: [IngestDocument(…)])` package path | `sdk:event-log`, `sdk:historical-ingest`, `api:manage-patients` |

Run any example:

```bash
dotnet run --project examples/10_ProjectManagement
dotnet run --project examples/11_Signals
dotnet run --project examples/12_DocumentResourceIngestion
```

## Working with projects

A **project** is an isolated workspace within your org (its own patients, logs, state, views, cohorts, config). To operate _inside_ a project, select it on the client — every data call then reads/writes within that workspace:

```csharp
// process env
// OLIRA_PROJECT=dev-sandbox

using var client = new OliraClient(
    apiKey: apiKey,
    project: "dev-sandbox");  // or project id
```

Omit `project` and a project-locked key uses its own project, while an org-wide key uses the org's **default** project. To manage the projects themselves (create/duplicate/rename/deprecate/restore/delete), see `10_ProjectManagement` — those calls need an org-wide key with `api:manage-projects`.

## Notes

- Examples `04` and `05` both demonstrate historical ingestion; `05` covers the case where patients already exist in your org.
- `12_DocumentResourceIngestion` is a console app (not a notebook). Defaults to bundled `sample_data/demo_clinical_note.pdf` (born-digital, OCR-ready); set `DOCUMENT_PATH` to override.
- `10_ProjectManagement` needs an **org-wide** key with `api:manage-projects` (a project-locked key is confined to its own workspace); it creates, duplicates, and deletes throwaway projects and cleans them up.
- `11_Signals` serializes in-memory accelerometer rows to Parquet via Parquet.Net and waits for absorb (requires the signals/Timescale backend; local app-api may 500 without it).
- `06_ReadPatientState` and `07_PatientToken` require an existing patient id — run `00_Quickstart` or `02_EventLogging` first, then pass the printed id or set `PATIENT_ID` in `.env`.
- `08_CohortManagement` may skip template assignment steps unless `OLIRA_EXAMPLE_SUMMARY_TYPE` is set in `.env` (e.g. `symptom_snapshot`).
- Cleanup blocks at the end of each program delete demo patients/projects. These are not part of a real integration — remove them when adapting the code.
- `LiveSmoke` is an internal end-to-end smoke harness (not a numbered tutorial). Prefer the numbered examples for learning.
- Full API reference: [https://docs.olira.ai/reference/sdk](https://docs.olira.ai/reference/sdk).
