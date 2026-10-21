# Architecture diagrams

Set di diagrammi della fase di architettura. Ogni decisione presa nel dossier è leggibile qui come forma.

Preferenza di lettura ereditata dall'analisi: diagram-first.

## Index

1. [Phase flow](#1-phase-flow)
2. [Position in the lifecycle](#2-position-in-the-lifecycle)
3. [Stack at a glance](#3-stack-at-a-glance)
4. [Deployable units and data ownership](#4-deployable-units-and-data-ownership)
5. [Module and layering shape](#5-module-and-layering-shape)
6. [Write path](#6-write-path)
7. [Search path](#7-search-path)
8. [Identity and authorization path](#8-identity-and-authorization-path)
9. [Environments and promotion](#9-environments-and-promotion)
10. [Canonical model, frozen](#10-canonical-model-frozen)
11. [Data sources, actors and output types](#11-data-sources-actors-and-output-types)
12. [Management services per step](#12-management-services-per-step)
13. [Service data flows grouped by deployment](#13-service-data-flows-grouped-by-deployment)
14. [Gaps](#14-gaps)

## 1. Phase flow

```mermaid
flowchart LR
  IN["Analisi chiusa: requisiti, modello, confini"] --> S["Stack decision"]
  S --> C["Technical constraints"]
  C --> P["Pattern decisions"]
  P --> E["Deployment environments"]
  E --> D["Architecture diagrams"]
  D --> H["Handoff"]
  H --> V{"Verdetto"}
  V -- "pronto per lo sviluppo" --> DEV["Sviluppo"]
  V -- "criterio non soddisfatto" --> S
  V -- "requisito non chiaro" --> A["Analisi"]
```

## 2. Position in the lifecycle

```mermaid
flowchart LR
  SC["Scouting"] --> CY["Ciclo di elicitazione"]
  CY --> AN["Analisi"]
  CY --> PR["Prototipo, non eseguito"]
  AN --> AR["Architettura, questa fase"]
  AR --> SV["Sviluppo"]
  AR -. "decisione violata" .-> AR
  AR -. "requisito ambiguo" .-> AN
```

Il prototipo non è stato eseguito: l'esperienza congelata qui è quella del sistema in produzione, non un esito di esplorazione.

## 3. Stack at a glance

Caption: ogni slot risolto e il criterio che lo ha scelto. Il profilo completo è in [StackProfile](StackProfile.md) (StackProfile.md).

```mermaid
flowchart TB
  subgraph RUNTIME["Runtime"]
    NET[".NET 10 / C#"]
    C8["Client .NET 8"]
    PY["Client Python 3.10+"]
  end
  subgraph SURFACES["Superfici"]
    GRPC["gRPC 2.80"]
    REST["REST + OpenAPI + Scalar"]
    SPA["Vue 3 + Naive UI + ECharts"]
  end
  subgraph APP["Applicazione"]
    HIK["Hikyaku 9.4.3"]
    KAI["Kaido + RabbitMQ 9.4.7"]
    MAP["MapZilla 9.1.5"]
  end
  subgraph DATA["Dati"]
    ST["Jigen.Store append-only + WAL"]
    HNSW["Jigen.Indexer.HNSW + SQ8"]
    ID["Identity + OpenIddict + SQLite"]
  end
  subgraph AI["Calcolo dei vettori"]
    ONNX["ONNX Runtime 1.27"]
    TOK["ML.Tokenizers + HuggingFace"]
    CV["OpenCvSharp5"]
  end
  C8 --> GRPC
  PY --> GRPC
  SPA --> REST
  GRPC --> HIK
  REST --> HIK
  HIK --> MAP
  HIK --> ST
  HIK --> ID
  HIK --> HNSW
  HIK --> ONNX
  HIK -. "delega" .-> KAI
  KAI --> ONNX
  ONNX --> TOK
  ONNX --> CV
  ST --> HNSW
```

## 4. Deployable units and data ownership

Caption: le unità distribuibili, i dati posseduti e le copie derivate dichiarate. Un solo proprietario per dato.

```mermaid
flowchart LR
  subgraph U1["U1 Server: container Jigen"]
    S_OWN["Possiede: catalogo dei database, voci, record, associazioni, grafo, utenti, ruoli, accessi"]
    S_REST["REST + gRPC"]
    S_IDX["Indice HNSW per collezione"]
  end
  subgraph U2["U2 Lavoratore di embedding: container Jigen.Embedding"]
    W_OWN["Possiede: profili di embedding configurati"]
    W_CALC["Calcolo testo, passaggi, immagine, tile"]
  end
  subgraph U3["U3 Interfaccia amministrativa: SPA"]
    UI_OWN["Possiede: stato di sessione nel browser"]
  end
  subgraph U4["U4 Libreria: pacchetti NuGet"]
    L_OWN["Possiede: nessun dato condiviso; archivio nel processo chiamante"]
  end
  subgraph U5["U5 Broker: RabbitMQ"]
    B_OWN["Possiede: code delle richieste, nessun dato di dominio"]
  end
  U3 -->|"REST + token"| S_REST
  U4 -->|"gRPC"| S_REST
  U1 -->|"richieste di vettori"| B_OWN
  B_OWN -->|"richieste di vettori"| W_CALC
  W_CALC -->|"vettori"| B_OWN
  S_IDX -->|"legge i vettori posseduti dall'archivio"| S_OWN
  U3 -. "copia derivata: nessuna, solo letture" .-> S_REST
```

Copie derivate dichiarate: i campioni di stato (calcolati dal server), i risultati calibrati nel client (calcolati dai risultati già ottenuti), il nome utente dentro l'accesso al database (copia dell'identità).

## 5. Module and layering shape

Caption: quattro progetti per modulo, dipendenze a senso unico, composizione a runtime.

```mermaid
flowchart LR
  HOST["Host: Jigen, Jigen-AllInOne, Jigen.Embedding"] --> API["API: controller, endpoint"]
  HOST --> HANDLERS["Handlers: implementazioni"]
  HOST --> CORE["Core: comandi, query, contratti"]
  API --> CORE
  HANDLERS --> CORE
  API -. "vieta" .-> HANDLERS
  HANDLERS -. "vieta" .-> API
  CORE -. "non conosce" .-> HANDLERS
```

Regola: un modulo non referenzia il progetto interno di un altro modulo; parla solo con i contratti di `Core` o con il proprio host. La composizione avviene a runtime tramite il caricatore dei moduli.

## 6. Write path

Caption: dal chiamante alla durabilità, con i punti in cui può fallire.

```mermaid
flowchart LR
  C["Chiamante: REST, gRPC, libreria"] --> V{"Contratto valido"}
  V -- "no" --> E400["Rifiuto 400"]
  V -- "sì" --> M["Messaggio di comando"]
  M --> H["Handler"]
  H --> L{"Lock del database"}
  L -- "no" --> E423["Rifiuto: database in uso"]
  L -- "sì" --> WAL["Registro di scrittura"]
  WAL --> Q["Coda di ingesta"]
  Q --> W["Thread scrittore"]
  W --> F["File dei contenuti e dei vettori"]
  W --> I["Indice HNSW"]
  WAL --> CK["Checkpoint periodico"]
  CK --> T["Registro troncato"]
```

## 7. Search path

```mermaid
flowchart LR
  Q["Query: vettore o testo"] --> T{"Testo"}
  T -- "sì" --> EM{"Calcolo locale o delegato"}
  EM -- "locale" --> O1["ONNX Runtime nel processo"]
  EM -- "delegato" --> K["Kaido su RabbitMQ"]
  K --> O2["Lavoratore ONNX"]
  O1 --> V["Vettore della query"]
  O2 --> V
  T -- "no" --> V
  V --> S["Indice della collezione"]
  S --> R{"Ricerca esatta o approssimata"}
  R -- "approssimata" --> G["Visita del grafo con larghezza efSearch"]
  R -- "esatta" --> B["Confronto con tutte le voci"]
  G --> RR["Ripristino esatto dei punteggi"]
  RR --> F{"Filtro"}
  F -- "sì" --> FL["Predicato sulle proprietà della voce"]
  F -- "no" --> T2["Top k"]
  FL --> T2
  B --> T2
  T2 --> M{"Raggruppamento per record condiviso"}
  M -- "sì" --> MG["Un risultato per record, budget di candidati"]
  M -- "no" --> OUT["Risultati con chiave, contenuto e punteggio"]
  MG --> OUT
```

## 8. Identity and authorization path

```mermaid
flowchart LR
  U["Utente o applicazione"] --> L{"Credenziali o token"}
  L -- "assenti" --> U401["401"]
  L -- "presenti" --> O["OpenIddict: token"]
  O --> I["Identità risolta"]
  I --> A{"Ruolo amministrativo"}
  A -- "sì" --> OK["Accesso al database"]
  A -- "no" --> D{"Accesso al database"}
  D -- "assente" --> U404["404 sul database esistente"]
  D -- "presente" --> OK
  OK --> OP["Operazione richiesta"]
```

Il rifiuto per database non autorizzato è `404`, non `403`: il database non associato non è osservabile. La regola è decisa una volta e vale su entrambe le superfici.

## 9. Environments and promotion

```mermaid
flowchart LR
  DEV["Sviluppo locale: dotnet run + Vite dev server"] --> CI["CI: build, test, pacchetti"]
  CI --> BENCH["Banco di confronto: compose con Qdrant, Milvus, pgvector"]
  CI --> IMG["Immagini: server, lavoratori, all-in-one"]
  BENCH --> STAGE["Ambiente di prova: stessi container, dati di prova"]
  IMG --> STAGE
  STAGE --> PROD["Produzione: container, volume dati, TLS"]
```

## 10. Canonical model, frozen

```mermaid
erDiagram
  DATABASE ||--o{ COLLECTION : "contiene"
  COLLECTION ||--o{ ENTRY : "raggruppa"
  ENTRY }o--|| RECORD : "referenzia"
  ENTRY ||--o| ASSOCIATION : "ha"
  COLLECTION ||--o{ GRAPH_NODE : "indicizza"
  USER ||--o{ ACCESS : "riceve"
  ROLE }o--o{ USER : "assegna"
  DATABASE ||--o{ ACCESS : "concede"
  PROFILE ||--o{ ENTRY : "produce vettori per"
  SAMPLE }o--|| DATABASE : "misura"
```

Il modello è quello dell'analisi, congelato. Il grafo resta separato dalla voce: possiede nodi e adiacenze e legge i vettori dall'archivio.

## 11. Data sources, actors and output types

Caption: le fonti dell'architettura, chi le produce e i tipi di output.

```mermaid
flowchart LR
  AN["Analisi: requisiti, modello, confini"]
  CODE["Codice e configurazione al commit b706e06"]
  SCO["Scouting: vincoli e non-goal"]
  HUM["Attore umano: maintainer e amministratore"]
  AGT["Attore LLM: architetto"]
  SYS["Attore di sistema: build e test"]
  DOS["Output: dossier di architettura"]
  DIA["Output: set di diagrammi"]
  SUM["Output: sintesi di fase"]
  SP["Output: Stack Profile"]
  UX["Output: riferimento all'esperienza congelato"]
  AN -->|"Criteri e requisiti"| AGT
  CODE -->|"Vincoli di fatto e lacune"| AGT
  SCO -->|"Non-goal"| AGT
  HUM -->|"Preferenze e verdetto"| AGT
  SYS -->|"Esiti di build e test"| AGT
  AGT -->|"Decisioni e vincoli"| DOS
  AGT -->|"Forme e flussi"| DIA
  AGT -->|"Sintesi"| SUM
  AGT -->|"Slot e versioni"| SP
  AGT -->|"Superfici congelate"| UX
  DOS -->|"Contratto per lo sviluppo"| HUM
```

## 12. Management services per step

Caption: il flusso logico della fase, i dati scambiati e il percorso delle deviazioni.

```mermaid
flowchart LR
  IN["Analisi chiusa e codice corrente"] -->|"Criteri"| S["Risolvere lo stack"]
  S -->|"Slot e versioni"| C["Fissare i vincoli"]
  C -->|"Soglie e divieti"| P["Scegliere le forme"]
  P -->|"Pattern"| E["Definire gli ambienti"]
  E -->|"Promozione e segreti"| D["Disegnare il set"]
  D -->|"Diagrammi"| H["Verdetto"]
  H -->|"Esito"| OUT["Dossier, Stack Profile, diagrammi, sintesi"]
  S -->|"Slot senza tecnologia"| GAP["Lacune dichiarate"]
  C -->|"Vincolo non verificabile"| GAP
  P -->|"Forma senza criterio"| GAP
  GAP -->|"Correzione"| S
  H -->|"Decisione violata dallo sviluppo"| P
```

## 13. Service data flows grouped by deployment

Caption: le unità distribuite, i flussi e le copie derivate. Include le tre topologie decise.

```mermaid
flowchart LR
  CL["Client .NET o Python"]
  subgraph LIB["Topologia 1: libreria nel processo"]
    L1["Archivio embedded"]
    L2["Indice per collezione"]
    L3["Calcolo ONNX locale, facoltativo"]
    L3 -->|"vettori"| L1
    L1 -->|"voci"| L2
  end
  subgraph AIO["Topologia 2: container completo"]
    A1["REST + gRPC"]
    A2["Archivio"]
    A3["Indice"]
    A4["Calcolo ONNX"]
    A1 -->|"comandi e query"| A2
    A2 -->|"voci"| A3
    A1 -->|"testo"| A4
    A4 -->|"vettori"| A1
  end
  subgraph SPLIT["Topologia 3: server e lavoratori separati"]
    B1["REST + gRPC"]
    B2["Archivio"]
    B3["Indice"]
    B1 -->|"comandi e query"| B2
    B2 -->|"voci"| B3
  end
  subgraph WORK["Lavoratori di embedding"]
    W1["Calcolo ONNX"]
  end
  subgraph MQ["Broker RabbitMQ"]
    M1["Coda delle richieste"]
  end
  CL -->|"gRPC o REST"| A1
  CL -->|"gRPC o REST"| B1
  CL -->|"chiamate dirette"| L1
  B1 -->|"richieste di vettori"| M1
  M1 -->|"richieste di vettori"| W1
  W1 -->|"vettori"| M1
```

## 14. Gaps

| Lacuna | Effetto | Dove si chiude |
| --- | --- | --- |
| La SPA non è confezionata in `wwwroot`: la cartella contiene solo un segnaposto | La superficie S-04 non è distribuita con il server | T-13 e T-14 |
| Il provider `Microsoft.Extensions.VectorData` non esiste nel repository | R-30 non è soddisfatto oggi | T-12 |
| Le tre rotte di menu dell'interfaccia non hanno un corrispondente nel server | Il menu dipende da chiamate che falliscono | T-14 |
| La superficie REST `search-filter` ignora il filtro | Nessun requisito la copre | T-08: rimozione decisa |
| Nessuna verifica di accessibilità | L'obiettivo WCAG 2.2 AA non è misurato | T-14 |
| Il registro di scrittura non è configurato dal server | La durabilità di R-12 dipende dalla configurazione | T-02 e T-13, con K-06 che lo attiva per default |
