# Project Decisions (ADR Log)

## ADR-0001 - Application Structure Baseline
Date: 2026-06-15
Status: Accepted
Context:
- User requested only the application structural layer based on provided dashboard and index management references.
- Workspace started from a minimal Vue scaffold without Element Plus, i18n, or module architecture.
Decision:
- Generate a module named `jigen-db` from user input "Jigen DB" using kebab-case folder naming.
- Build a shell-first architecture with separate structural components: header, sidebar, and footer.
- Keep view files responsible for orchestration/state while components remain presentational.
- Use Element Plus controls but no `<el-row>`/`<el-col>`, relying on CSS Grid/Flex layouts.
- Implement a reusable Coming Soon route for navigable but not-yet-implemented features.
Consequences:
- The app gains a scalable layout foundation aligned with the skill constraints.
- Future feature pages can be plugged into shell routing without layout rewrites.

## ADR-0002 - UX, i18n, and State Conventions
Date: 2026-06-15
Status: Accepted
Context:
- User selected mock data, no geospatial scope, custom operations, and dynamic table page-size behavior.
- The design references require shared navigation state and dashboard/index pages with independent scrolling.
Decision:
- Configure `vue-i18n` with `en` as base locale and move all visible labels to translation keys.
- Add a Pinia store for cross-component navigation synchronization (header/sidebar/view).
- Enforce independent scrolling between sidebar and main content by constraining shell height to viewport.
- Include explicit pointer cursor styles for actionable custom UI wrappers.
- Implement dynamic default table pagination size calculated from available viewport space in Index Management view.
Consequences:
- Navigation and feature context stay synchronized across non-parent-child components.
- The initial UI is internationalization-ready and aligned with the requested interaction constraints.

## ADR-0003 - Initial Scope Execution Notes
Date: 2026-06-15
Status: Accepted
Context:
- User selected mock data, custom operations, no geospatial integration, and dynamic table page-size.
- Workspace scan found no reusable module components to adopt.
Decision:
- Implement only the structural shell and foundation views (Dashboard, Index Management, Coming Soon).
- Keep data source mocked inside views and leave service layer creation for a later API-focused iteration.
- Route all non-implemented sidebar sections to a reusable Coming Soon view with feature context in query/store.
- Place all module-specific components under `src/modules/jigen-db/components/<ComponentName>/`.
- Keep all generated texts under i18n keys with `en` locale as baseline.
Consequences:
- The app is immediately navigable and visually aligned to the reference wireframes.
- CRUD/service integration can be added incrementally without reworking shell composition.

## ADR-0004 - Authentication Module and Sign-In UX
Date: 2026-06-16
Status: Accepted
Context:
- User requested implementation of the sign-in workspace selector screen from the provided design reference.
- Data source was confirmed as OpenAPI `https://localhost:13223/openapi/v1.json` with custom operations and no geospatial/table scope.
Decision:
- Create a dedicated module `src/modules/auth` with view orchestration in `SignInView` and three presentational components: `SignInWorkspaceSelector`, `SignInHeroPanel`, `SignInCard`.
- Integrate REST login via axios service layer (`baseRestService` + `authService`) targeting `POST /identity/login`, with optional workspace sent in payload.
- Add typed contracts in `@types/auth.ts` and wire auth state through Pinia (`auth` store) for token/user/workspace persistence and route guarding.
- Set `/sign-in` as entry route for unauthenticated users and redirect authenticated sessions to `/dashboard`.
- Keep all visible texts in i18n namespace `auth.*`, include pointer cursor on actionable custom elements, and preserve mobile/desktop responsiveness.
Consequences:
- Authentication now gates application shell routes and provides a reusable foundation for future identity features (logout, refresh token, SSO).
- Workspace selection is synchronized between login and shell header through shared store state.

