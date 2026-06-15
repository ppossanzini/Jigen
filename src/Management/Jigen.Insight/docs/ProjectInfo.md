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
