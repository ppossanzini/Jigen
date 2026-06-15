# Infrastructure ADRs - File 01

## ADR-0004 - Hotfix browser login (TLS locale)
- Date: 2026-06-14
- Status: Accepted
- Context: browser bloccava chiamate dirette HTTPS con certificato non trusted.
- Decision:
  - API base default: /api.
  - Proxy Vite: /api -> https://localhost:13223 con secure=false.
- Consequences:
  - Chiamate API non bloccate dal browser in sviluppo locale.
  - Login raggiunge backend; esito 401 quando credenziali non valide.