## ADR-0005 - Security Users/Roles Master-Detail Module
Date: 2026-06-16
Status: Accepted
Context:
- User requested a new master-detail experience for users and roles management under `src/modules/security`.
- Data source was confirmed as OpenAPI `https://localhost:13223/openapi/v1.json` with dynamic table page-size and accessibility QA requested during development.
Decision:
- Create a dedicated `security` module with nested routes under `/security` using a shared `SecurityLayout` and two views: `UsersMasterDetail` and `RolesMasterDetail`.
- Add typed OpenAPI contracts in `@types/security.ts` and a dedicated REST adapter `src/services/securityService.ts` using `/users`, `/users/{id}`, `/roles`, `/roles/{id}` with identity fallbacks for listing endpoints.
- Introduce `src/stores/security.ts` (Pinia) as shared cross-view state for users, roles, selected entities, and in-memory user-role assignments.
- Implement users CRUD plus role assignment from the user detail pane, and roles CRUD with cross-reference to assigned users.
- Extend shell navigation and router metadata with `security-users` and `security-roles` routes and add complete i18n coverage in `en.ts`.
Consequences:
- Security administration is now reachable as first-class application functionality, no longer routed through Coming Soon placeholders.
- Role assignment state is persisted through update calls and synchronized in the shared store for consistent master-detail behavior across both security views.

## ADR-0006 - Database Management Scope, Authorization, and Naming Alignment
Date: 2026-06-17
Status: Accepted
Context:
- User requested a dedicated database management screen with operations read/create/delete and rule: create/delete only for `DatabaseAdmin`.
- OpenAPI source confirmed at `https://localhost:13223/openapi/v1.json` with endpoints `/database` and `/database/{dbname}/collections`.
- Existing implementation used Index naming and needed scope realignment to Database semantics.
Decision:
- Introduce typed API contracts in `@types/database.ts` and a transport-only service `src/services/databaseService.ts` using axios/baseRestService conventions.
- Add shared Pinia store `src/stores/database.ts` for cross-component synchronization of selected database and loaded collections.
- Enforce role-based gating through auth store roles and `isDatabaseAdmin` getter; allow read to authenticated users and gate create/delete in UI interactions.
- Rename all UI artifacts from `Index*` to `Database*` (`DatabaseManagementView`, `DatabaseToolbar`, `DatabaseTable`, `DatabaseDetailPanel`) including routes and imports.
- Keep dynamic table page size calculation and all user-facing strings in i18n under `databaseManagement.*`.
Consequences:
- Scope naming is now coherent with functional behavior, reducing ambiguity for future maintenance.
- Database and collections state is synchronized across non parent-child components via Pinia, aligning with project rules.
- Authorization constraints are visible and enforced in the UI, with clear feedback for non-admin users.

## ADR-0007 - Security Views Visual Alignment With Database View
Date: 2026-06-17
Status: Accepted
Context:
- User requested redesign of Users/Roles section to match the visual style used in Database Management view.
- Existing security pages used functional master-detail structure but with less consistent top-level toolbar/title treatment.
Decision:
- Refactor `UsersMasterDetail` and `RolesMasterDetail` templates to adopt the same high-level visual composition (`title + subtitle + action area`, consistent spacing and grid container semantics).
- Introduce shared security view class contract (`security-master-view*`) in view-local LESS files aligned to Database view spacing and sizing.
- Align dialog footer actions layout to the same horizontal action group pattern used in Database view.
- Keep business logic, data flow, permissions, and API/store contracts unchanged.
Consequences:
- Users/Roles pages are visually consistent with Database Management, improving perceived cohesion across modules.
- The redesign is low-risk because it is presentation-focused and does not alter domain behavior.

## ADR-0008 - Remove Users/Roles Section
Date: 2026-06-17
Status: Accepted
Context:
- User requested complete removal of Users and Roles section.
- Users/Roles were implemented through dedicated security module views, routes, service, store, and shared API types.
Decision:
- Remove routes and references to `security-users` and `security-roles` from router and sidebar navigation.
- Delete `src/modules/security` folder entirely.
- Delete security-specific data layer files: `src/services/securityService.ts`, `src/stores/security.ts`, `@types/security.ts`.
- Keep `security` and `settings` sidebar entries navigable by routing them to the existing Coming Soon target.
Consequences:
- Users/Roles feature is no longer reachable or compiled in the frontend.
- Security-related i18n keys remain as inactive text resources and can be cleaned in a future pass if requested.

