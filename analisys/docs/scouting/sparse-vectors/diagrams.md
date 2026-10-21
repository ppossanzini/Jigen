# Sparse vectors — scouting diagrams

Set di diagrammi del giro di scouting dedicato ai vettori sparsi. Preferenza di lettura: diagram-first.

## Index

1. [Phase flow](#1-phase-flow)
2. [Position in the lifecycle](#2-position-in-the-lifecycle)
3. [Problem and outcome frame](#3-problem-and-outcome-frame)
4. [Capability and risk map](#4-capability-and-risk-map)
5. [Adopt-extend-build map](#5-adopt-extend-build-map)
6. [Competitor techniques](#6-competitor-techniques)
7. [Who computes the weights](#7-who-computes-the-weights)
8. [Question and proof map](#8-question-and-proof-map)
9. [Data sources, actors and output types](#9-data-sources-actors-and-output-types)
10. [Management services per step](#10-management-services-per-step)
11. [Service data flows grouped by deployment](#11-service-data-flows-grouped-by-deployment)
12. [Gaps](#12-gaps)

## 1. Phase flow

```mermaid
flowchart LR
  IN["Richiesta: inserire vettori sparsi"] --> PF["Problem framing"]
  PF --> AI["Assumption interrogation"]
  AI --> SS["Solution scouting"]
  SS --> AS["Analogue solutions: concorrenza"]
  AS --> TF["Technical feasibility"]
  TF --> UP["Unknowns plan"]
  UP --> OE["Order of magnitude estimate"]
  OE --> D["Diagrammi"]
  D --> H["Recommendation and handoff"]
  H --> V{"Verdetto"}
  V -- "procedi con scope ridotto" --> CY["Ciclo di elicitazione"]
  V -- "serve una prova" --> UP
  V -- "fermati" --> STOP["Nessun seguito"]
```

## 2. Position in the lifecycle

```mermaid
flowchart LR
  SC1["Scouting 1: riduzione dello scope"] --> CY["Ciclo di elicitazione"]
  CY --> AN["Analisi, giro 2"]
  AN --> AR["Architettura, giro 2"]
  AR --> SC2["Scouting 2: vettori sparsi, questo giro"]
  SC2 --> CY2["Nuovo ciclo: analisi e architettura incrementali"]
  CY2 --> DEV["Sviluppo"]
```

Il giro riapre un'esclusione del primo scouting: la ricerca ibrida con parole chiave era fuori scope per assenza di requisito. Il requisito ora esiste, quindi la decisione va rifatta.

## 3. Problem and outcome frame

```mermaid
flowchart LR
  WHO["Chi: sviluppatore di ricerca su .NET e amministratore"] --> PROB["Problema: la ricerca densa non trova il termine esatto"]
  PROB --> NEED["Serve una ricerca lessicale esatta, unita a quella semantica"]
  NEED --> OUT["Esito osservabile: un risultato che contiene il termine compare, e l'ordine unico non seppellisce nessuna delle due modalità"]
  NEED --> NON1["Non obiettivo: tokenizzare e pesare il testo nel server"]
  NEED --> NON2["Non obiettivo: generare vettori sparsi da modelli"]
  NEED --> NON3["Non obiettivo: sostituire l'indice approssimato denso"]
  OUT --> MET["Misura: richiamo lessicale su un insieme con termine noto, più nDCG della fusione contro la sola ricerca densa"]
```

## 4. Capability and risk map

Caption: le capacità, il rischio dominante di ciascuna e la capacità che decide l'esito.

```mermaid
flowchart TB
  S1["S1 Conservare il vettore sparso per voce"] -->|"rischio: formato del registro e migrazione"| R1["Basso se il formato è versionato"]
  S2["S2 Indicizzare i termini"] -->|"rischio: memoria e tempo di costruzione a un milione di voci"| R2["Alto: decide la fattibilità"]
  S3["S3 Cercare per vettore sparso"] -->|"rischio: potatura e filtri insieme"| R3["Medio-alto: correttezza della potatura"]
  S4["S4 Fondere denso e sparso"] -->|"rischio: scale non confrontabili"| R4["Basso: la fusione per posizione non confronta punteggi"]
  S5["S5 Esporre su superfici, client e interfaccia"] -->|"rischio: parità fra canali"| R5["Medio"]
  S2 --> CRIT["Capacità critica"]
  S3 --> CRIT
```

## 5. Adopt-extend-build map

```mermaid
flowchart LR
  S1["S1 Conservare"] --> AD1["extend: archivio append-only, registro e riconciliazione esistenti"]
  S2["S2 Indicizzare"] --> BD1["build: indice dei termini proprietario"]
  S2 --> RJ1["Lucene.NET rifiutato: porta di Lucene 4.8, senza potatura WAND, ciclo di commit proprio"]
  S2 --> AD2["adopt: StoredList e file mappati come primitive"]
  S3["S3 Cercare"] --> BD2["build: prodotto scalare su elenchi ordinati per peso"]
  S4["S4 Fondere"] --> AD3["adopt: RRF, documentato da Qdrant, Weaviate e Milvus"]
  S4 --> AD4["extend: calibrazione cross-modale esistente, per gruppi e non per due scale"]
  S5["S5 Esporre"] --> AD5["extend: protobuf, REST, client .NET e Python, workbench"]
```

## 6. Competitor techniques

Caption: come i prodotti affermati rappresentano, inseriscono e cercano i vettori sparsi. Dettaglio e fonti in [Analogue solutions](dossier.md#analogue-solutions) (dossier.md#analogue-solutions).

```mermaid
flowchart LR
  subgraph REP["Rappresentazione comune"]
    P["Coppie (indice, peso), indici unici e ordinati"]
  end
  subgraph IDX["Indice comune"]
    I["Indice invertito: termine verso elenco delle voci con peso"]
  end
  subgraph SCORE["Punteggio comune"]
    D["Prodotto scalare sulle sole dimensioni condivise"]
  end
  P --> I --> D
  D --> F1["Fusione per posizione: RRF"]
  D --> F2["Fusione per punteggio normalizzato"]
  F2 --> W["Weaviate: relativeScoreFusion e alpha"]
  F1 --> Q["Qdrant: prefetch, RRF e DBSF"]
  F1 --> M["Milvus: ibrido con RRF"]
  F1 --> E["Elasticsearch e OpenSearch: RRF"]
  subgraph SPECIAL["Varianti"]
    PG["pgvector: tipo dedicato, massimo 1000 elementi non nulli"]
    V["Vespa: tensore sparso più indice testuale, weakAND"]
    L["LanceDB: BM25 su colonna di testo, indice da ottimizzare"]
  end
  I --> V
  P --> PG
```

## 7. Who computes the weights

Caption: la linea di scope. Chi calcola i pesi decide se il server ha bisogno di un analizzatore e di statistiche di corpus.

```mermaid
flowchart LR
  subgraph CALLER["Pesi calcolati dal chiamante"]
    Q1["Qdrant: il client invia indici e valori"]
    J["Jigen, scope scelto"]
  end
  subgraph SERVER["Pesi calcolati dal server"]
    W1["Weaviate: BM25 dal testo indicizzato"]
    M1["Milvus: funzione BM25 su campo di testo"]
    E1["Elasticsearch: ELSER o SPLADE al momento dell'indicizzazione"]
    V1["Vespa: BM25 su indice testuale"]
    L1["LanceDB: BM25 sulla colonna"]
  end
  CALLER --> N1["Nessun analizzatore, nessuna statistica di corpus, nessuna deriva dell'IDF"]
  SERVER --> N2["Analizzatore, statistiche, normalizzazione della lunghezza, dizionario dei termini"]
```

## 8. Question and proof map

```mermaid
flowchart LR
  U1["U1 Costo dell'indice: memoria e latenza"] --> P1["Prova: banco con 100k e 1M voci, misure di memoria, costruzione e ricerca"]
  U2["U2 Costo del formato: registro e file"] --> P2["Prova: test di andata e ritorno con archivio della versione precedente"]
  U3["U3 Potatura con filtro"] --> P3["Prova: confronto fra ricerca potata e ricerca esaustiva sullo stesso filtro"]
  U4["U4 Qualità della fusione"] --> P4["Prova: nDCG su un insieme etichettato, denso contro ibrido"]
  U5["U5 Domanda reale"] --> P5["Prova: interviste e uso dichiarato, eredità di P4 dello scouting 1"]
  P1 --> D{"Decisione"}
  P3 --> D
  D -->|"entro soglia"| GO["Procedi"]
  D -->|"fuori soglia"| RED["Riduci: potatura assente, o indice solo su disco"]
```

## 9. Data sources, actors and output types

```mermaid
flowchart LR
  CODE["Codice: archivio, indice, superfici al commit b706e06"]
  DOCS["Documentazione del prodotto e nota su BGE-M3 denso"]
  WEB["Documentazione dei prodotti concorrenti"]
  HUM["Attore umano: maintainer e amministratore"]
  AGT["Attore LLM: agente di scouting"]
  O1["Output: dossier di scouting"]
  O2["Output: set di diagrammi"]
  O3["Output: sintesi con raccomandazione"]
  O4["Output: requisiti e pacchetti per l'analisi"]
  CODE -->|"Vincoli di fatto"| AGT
  DOCS -->|"Contratto attuale"| AGT
  WEB -->|"Tecniche e scelte dei concorrenti"| AGT
  HUM -->|"Scope scelto: pesi dal chiamante, stessa voce"| AGT
  AGT --> O1
  AGT --> O2
  AGT --> O3
  AGT --> O4
```

## 10. Management services per step

```mermaid
flowchart LR
  IN["Richiesta e codice"] -->|"Cosa manca"| F["Inquadrare il problema"]
  F -->|"Esito e non obiettivi"| A["Interrogare le assunzioni"]
  A -->|"Domande verificabili"| S["Cercare soluzioni adottabili"]
  S -->|"Adotta, estendi, costruisci"| AS["Leggere i concorrenti"]
  AS -->|"Tecniche e lezioni"| T["Valutare la fattibilità"]
  T -->|"Capacità e rischio"| U["Pianificare le prove"]
  U -->|"Prove minime"| O["Stimare l'ordine di grandezza"]
  O -->|"Intervallo"| D["Disegnare"]
  D -->|"Diagrammi"| H["Raccomandare"]
  H -->|"Verdetto"| OUT["Dossier, diagrammi, sintesi"]
  AS -->|"Nessuna fonte primaria"| GAP["Lacuna dichiarata"]
```

## 11. Service data flows grouped by deployment

Caption: dove si colloca l'indice dei termini rispetto alle unità già decise. Nessuna nuova unità distribuibile.

```mermaid
flowchart LR
  CL["Client .NET o Python: calcola i pesi"]
  subgraph SRV["Server o processo embedded"]
    ST["Archivio: voci, vettori densi, vettori sparsi"]
    DI["Indice denso per collezione"]
    SI["Indice dei termini per collezione"]
    FU["Fusione dei risultati"]
    ST -->|"voci indicizzabili"| DI
    ST -->|"termini e pesi"| SI
    SI -->|"risultati lessicali"| FU
    DI -->|"risultati semantici"| FU
  end
  CL -->|"voci con pesi"| ST
  CL -->|"query densa e query sparsa"| FU
  FU -->|"ordinamento unico"| CL
```

## 12. Gaps

| Lacuna | Effetto | Dove si chiude |
| --- | --- | --- |
| Nessuna misura di memoria e latenza dell'indice dei termini | La stima resta un intervallo ampio | U1 |
| Nessun insieme etichettato per misurare la fusione | La qualità della fusione non è verificabile oggi | U4 |
| Per Milvus e Vespa la fonte è la documentazione citata, non una prova eseguita | Le due schede descrivono il prodotto, non un confronto misurato | U6 |
| Il contratto di embedding dichiara BGE-M3 denso soltanto | I pesi lessicali vanno prodotti dal chiamante con un altro strumento | Scope scelto, nessuna modifica al server |
