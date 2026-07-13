# Jigen Insight3 — Implementation Guide

Guide for the implementing model (Sonnet/Haiku). Follow it in order; the **Hard rules** section is binding and overrides any habit or template default.

## 1. Context and goal

Insight3 is the management UI for the Jigen vector-database server, replacing the two previous attempts:

- `src/Management/Jigen.Insight` (Vue 3 + Element Plus, hand-rolled Less styling) — rejected: too much custom CSS, not professional enough.
- `src/Management/Jigen.Insight2` (React + shadcn) — rejected: wrong stack for this team.

Insight3 is a **developer-facing database GUI** in the spirit of tools like Redis Insight (same *category* of product: browser + workbench + analysis for a database), but with original layout, naming and visuals. Do not copy Redis Insight UI, assets, or wording.

Target stack: **Vue 3 + TypeScript + Vite + Pinia + Naive UI + UnoCSS + ECharts**, based on the **Soybean Admin** template.

## 2. Template: Soybean Admin

- Repo: https://github.com/soybeanjs/soybean-admin (MIT)
- Docs: https://docs.soybeanjs.cn/
- Live demo: https://naive.soybeanjs.cn

Bootstrap:

1. Clone `soybean-admin` (NaiveUI edition) into `src/Management/Jigen.Insight3/`. Remove the template's `.git` history.
2. Strip all demo/example pages (`views/_builtin` demos, mock data, fake dashboards). Keep: layout system, router + file-route generation, theme store/settings, auth scaffolding, i18n plumbing, the ECharts hook (`useEcharts`) if present.
3. Keep the template's own conventions (file routing, `@sa/*` packages, UnoCSS config). Do not restructure the template; add feature pages inside its existing structure under `src/views/`.
4. Disable/remove the template's mock login and wire real auth (see §6 Phase 1).

## 3. Hard rules (binding)

