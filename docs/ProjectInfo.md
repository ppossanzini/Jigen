# Project Decisions Log

## ADR Index
- ADR-0001 | Accepted | Backend | Database access control and DB details endpoint baseline | docs/ProjectInfo.md
- ADR-0002 | Accepted | Backend | Database ownership enforcement for read visibility | docs/ProjectInfo.md
- ADR-0003 | Accepted | Backend | Multi-collection search API with timing telemetry | docs/ProjectInfo.md
- ADR-0004 | Accepted | Backend | In-memory server status history for metrics module | docs/ProjectInfo.md
- ADR-0005 | Superseded | Backend | Fixed selectable windows for Metrics status endpoint | docs/ProjectInfo.md
- ADR-0006 | Accepted | Backend | Explicit route windows for Metrics status endpoint | docs/ProjectInfo.md
- ADR-0007 | Accepted | Backend | Embeddings bulkhead with bounded queue and concurrency cap | docs/ProjectInfo.md

## ADR-0001 - Database access control and DB details endpoint baseline
- Date: 2026-06-18
- Status: Accepted
- Context:
  - The server must support associating users to each database.
  - API must expose details for current target database: size, collections, creation date, and associated users.
  - Existing persistence in `SystemInfo` only stores a flat list of database names.
- Decision:
  - Use a hybrid model in server metadata: retain database list and add per-database metadata records.
  - Store user associations per database with `UserId` plus `UserName` snapshot.
  - Expose new database details endpoint on explicit database route.
  - Interpret "utenti collegati" as enabled/associated users, not active real-time sessions.
  - Persist database creation timestamp from now on, with no automatic backfill for legacy entries.
  - Keep implementation within Core/API/Handlers CQRS boundaries and avoid EF migrations.
- Consequences:
  - Backward compatibility logic is needed for legacy `SystemInfo` payloads.
  - Database details responses can include stale usernames if identity records change or are deleted.
  - Future work may add active session tracking as a separate feature.

## ADR-0002 - Database Ownership Enforcement for Read Visibility
- Date: 2026-06-19
- Status: Accepted
- Context:
  - Requirement: users must only see databases they are associated with.
  - Requirement: when a database is not owned by the caller, API reads must not reveal either existence or content.
  - Existing read handlers returned data for any known database and did not evaluate current identity ownership.
- Decision:
  - Introduce a centralized ownership guard in Handlers CQRS to evaluate current user and database association from system metadata.
  - Filter `ListDatabases` output to only databases readable by current user.
  - Enforce ownership checks on all database and collection read queries; unauthorized access returns the same `Database not found` error used for non-existing databases.
  - Treat `DatabaseAdmin` role/permission as bypass for read ownership checks.
  - Automatically associate the authenticated creator to the database user list during database creation.
  - Keep ownership persisted in `SystemInfo.DatabaseInfos[].Users` without EF migrations and without changing API contracts.
- Consequences:
  - Unauthorized users can no longer infer database existence via read APIs.
  - Existing databases with empty user associations become invisible to non-admin users until associations are configured.
  - Centralized guard reduces duplication and keeps read authorization behavior consistent across handlers.

## ADR-0003 - Multi-collection search API with timing telemetry
- Date: 2026-06-19
- Status: Accepted
- Context:
  - A new API endpoint is needed to search across multiple collections within a single database.
  - The search input must support either plain text (requiring embeddings generation) or precomputed embeddings.
  - The response must expose per-phase timings for embeddings calculation, search, merge, and sorting, plus per-collection results.
- Decision:
  - Add a dedicated `SearchCollections` query in Core with a body DTO carrying collection list, sentence/embeddings, and top-k.
  - Add a REST endpoint under collections scope that validates mutual exclusivity of sentence versus embeddings and dispatches only via Hikyaku.
  - Implement search execution in handlers by iterating selected collections, collecting per-collection top-k results, then merging and sorting globally.
  - Return a structured payload that includes per-collection results and timing metrics: embeddings calculation, total search, merge, and sorting.
  - Keep the existing single-collection search flow unchanged for backward compatibility.
