# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.4.1] - 2026-08-14

### Added
- Optional `idempotencyKey` parameter on `LogFhir`/`LogFhirAsync` (`OliraClient`, `OliraModule`),
  matching `LogBatch`/`LogSpec`. Makes the call safe to retry after a network error or 5xx.
  One FHIR resource can map to several Olira events; pass one key for the call — the server
  applies it to each mapped event.
- `LogFhirAsync(string, object, CancellationToken)` overload preserving the pre-`idempotencyKey`
  positional call shape — `idempotencyKey` was inserted before `CancellationToken` on the main
  overload, so existing 3-positional-argument callers now resolve to this overload instead of a
  compile error.

## [0.4.0] - 2026-08-13

### Added
- Outbound-actions APIs on `OliraClient` (sync + async) and `OliraModule`:
  `CreateActionDestination`, `ListActionDestinations`, `GetActionDestination`,
  `UpdateActionDestination`, `DeleteActionDestination`,
  `RotateActionDestinationSecret`, `ListActionDeliveries`, `GetActionDelivery`,
  `RedeliverActionDelivery`. Requires the new `sdk:actions` scope. New models:
  `ActionTrigger` (string constants for the currently available triggers, plus
  `RecommendedDigestTriggers`; includes `integration.sync.failed`), `ActionDestination`, `ActionDestinationListResult`,
  `ActionDestinationDeleteResult`, `WebhookDestinationConfig`,
  `EmailDestinationConfig`, `DigestSchedule`, `ActionDelivery`,
  `ActionDeliveryListResult`, `DeliveryAttempt`.

## [0.3.0] - 2026-08-12

### Added
- Confidence scoring config API on `OliraClient` and `OliraModule`: org/view/block
  get/set, plus view scorer params and confidence weights helpers. Requires
  `api:org-config`.

## [0.2.0] - 2026-08-12

### Added
- Batch export API on `OliraClient` (sync + async) and `OliraModule`: `CreateExport`,
  `GetExport`, `ListExports`, and `DownloadExport`. Requires `sdk:state-read`.

## [0.1.0] - 2026-08-10

### Added
- `ListLogTypes()` / `GetLogType()` on `OliraClient` (sync + async) and `OliraModule` — live
  discovery of the platform's log-type catalog, including each type's full payload JSON Schema.
  Requires `sdk:event-log` scope. Complements the static `OliraLogType` constants for
  agent-guided mapping.

## [0.0.1] - 2026-08-05

### Added

- Initial public release of the Olira .NET SDK (`Olira` NuGet package).
- API parity with the Python `olira` package **1.11.1**, including:
  - Patient management (create, get, list, update, delete, batch create, patient tokens)
  - Event logging (`Log`, `LogBatch`, `LogFhir`) with background flush
  - Log query builder (`LogQuery` / `F`) against patient and population endpoints
  - Cohort management, org schema management, and project (workspace) APIs
  - Patient state reads (stable data, event modules, views, logs, memories)
  - Historical ingestion helpers and document / signal upload surfaces
- Auth via Olira API keys (`Bearer`), optional `X-Olira-Project` header, and retry policy
  for transient HTTP failures (non-idempotent creates are not retried).