1. **No custom CSS unless strictly necessary.** Styling comes from (a) Naive UI component props and theme tokens (`themeOverrides`), and (b) UnoCSS utility classes already configured by the template. Do **not** create `.css/.less/.scss` files and do **not** add `<style>` blocks to components. If a case is truly impossible without one (expect ≤ a handful in the whole app), add a one-line comment in the style block stating why a token/utility could not do it.
2. **Theming only via tokens.** Brand colors, radius, spacing go through Soybean's theme settings / Naive UI `themeOverrides` in one central place. Never hardcode hex colors in components or chart options — read them from the theme store so light/dark and re-theming keep working.
3. **Fluid layout, no fixed sizes.** Pages always fill the available viewport (flex/grid, `flex-1`, `h-full`, `min-h-0`). No fixed pixel widths/heights on page-level containers; fixed sizes are acceptable only for icons/avatars/logo. Content must adapt from laptop to ultrawide.
4. **Results are tables, no pagination.** Every result/list view uses Naive UI `n-data-table` with `virtual-scroll` enabled and **infinite/continuous loading** (load next chunk when the scroll approaches the end). No pager component anywhere.
5. **Master–detail with right panel.** Clicking a table row opens the detail in a right-side `n-drawer` (or the layout's right panel), never a new page, so the table context is preserved. Drawer width in `%`/`vw`, not px.
6. **Charts: ECharts only**, through `vue-echarts` (https://github.com/ecomfe/vue-echarts). One shared module builds the base chart theme (text, axis, grid colors) from the current Naive UI theme tokens; every chart imports it. Charts resize with their container (autoresize).
7. **All user-facing text via i18n keys** (template's vue-i18n setup). Provide `en` as the base locale; keys organized per feature.
8. **API types are generated, not hand-written.** Use `openapi-typescript` against the running server (`http://localhost:13223/openapi/v1.json`, same as Insight2's `gen:api-types` script) and type all services against the generated schema.
9. **No Chinese anywhere.** Soybean Admin ships with Chinese content: remove the `zh-CN` locale (and any other non-English locale files), set `en-US` as the only/default locale, and strip Chinese strings from code you touch — comments, console logs, config labels, README, HTML meta/title, loading screens. Nothing rendered in the UI, logged, or left in the source of pages you own may be in Chinese. Final check: a grep for CJK characters (`[一-鿿]`) in `src/` must return zero hits.

## 4. Branding

- Logos (recent versions, use these — not the ones inside Insight v1/v2): `assets/jigen-logo-full.png`, `assets/jigen-logo-full-dark.png`, `assets/files/jigen-logo-completo.svg`, icon `assets/logo_128.png`. Copy the needed files into `Jigen.Insight3/public/`.
- Recent brand palette (from `assets/files/jigen-logo.html`):
  - Primary: lime `#B5E61D`
  - Dark surface: `#15170E` (near-black with green cast)
  - Light surface: `#F8FAF1` / `#EFF3E2`
  - Accents: orange `#FF5C39`, violet `#7C5CFF`, teal `#00C9A7`
- Map these into Soybean's theme settings as primary/info/success/warning/error equivalents once, centrally (rule 3.2). Default appearance: **dark**, with the light theme fully working via the theme switcher.

## 5. Information architecture

Left sidebar (Soybean layout) with these sections. Nothing here copies Redis Insight screens; it maps 1:1 to Jigen's actual API surface (§7).

1. **Overview** (landing) — server health dashboard.
   - KPI tiles: CPU %, memory, total databases/collections/vectors, ingestion queue length (latest sample).
   - ECharts time series for CPU %, memory, ingestion queue and per-database sizes, with a window selector mapped to the API's fixed windows: 1m / 5m / 10m / 1h.
   - Data: `GET /api/metric/server-status/{1m|5m|10m|1h}` → `ServerStatusHistory` (samples with per-database and per-collection status). Poll while the page is visible; stop when hidden.
2. **Databases** — table of databases (name, created, collections, vectors, content/vector/index size, free space, users) with create/delete actions. Row click → right drawer with `DatabaseDetails`: storage breakdown (ECharts bar/treemap of content/vector/index per collection), collections summary, assigned users (`GET/PUT /api/database/{name}/users`). Delete asks confirmation and exposes the `deletefiles` flag.
3. **Collections** (per selected database) — table from `ListCollections` + `CollectionInfo` (vectors, dimensions, sizes, index metrics: max level, average degree, deleted count, quantization). Row click → drawer with index detail and entry points to Workbench and Graph pre-filtered on that collection.
4. **Workbench** (search) — the core page.
   - Query editor: database selector, multi-select of collections, input as *sentence* (server-side embedding) or raw *embeddings* array, `top` K.
   - `POST /api/database/{dbname}/collections/search` → results table (rule 3.4): score, key (decoded per §7 key note), collection, content preview. Row click → right drawer with pretty-printed JSON content.
   - Timing strip above results: embedding calculation / search / merge / sort times from `SearchCollectionsResult`, plus per-collection search time (small ECharts bar).
   - Document operations from the same page: get / upsert / delete by key (`.../documents/{key}` with `keyType` selector: auto | string | int | long | guid).
5. **Graph explorer** — HNSW index visualization from `GET .../collections/{collection}/graph?dimensions=2|3&limit=&level=`.
   - 2D: ECharts `graph` series with node coloring by level or deleted-state; 3D: `echarts-gl` scatter3D + lines3D. Controls: dimensions, node limit, HNSW level filter. Show snapshot stats (total/live/deleted nodes, max level, truncated flag). Highlight the entrypoint node.
6. **Security** — users, roles, apps from the Identity module (`/api/users`, `/api/roles`, `/api/roles/{id}/users`, `/api/identity/apps`): tables (rule 3.4) with create/update/delete, detail drawer on the right.
7. **Settings** — theme (Soybean's built-in settings drawer), API endpoint, locale.

## 6. Implementation phases

Do them in order; each phase must build (`vite build` + `vue-tsc`) before the next.

- **Phase 0 — Scaffold.** Clone + strip template (§2), apply branding tokens and logos (§4), routes/menu for the pages in §5 with empty placeholder views. Verify dark/light switch and fluid layout on every placeholder.
- **Phase 1 — API layer + auth.** Generate OpenAPI types, one service module per controller, Axios instance with base URL from `public/settings.json` (same pattern as Insight v1/v2). Wire login/logout to `/api/identity/login|logout` (inspect `src/Server/Modules/Identity/Jigen.Identity.API/Controllers/` for the exact contract — do not guess). Route guard from the template.
- **Phase 2 — Overview.** Metrics dashboard with shared ECharts theme module (rule 3.6).
- **Phase 3 — Databases + Collections.** Tables, drawers, create/delete flows.
- **Phase 4 — Workbench.** Search, results table with virtual scroll + infinite loading, detail drawer, document CRUD.
- **Phase 5 — Graph explorer.** 2D first, then 3D via `echarts-gl`.
- **Phase 6 — Security.** Users/roles/apps CRUD.
- **Phase 7 — Polish.** i18n completeness pass, empty/error/loading states (Naive UI `n-empty`, `n-result`, `n-skeleton`), confirm dialogs for destructive actions.

## 7. API reference

REST controllers (source of truth — read them before coding a service):

| Area | Endpoint | Notes |
|---|---|---|
| Databases | `GET/POST/DELETE /api/database`, `GET /api/database/{name}/details`, `GET/PUT /api/database/{name}/users` | `src/Server/Jigen.API/DatabaseController.cs`; DTOs `DatabaseDetails`, `DatabaseUserInfo` |
| Collections | `GET /api/database/{db}/collections`, `GET .../{collection}/info`, `GET .../{collection}/graph` | `src/Server/Jigen.API/CollectionsController.cs`; DTOs `CollectionInfo`, `IndexGraphSnapshot` |
| Search | `POST /api/database/{db}/collections/search` | `SearchCollectionsData` (sentence *or* embeddings) → `SearchCollectionsResult` with timings |
| Documents | `GET/POST/PUT/PATCH/DELETE .../{collection}/documents/{key}` (+`/json`), query `keyType` | Key is typed: string/int/long/guid, auto-detected if omitted |
| Metrics | `GET /api/metric/server-status/{1m,5m,10m,1h}` | `ServerStatusHistory` samples: CPU, memory, per-DB/per-collection sizes and HNSW stats |
| Identity | `POST /api/identity/login|logout`, `GET/POST/PUT/DELETE /api/users`, `/api/roles`, `GET /api/roles/{id}/users`, `GET /api/identity/apps`, OIDC under `/api/connect/*` | `src/Server/Modules/Identity/Jigen.Identity.API/Controllers/` |

Key note: `CollectionSearchResultItem.Key` arrives as raw bytes (base64 in JSON). Decode for display using the same rules as `CollectionsController.TryResolveKey` (guid = 16 bytes, long = 8, int = 4, otherwise UTF-8 string) and let the user override the interpretation.

## 8. Definition of done (per page)

- Fills the viewport at any window size, no horizontal scroll of the page body, no fixed page dimensions.
- Works in dark and light theme with no hardcoded colors (grep for `#` in views must return nothing outside the central theme module).
- No new `<style>` blocks or stylesheet files (rule 3.1).
- Tables virtualized, no pagination controls; detail via right drawer.
- All strings via i18n; loading/empty/error states present; destructive actions confirmed.
- `vue-tsc` clean, `vite build` succeeds.
