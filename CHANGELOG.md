# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
