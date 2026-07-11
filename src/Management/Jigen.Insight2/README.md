# Jigen Insight (v2)

Console di gestione per Jigen (vector database, indice HNSW): login, gestione database/collection, ricerca semantica multi-collection, Graph Explorer HNSW, gestione utenti/ruoli, dashboard metriche.

Riscrittura di `src/Management/Jigen.Insight` (Vue 3) su base [shadcn-admin](https://github.com/satnaing/shadcn-admin): React 19 + Vite + TanStack Router/Query + Tailwind v4 + shadcn/ui, con ECharts al posto di recharts per i grafici.

## Tech stack

- **Build**: Vite
- **UI**: shadcn/ui (TailwindCSS + Radix UI)
- **Routing**: TanStack Router (file-based)
- **Data fetching**: TanStack Query
- **Stato client**: Zustand
- **Grafici**: ECharts / echarts-gl (Graph Explorer 3D)
- **Tipi API**: generati da `openapi-typescript` a partire dall'OpenAPI del server Jigen

## Sviluppo

Il server Jigen deve essere in esecuzione su `http://localhost:13223` (vedi `public/settings.json` per la configurazione runtime di base URL e client OIDC).

```bash
npm install
npm run dev
```

Rigenerare i tipi API dopo una modifica ai contratti del server:

```bash
npm run gen:api-types
```

Il dev server gira sulla porta 5173 (stessa del vecchio Jigen.Insight) per riusare il client OIDC `jigen-insight-spa` già registrato lato server, senza bisogno di configurazioni aggiuntive.
