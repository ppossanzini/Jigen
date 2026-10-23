# Scouting diagrams

Set di diagrammi del giro di scouting sulla struttura su disco dei vettori sparsi. Ogni diagramma è richiamato dalla sezione del dossier che lo possiede.

## 1. Phase flow

```mermaid
flowchart LR
  IN["Richiesta"] --> PF["Problem framing"]
  PF --> AI["Assumption interrogation"]
  AI --> SS["Solution scouting"]
  SS --> AS["Analogue solutions"]
  AS --> TF["Technical feasibility"]
  TF --> UP["Unknowns plan"]
  UP --> OE["Order of magnitude estimate"]
  OE --> D["Scouting diagrams"]
  D --> H["Recommendation and handoff"]
  H --> R{"Verdetto"}
  R -- "procedi con scope ridotto" --> CY["Ciclo di elicitazione"]
  R -- "fermati" --> ST["Stop"]
```

## 2. Position in the lifecycle

```mermaid
flowchart LR
  I["Idea: pesi lessicali per voce"] --> S["Scouting"]
  S -- "raccomandazione" --> E["Ciclo di elicitazione"]
  E --> P["Prototype"]
  E --> A["Analysis"]
  P --> A
  A --> AR["Architecture"]
  S -. "vincoli e prove" .-> P
  S -. "domande aperte" .-> A
```

## 3. Data sources, actors and output types

```mermaid
flowchart LR
  CH["Chiamante: coppie termine/peso"] -- "vettore sparso" --> W["Thread di scrittura"]
  W -- "record in append" --> SF["File dei segmenti"]
  W -- "voce nel manifest" --> MF["Manifest"]
  OP["Apertura del database"] -- "legge" --> MF
  OP -- "mappa" --> SF
  Q["Query: coppie termine/peso"] -- "termini" --> SE["Ricerca sulle liste"]
  SE -- "legge" --> SF
  SE -- "chiavi e punteggi" --> OUT["Risultati ordinati"]
  W -- "tombstone" --> MF
```

## 4. Management services per step

```mermaid
flowchart LR
  A1["Accettare le coppie"] -- "coppie validate" --> A2["Scrivere il segmento"]
  A2 -- "segmento immutabile" --> A3["Aggiornare il manifest"]
  A3 -- "numero di segmenti" --> A4["Aprire il database"]
  A4 -- "segmenti mappati" --> A5["Cercare per termine"]
  A5 -- "candidati" --> A6["Ordinare e troncare"]
  A6 -- "top-k" --> A7["Restituire"]
  A2 -- "voce sovrascritta" --> T["Tombstone nel manifest"]
  T -- "spazio morto" --> C["Compattazione periodica"]
  C -- "nuovo segmento" --> A3
```

## 5. Problem and outcome frame

```mermaid
flowchart TB
  B["Destinatario: sviluppatore embedded, amministratore"] --> P["Problema: nessun formato su disco che riapra in tempo costante senza copie"]
  P --> O1["Esito: apertura indipendente dalla dimensione"]
  P --> O2["Esito: una sola copia delle coppie"]
  P --> O3["Esito: spazio delle liste misurato e inferiore al grezzo"]
  P --> O4["Esito: ricerca per termine senza scansione"]
  K1["Vincolo dato: nessuna ricostruzione all'apertura"] --> P
  K2["Vincolo dato: nessuna duplicazione"] --> P
  K3["Vincolo dato: un solo scrittore"] --> P
  K4["Vincolo dato: nessun servizio esterno"] --> P
  K5["Vincolo dato: licenza permissiva"] --> P
  K6["Vincolo dato: indice denso invariato"] --> P
  N["Fuori ambito: pesi nel server, fusione denso/sparso, compressione e potatura nella prima consegna"] --> P
```

## 6. Capability and risk map

```mermaid
flowchart LR
  C1["C-1 Registrare le coppie<br/>rischio noto"] --> C2["C-2 Aprire senza ricostruzione<br/>rischio ignoto"]
  C2 --> C3["C-3 Trovare per termine<br/>rischio noto"]
  C2 --> C4["C-4 Aggiornare e cancellare<br/>rischio noto"]
  C3 --> C5["C-5 Contenere lo spazio<br/>rischio ignoto"]
  C3 --> C6["C-6 Potare i candidati<br/>rischio ignoto, rimandato"]
  SL["StoredList<br/>file, indice, flush, compattazione"] -.-> C1
  SL -.-> C2
  SL -.-> C4
  SL2["Dizionario termine verso indice<br/>assente: da costruire"] -.-> C3
  style C2 fill:#ffe0b2,color:#000000,stroke:#e65100,stroke-width:2px
```

## 7. Adopt-extend-build map

```mermaid
flowchart LR
  F["Formato delle coppie"] --> B1["build"]
  S["Scrittura in append con tombstone"] --> B2["build"]
  C["Compressione delle liste"] --> B3["build, dopo la misura dello spazio"]
  P["Potatura dei candidati"] --> B4["build, dopo la misura della latenza"]
  S2["Ricerca esatta sulle liste"] --> B5["build"]
  SL["File, indice, flush, compattazione"] --> E1["extend: StoredList"]
  TD["Indirizzamento per termine"] --> B6["build"]
  K["Vincoli: scrittore unico, memory-mapped, licenza permissiva"] -.-> B1
  K -.-> B2
  K -.-> B5
```

## 8. Question and proof map

```mermaid
flowchart LR
  Q1["Q-1 Cardinalità del vettore sparso"] --> PR5["Pr-5: domanda all'utente"]
  Q2["Q-2 Spazio con e senza compressione"] --> PR2["Pr-2: misura dei due file"]
  Q3["Q-3 Latenza della ricerca esatta"] --> PR3["Pr-3: 100 termini su 1 milione di coppie"]
  Q4["Q-4 Formato del record"] --> D["Chiusa in Solution scouting"]
  A1["A-1 Apertura costante"] --> PR1["Pr-1: apertura a 10.000 e 1.000.000 di coppie"]
  A3["A-3 Append senza riscritture"] --> PR4["Pr-4: sovrascrittura, cancellazione, riapertura"]
  PR5 --> B1["Decide la forma del record"]
  PR1 --> B2["Decide la struttura dei file"]
  PR4 --> B3["Decide tombstone e compattazione"]
  PR2 --> B4["Decide la compressione"]
  PR3 --> B5["Decide la potatura"]
```
