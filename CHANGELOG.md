# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.5.0] - 2026-08-17

### Added
- Optional `idempotencyKey` parameter on `LogFhir`/`LogFhirAsync` (`OliraClient`, `OliraModule`),
  matching `LogBatch`/`LogSpec`. Makes the call safe to retry after a network error or 5xx.
  One FHIR resource can map to several Olira events; pass one key for the call — the server
  applies it to each mapped event.
- `LogFhirAsync(string, object, CancellationToken)` overload preserving the pre-`idempotencyKey`
  positional call shape — `idempotencyKey` was inserted before `CancellationToken` on the main
  overload, so existing 3-positional-argument callers now resolve to this overload instead of a
  compile error.
- `ExternalIdentifier.IntegrationId` — the platform-assigned id of the integration that owns an
  identifier (e.g. an Epic sync). Read-only; present on `GetPatient`/`ListPatients` responses, and
  safe to omit or echo unchanged on `UpdatePatient`.
- `AddPatientExternalIdentifiers`/`RemovePatientExternalIdentifiers` (`OliraClient` sync + async,
  and `OliraModule`) — add or remove one or more external identifiers on a patient without a full
  `UpdatePatient` replace. Idempotent. `RemovePatientExternalIdentifiers` takes
  `ExternalIdentifierMatcher` entries: `System` + `Value` (one row), `System` only
  (every identifier for that system — `System = "epic"` unlinks every connected Epic
  instance), `IntegrationId` only (that instance), or `System` + `IntegrationId`.
  It is the only way to delete an identifier, and can remove one owned by a platform
  integration — a deliberate, irreversible unlink. An empty matcher, or `Value` set
  without `System`, is rejected client-side before the request is sent.
- `ListPatients(integrationId: ...)` and `ListPatients(externalSystem: ...)` without a
  value — find every patient linked to one integration instance, or every patient with
  an identifier for that system. `externalValue` still requires `externalSystem`.

### Fixed
- `LogFhir`/`LogFhirAsync` no longer retries automatically on a transport-level network error or
  5xx when no `idempotencyKey` is supplied. Without a key, the server has no stable dedup anchor,
  so replaying a request whose response was lost could create a duplicate event — the transport's
  own retry now only fires when a key makes that replay safe.
- `ExternalIdentifier` previously had no `IntegrationId` property, so a `GetPatient` → append →
  `UpdatePatient` round-trip silently stripped a stored integration link. The server now also
  treats `UpdatePatient`'s `ExternalIdentifiers` as merge/append-only, so this can no longer
  happen even with an older SDK or a raw HTTP client.

### Changed
- **Breaking:** `UpdatePatient(externalIdentifiers: ...)` no longer replaces the stored list — it
  merges. New (System, Value) pairs are added; anything already stored, including a
  platform-owned identifier, is left untouched whether or not you include it. An empty list is
  now rejected (422) instead of clearing every identifier — use
  `RemovePatientExternalIdentifiers` to remove one.

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
