# Frontend ADRs - File 01

## ADR-0001 - Scope V1 e input iniziali
- Date: 2026-06-14
- Status: Accepted
- Context: avvio client da OpenAPI locale per prima release.
- Decision:
  - Sorgente dati: https://localhost:13223/openapi/v1.json.
  - Scope: solo login + pagina principale (no CRUD completo).
  - Mappe: escluse in questa fase.
  - Paginazione futura: page size dinamico.
  - Reuse componenti: libreria interna assente, consentita creazione module-based.
- Consequences:
  - Delivery rapida su onboarding utente e shell applicativa.
  - CRUD completo rinviato a iterazioni successive.

## ADR-0002 - Architettura UI e layout
- Date: 2026-06-14
- Status: Accepted
- Context: serviva un layout coerente e regole UI non ambigue.
- Decision:
  - Sidebar sempre navigabile, fallback su route Coming Soon.
  - Stato cross-view gestito con Pinia.
  - Scroll indipendente sidebar/main, root page non scrollabile.
  - In conflitto regole, applicata la piu restrittiva: no el-row/el-col.
  - Tema riallineato alla palette del logo jigen3.
- Consequences:
  - Navigazione robusta anche con moduli non ancora implementati.
  - Riduzione di inconsistenze visuali e di stato tra viste.

## ADR-0003 - Struttura tecnica Login + Main
- Date: 2026-06-14
- Status: Accepted
- Context: richiesta implementazione end-to-end in Vue 3 + Element Plus.
- Decision:
  - Dipendenze: element-plus, vue-i18n, axios, less.
  - Endpoint auth usati: /identity/login, /identity/logout.
  - Layer creati: src/services, src/stores, src/lang, src/@types, src/layouts, src/modules.
  - Routing: login pubblico + area /app protetta + coming-soon.
  - Moduli attivi: auth (login) e main (home).
- Consequences:
  - Base pronta per estendere moduli CRUD senza rifare bootstrap.

## ADR-0005 - Verifiche tecniche
- Date: 2026-06-14
- Status: Accepted
- Context: validare stabilita minima prima delle iterazioni CRUD.
- Decision:
  - Type check: OK.
  - Build: OK.
  - Warning residui: annotazioni PURE da dipendenza terza (non bloccanti).
- Consequences:
  - Stato codice idoneo per proseguire con moduli funzionali successivi.

## ADR-0006 - Console-style UI refactor
- Date: 2026-06-15
- Status: Accepted
- Context: richiesta evoluzione visuale verso esperienza dashboard prodotto ad alta densita informativa.
- Decision:
  - Ambito: login + main + shell completa.
  - Priorita: layout prodotto ad alta densita informativa (non landing marketing).
  - Palette: mantenuta famiglia jigen3 (cyan/magenta/violet).
  - Motion: subtle (fade/slide brevi e non invasive).
  - Nuovi pannelli operativi e topbar meta-state con i18n completo.
- Consequences:
  - UI piu tecnica e leggibile in ottica console.
  - Base pronta per espansione moduli operativi senza refactor strutturale.

## ADR-0007 - Login simplification and neutral branding
- Date: 2026-06-15
- Status: Accepted
- Context: richiesta rimozione pannello laterale login, inserimento logo e riduzione sfumature sfondo.
- Decision:
  - Rimosso pannello informativo laterale dalla vista login.
  - Inserito logo Jigen in posizione centrale sopra la card di accesso.
  - Background login reso molto scuro e quasi uniforme.
  - Verificata assenza di riferimenti "redis" nei sorgenti e docs progetto.
- Consequences:
  - Login piu pulito e coerente con branding prodotto.
  - Interfaccia meno rumorosa visivamente in fase di accesso.

## ADR-0008 - Global terminal monospace typography
- Date: 2026-06-15
- Status: Accepted
- Context: richiesta di applicare un font monospace stile terminale in tutta l'applicazione.
- Decision:
  - Definito stack monospace globale in `--font-mono-stack`.
  - Applicato il font al `body` per tutta la UI applicativa.
  - Forzato allineamento Element Plus tramite `--el-font-family`.
- Consequences:
  - Tipografia coerente tra pagine custom e componenti Element Plus.
  - Identita visuale piu tecnica e uniforme in tutto il prodotto.

## ADR-0009 - Centralized dashed border for app cards
- Date: 2026-06-15
- Status: Accepted
- Context: richiesta di usare `2px dashed` per le card interne con gestione centralizzata.
- Decision:
  - Introdotta variabile globale `--app-card-border` in `variables.less`.
  - Applicata regola globale `.el-card { border: var(--app-card-border) !important; }`.
  - Sostituiti bordi hardcoded delle card principali con la variabile centralizzata.
- Consequences:
  - Aggiornare il bordo card richiede una sola modifica globale.
  - Coerenza visuale tra login, main hero, pannelli dashboard e coming soon.

## ADR-0010 - Users and roles module V1
- Date: 2026-06-15
- Status: Accepted
- Context: richiesta implementazione gestione utenti e ruoli lato frontend con integrazione shell esistente.
- Decision:
  - Creato modulo `users` con vista dedicata `UsersRolesView` sotto route protetta `/app/users`.
  - Separati pannelli presentazionali `UsersPanel` e `RolesPanel` con dialog CRUD e tabelle Element Plus.
  - Introdotti `usersRolesService` (axios) e `usersRoles` store Pinia per stato condiviso cross-component.
  - Integrata navigazione sidebar/quick actions verso modulo reale (sostituito fallback coming-soon per users).
  - Estese localizzazioni IT/EN con namespace `usersRoles` per testi UI e feedback operazioni.
  - Definiti endpoint attesi frontend: `GET/POST/PUT/DELETE /users` e `GET/POST/PUT/DELETE /roles`.
- Consequences:
  - Frontend pronto per gestione utenti/ruoli senza ulteriori refactor strutturali.
  - Se backend espone contratti diversi dagli endpoint assunti, sara necessaria mappatura nel service.

## ADR-0011 - Users/Roles OpenAPI contract alignment
- Date: 2026-06-15
- Status: Accepted
- Context: verifica endpoint OpenAPI live conferma disponibilita `/users`, `/users/{id}`, `/roles`, `/roles/{id}` con DTO request dedicati.
- Decision:
  - Allineati i payload frontend ai DTO OpenAPI: `CreateUserData/UpdateUserData` (`userName`, `password`, `roles`) e `CreateRoleData/UpdateRoleData` (`name`).
  - Rimossi dal modulo utenti/ruoli i campi UI non presenti nel contratto (`email`, `isActive`, `description`).
  - Introdotta normalizzazione robusta dei payload di risposta per gestire shape non tipizzate nello schema OpenAPI.
  - Aggiornati dizionari i18n e binding vista/componente coerentemente ai nuovi campi.
- Consequences:
  - Il frontend invia richieste conformi al contratto API corrente.
  - Azioni edit/delete vengono disabilitate se la risposta runtime non fornisce un identificativo utilizzabile.