## ADR-0009 - Reintroduce Security Users/Roles as Database-like Master-Detail
Date: 2026-06-17
Status: Accepted
Context:
- User requested implementation of users/roles management with master-detail behavior similar to Database Management.
- Data source confirmed as OpenAPI `https://localhost:13223/openapi/v1.json` with real REST integration, dynamic table page size, and accessibility QA requested.
- Security module had been removed in ADR-0008, therefore full reintroduction was required.
Decision:
- Create mandatory consumer routing instruction files in `.github/instructions/` for frontend and backend skill routing before code generation.
- Reintroduce security domain through `@types/security.d.ts`, `src/services/securityService.ts`, and `src/stores/security.ts` using `/users`, `/users/{id}`, `/roles`, `/roles/{id}` plus identity fallback endpoints.
- Implement two routed views under `src/modules/jigen-db/views`: `SecurityUsersView` and `SecurityRolesView` with dedicated presentational components in `src/modules/jigen-db/components/<ComponentName>/`.
- Align visual composition to Database Management pattern (toolbar + master table + detail panel), keep cross-component synchronization in Pinia, and enable dynamic pagination size recalculation from viewport.
- Resolve layout rule conflict by applying stricter orchestrator rule: avoid `el-row`/`el-col` and use CSS grid/flex even though local Element Plus instructions generally suggest grid components.
Consequences:
- Security Users and Roles are again first-class navigable routes (`/security/users`, `/security/roles`) within the shell sidebar.
- REST and state management are centralized and reusable, with selection consistency between master and detail sections.
- Documented conflict resolution prevents future divergence on layout primitives.

## ADR-0010 - Migrate Sign-In to Authorization Code + Token Flow
Date: 2026-06-17

## ADR-0011 - Database Management Details From /database/{name}/details
Date: 2026-06-18
Status: Accepted
Context:
- User requested that Database Management detail panel be populated from new API `GET /database/{name}/details`.
- OpenAPI source confirmed at `http://localhost:13223/openapi/v1.json` exposing schema `DatabaseDetails` with storage, vectors, collections, and users metadata.
- Existing frontend detail panel was based mainly on collections list and did not consume the new database details contract.
Decision:
- Extend `@types/database.ts` with `DatabaseDetailsApi`, `CollectionInfoApi`, and `DatabaseUserInfoApi` contracts.
- Extend `src/services/databaseService.ts` with `getDatabaseDetails(name)` and strict mapping to normalized frontend details model.
- Extend `src/stores/database.ts` with `detailsByDatabase`, `loadingDetails`, `loadDetailsFor`, and `selectedDatabaseDetails` getter for cross-component synchronization.
- Refactor `DatabaseDetailPanel` to render overview and diagnostics from API details (non-tabular card/list style), including collections and users sections.
- Update `DatabaseManagementView` orchestration to load details alongside collections on selection, refresh, and creation flows.
Consequences:
- Detail panel now reflects backend source of truth for database-level metrics and associated users.
- UX keeps non-tabular detail rendering aligned with repository structural memory rules.
- Frontend state adds one more synchronized slice (`detailsByDatabase`) that must be invalidated on delete and refreshed after mutations.
Status: Accepted
Context:
- Login flow was expected to use authorization code plus token exchange, but frontend accepted cookie-only login responses.
- `authService` generated a local fallback token when backend did not return a token, causing false authenticated state and downstream 401 errors.
Decision:
- Remove local fake-token fallback from `src/services/authService.ts` and accept only real token responses.
- Add OAuth Authorization Code + PKCE flow in frontend with redirect to `/connect/authorize` and callback route `/auth/callback`.
- Exchange authorization code at `/connect/token` and persist only returned access token in auth store/local/session storage.
- Keep credential submit (`/identity/login`) as identity bootstrap, then continue with authorization redirect when direct token is not returned.
- Add typed OIDC env configuration (`VITE_OIDC_CLIENT_ID`, `VITE_OIDC_CLIENT_SECRET`, `VITE_OIDC_REDIRECT_URI`, `VITE_OIDC_SCOPE`) and callback i18n copy under `auth.*`.
Consequences:
- Frontend authentication is aligned to code+token semantics and no longer relies on cookie-only session as final app auth state.

