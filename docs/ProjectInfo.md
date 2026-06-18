# Project Decisions Log

## ADR Index
- ADR-0001 | Accepted | Backend | Database access control and DB details endpoint baseline | docs/ProjectInfo.md

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
