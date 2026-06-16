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
