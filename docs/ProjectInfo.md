# Project Decisions Log

## ADR Index
- ADR-0001 | Accepted | Backend | Database access control and DB details endpoint baseline | docs/ProjectInfo.md
- ADR-0002 | Accepted | Backend | Database ownership enforcement for read visibility | docs/ProjectInfo.md

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