## ADR-0012 - Database Details User Association Flow
Date: 2026-06-19
Status: Accepted
Context:
- User requested assigning users to a database directly from Database Details panel.
- OpenAPI verification on `http://localhost:13223/openapi/v1.json` exposes `PUT /database/{name}/users` with `SetDatabaseUsersData` payload.
- Current frontend already shows associated users in details but had no write interaction.
Decision:
- Extend `src/services/databaseService.ts` with `setDatabaseUsers(name, users)` targeting `PUT /database/{name}/users`.
- Extend `src/stores/database.ts` with `setDatabaseUsers` action to synchronize `detailsByDatabase` and `usersCount` after successful update.
- Add Database Details interaction in `DatabaseDetailPanel` using Element Plus controls (`el-form`, `el-select`, `el-button`) to select and add a user.
- Load assignable users from `securityService.listUsers()` in `DatabaseManagementView` and submit merged existing+selected users to preserve current assignments.
- Add dedicated i18n labels and feedback messages under `databaseManagement.assignUser` and `databaseManagement.feedback`.
Consequences:
- Database administrators can grant user access from the same details context where assignments are inspected.
- Frontend updates remain aligned with backend contract and avoid overwriting existing associations by sending merged user lists.
- Misconfigured OAuth clients now surface explicit login failure instead of silently creating invalid local sessions.

## ADR-0012 - Database Workspace Progressive Columns and Collection Drill-Down
Date: 2026-06-19
Status: Accepted
Context:
- User requested that Database workspace columns expand progressively so collections appear in a third column when a database is selected.
- User requested a fourth column for selected collection details/exploration, with left tables shrinking to make room.
- User explicitly accepted horizontal scrolling at workspace level.
Decision:
- Keep column order fixed as: Database Table, Database Details, Collections, Collection Details.
- Show Collections column only when a database is selected and Collection Details column only when a collection is selected.
- Extend shared Pinia store state with selected collection tracking and derived selected collection detail.
- Implement a dynamic CSS Grid strategy with class-based templates (`default`, `has-database`, `has-collection`) and horizontal overflow on workspace grid.
- Keep detail rendering semantic/non-tabular in side panels and keep table behavior resilient with minimum width constraints.
Consequences:
- Database management now supports progressive drill-down without route changes or modal transitions.
- Wide viewports display all contextual levels at once, while narrower viewports preserve access through horizontal grid scrolling.
- Collection explorer currently exposes structural and metric details; deep content browsing can be added later without changing the layout contract.

## ADR-0013 - Local i18n Labels In Child Components
Date: 2026-06-19
Status: Accepted
Context:
- New project rule: fixed labels coming only from i18n must not be forwarded as props from parent views.
- Multiple module views (`jigen-db`, `security`) were passing static localized labels to presentational children.
Decision:
- Refactor child components to resolve fixed labels directly with local i18n (`$t(...)`) instead of receiving dedicated label props.
- Keep text props only where text is runtime-dynamic from parent context (for example titles that include selected entity names).
- Remove obsolete label props from component contracts and from parent bindings in corresponding views.
Consequences:
- Component APIs are slimmer and less coupled to parent views.
- i18n ownership is clearer for static UI copy, reducing repetitive prop plumbing.
- Parent views keep focus on orchestration and dynamic state only.

## ADR-0014 - SecurityAdmin-Gated Database User Access Revocation
Date: 2026-06-19
Status: Accepted
Context:
- User reported that database user access could be granted but not removed from Database Management.
- User explicitly requested non-cascade behavior and a dedicated detailed mask before revocation.
- User constraint: database user manipulation must be allowed only for role `SecurityAdmin`.
Decision:
- Extend auth store with `isSecurityAdmin` role getter and keep `isDatabaseAdmin` for create/delete database actions only.
- Gate assign/remove user access actions in Database Management view with `SecurityAdmin` checks and dedicated feedback message.
- Add a detailed revoke-access dialog that shows database, user name, user id, explicit non-cascade warning, and confirmation checkbox.
- Execute revocation by updating `PUT /database/{name}/users` payload with selected user removed from current association list.
- Extend `DatabaseDetailPanel` user list with explicit remove action button for each associated user when security permission is present.
Consequences:
- Security policy is explicit in UI: only `SecurityAdmin` can modify database user access while read-only visibility remains available.
- Revocation flow now prevents accidental destructive expectations by clearly stating non-cascade behavior.
- Frontend keeps backend contract unchanged by reusing existing set-users endpoint with filtered associations.