- Consequences:
  - API consumers can profile end-to-end query cost and compare phase impact.
  - Global merged ranking is limited to top-k after merge/sort, while each collection still exposes its local top-k.
  - Text-based queries have additional latency due to embeddings generation, now explicitly measurable.

## ADR-0004 - In-memory server status history for metrics module
- Date: 2026-06-22
- Status: Accepted
- Context:
  - A new Metrics API endpoint is needed to expose server state over the last hour with one sample every 5 seconds.
  - The payload must include process CPU usage, process memory usage, per-database ingestion backlog, and collection sizes expressed as number of elements.
  - The Metrics module currently has no CQRS contracts, controllers, or handler wiring.
- Decision:
  - Implement a dedicated Metrics CQRS query that returns a fixed 1-hour rolling window sampled every 5 seconds.
  - Keep sampling in memory inside the Metrics Handlers module through a singleton hosted service, with no persistence layer or EF usage.
  - Build per-database snapshots directly from active `Store` instances exposed by `DatabasesManager`, using `IngestionQueueLength` and `GetCollectionInfo`.
  - Expose the data via a dedicated Metrics API controller that dispatches only through `IHikyaku`.
- Consequences:
  - Metrics history is available only after process startup and is limited to the last rolling hour.
  - Sampling remains lightweight and avoids schema changes, but historical data is lost on restart.

## ADR-0005 - Fixed selectable windows for Metrics status endpoint
- Date: 2026-06-22
- Status: Superseded
- Superseded-By: ADR-0006
- Context:
  - Consumers need to query server status for shorter recent windows without downloading the full one-hour dataset.
  - Allowed windows must be constrained to a finite set: 1 minute, 5 minutes, 10 minutes, or 1 hour.
- Decision:
  - Extend Metrics status query with a requested window and enforce a strict allow-list of windows.
  - Accept the window in API as `window` query parameter with values `1m`, `5m`, `10m`, `1h`.
  - Keep internal sampling cadence and retention unchanged (5-second samples, 1-hour rolling buffer).
  - Return only filtered samples for the requested window while preserving response shape.
- Consequences:
  - Clients can reduce payload size and focus on short-term trends.
  - Unsupported window values return HTTP 400 at API boundary.

## ADR-0006 - Explicit route windows for Metrics status endpoint
- Date: 2026-06-22
- Status: Accepted
- Supersedes: ADR-0005
- Context:
  - Query-parameter based windows are less discoverable for API consumers.
  - Requirement is to expose explicit self-describing routes for each supported window.
- Decision:
  - Replace query-parameter selection with explicit routes under `metric/server-status`.
  - Expose four endpoints only: `/metric/server-status/1m`, `/metric/server-status/5m`, `/metric/server-status/10m`, `/metric/server-status/1h`.
  - Keep internal query payload and service filtering unchanged.
- Consequences:
  - API contract is clearer and easier to discover in OpenAPI.
  - Existing clients using query parameter must update to the new route paths.

## ADR-0007 - Embeddings bulkhead with bounded queue and concurrency cap
- Date: 2026-06-22
- Status: Accepted
- Context:
  - Embeddings computation is CPU/ONNX intensive and concurrent spikes can saturate resources.
  - Requirement: enforce a maximum number of concurrent executions and queue overflow requests.
- Decision:
  - Introduce a queued wrapper (`QueuedEmbeddingGenerator`) around `IEmbeddingGenerator`.
  - Use bounded channel queue with configurable capacity and configurable worker concurrency.
  - Expose runtime knobs in `JigenServer` settings: `EmbeddingsMaxConcurrency`, `EmbeddingsQueueCapacity`, `EmbeddingsQueueTimeoutSeconds`.
  - Keep legacy behavior only when cap is explicitly disabled (`EmbeddingsMaxConcurrency <= 0`).
- Consequences:
  - Request bursts are smoothed by queueing instead of spawning uncontrolled concurrent embedding executions.
  - When queue stays full beyond timeout, requests fail fast with timeout error.
  - Same pattern can be applied to other heavy services by adding equivalent queued decorators.
