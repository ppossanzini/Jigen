# Scouting diagrams

Preferenza di lettura: diagram-first. I diagrammi usano il tema predefinito del renderer. Nessuna palette personalizzata, quindi il contrasto non è misurato: va verificato nel tema di visualizzazione adottato.

## Index

1. [Phase flow](#1-phase-flow)
2. [Position in the lifecycle](#2-position-in-the-lifecycle)
3. [Problem and outcome frame](#3-problem-and-outcome-frame)
4. [Capability and risk map](#4-capability-and-risk-map)
5. [Adopt-extend-build map](#5-adopt-extend-build-map)
6. [Question and proof map](#6-question-and-proof-map)
7. [Data sources, actors and output types](#7-data-sources-actors-and-output-types)
8. [Management services per step](#8-management-services-per-step)
9. [Service data flows grouped by deployment](#9-service-data-flows-grouped-by-deployment)

## 1. Phase flow

Caption: i passi della fase in ordine, la sezione che ciascuno scrive e i tre esiti della raccomandazione.

```mermaid
flowchart LR
  IN["Idea o opportunità"] --> D20["Problem framing"]
  D20 --> D30["Assumption interrogation"]
  D30 --> D40["Solution scouting"]
  D40 --> D45["Analogue solutions"]
  D45 --> D50["Technical feasibility"]
  D50 --> D60["Unknowns plan"]
  D60 --> D70["Order of magnitude estimate"]
  D70 --> D80["Recommendation and handoff"]
  D80 --> GATE{"Raccomandazione"}
  GATE -- "procedere" --> GO["Ciclo di elicitazione: prototipo e analisi"]
  GATE -- "ridurre lo scope" --> RED["Taglio dello scope e nuovo giro"]
  GATE -- "fermare" --> STOP["L’idea si ferma qui"]
```

## 2. Position in the lifecycle

Caption: dove sta la fase di scouting, cosa consegna e quale uscita la chiude.

```mermaid
flowchart LR
  S["Scouting"] --> C{"Primo passaggio"}
  C -- "prima il prototipo" --> P["Prototipo"]
  C -- "prima l’analisi" --> A["Analisi"]
  P -. "esplorare di nuovo" .-> A
  A -. "domanda aperta" .-> P
  A -- "analisi completa" --> AR["Architettura"]
  AR --> D["Sviluppo"]
  S --> EXIT["Uscita: raccomandazione, dossier e diagrammi"]
```

## 3. Problem and outcome frame

Caption: beneficiario, problema, esito osservabile, vincoli dati o assunti e non-goal correnti.

```mermaid
flowchart LR
  BEN["Beneficiario: team .NET che costruisce ricerca semantica"] --> PRB["Problema: servizio separato oppure dati fuori dal processo"]
  PRB --> OUT["Esito: ricerca su 100.000 vettori in pochi ms, stesso processo o un container"]
  CG1["Vincolo dato: .NET 10, Apache-2.0, ONNX locale"] --> PRB
  CG2["Vincolo dato: un solo processo per database"] --> PRB
  CA1["Vincolo assunto: controllo locale dei dati"] --> PRB
  CA2["Vincolo assunto: embedding locali competitivi"] --> PRB
  PRB --> NG["Non-goal: nessun cluster, nessun SQL, filtro a uguaglianza"]
```

## 4. Capability and risk map

Caption: le capacità, il rischio dominante di ciascuna marcato noto o ignoto, e la capacità che decide l’esito.

```mermaid
flowchart LR
  IDEA["Proposta: ricerca semantica embedded e servibile in .NET"] --> K1["K1 Conservare contenuti e vettori"]
  IDEA --> K2["K2 Cercare per similarità"]
  IDEA --> K3["K3 Generare embedding"]
  IDEA --> K4["K4 Esporre lo store in rete"]
  IDEA --> K5["K5 Integrare gli ecosistemi"]
  IDEA --> K6["K6 Raggruppare e calibrare i risultati"]
  IDEA --> K7["K7 Distribuire e aggiornare"]
  K1 -- "rischio noto: spazio e compattazione" --> R1["Noto"]
  K2 -- "rischio ignoto: recall su dati reali" --> R2["Ignoto"]
  K3 -- "rischio ignoto: qualità e GPU" --> R3["Ignoto"]
  K4 -- "rischio ignoto: sicurezza e scala" --> R4["Ignoto"]
  K5 -- "rischio ignoto: conformance MEVD" --> R5["Ignoto"]
  K6 -- "rischio noto: nessun top-k esatto" --> R6["Noto"]
  K7 -- "rischio ignoto: release e licenze" --> R7["Ignoto"]
  K2 --> CRIT["Capacità critica: decide l’esito"]
```

## 5. Adopt-extend-build map

Caption: la decisione corrente per ogni capacità e il vincolo che la limita.

```mermaid
flowchart LR
  K1["K1 Conservare contenuti e vettori"] --> A1["Costruire"]
  A1 --> C1["Vincolo: scrittore singolo, nessuna compattazione automatica"]
  K2["K2 Cercare per similarità"] --> A2["Estendere HNSW"]
  A2 --> C2["Vincolo: filtro a uguaglianza, top-k approssimato"]
  K3["K3 Generare embedding"] --> A3["Adottare ONNX e costruire la pipeline"]
  A3 --> C3["Vincolo: pesi non inclusi, validazione GPU aperta"]
  K4["K4 Esporre lo store in rete"] --> A4["Adottare ASP.NET Core e costruire i moduli"]
  A4 --> C4["Vincolo: credenziali di default, gRPC in chiaro"]
  K5["K5 Integrare gli ecosistemi"] --> A5["Costruire i client ed estendere MEVD"]
  A5 --> C5["Vincolo: provider MEVD non upstream"]
  K6["K6 Raggruppare e calibrare i risultati"] --> A6["Costruire"]
  A6 --> C6["Vincolo: grouped search su una collezione"]
  K7["K7 Distribuire e aggiornare"] --> A7["Adottare NuGet e Docker, costruire le immagini"]
  A7 --> C7["Vincolo: release non automatizzata"]
```

## 6. Question and proof map

Caption: ogni domanda sopravvissuta, la decisione che blocca, la prova minima che la chiude e il momento in cui va eseguita.

```mermaid
flowchart LR
  Q1["P1: performance server-to-server"] --> B1["Blocca: priorità del canale server"]
  Q2["P2: recall su dati reali"] --> B2["Blocca: configurazione di default"]
  Q3["P3: qualità degli embedding locali"] --> B3["Blocca: ragione d’uso degli embedding integrati"]
  Q4["P4: adozione esterna"] --> B4["Blocca: ampiezza dello scope"]
  Q5["P5: postura di sicurezza"] --> B5["Blocca: claim di produzione"]
  Q6["P6: conformance MEVD"] --> B6["Blocca: percorso di adozione .NET"]
  Q7["P7: operatori di filtro richiesti"] --> B7["Blocca: roadmap del filtro"]
  B1 --> P1["Prova: adapter Jigen server nel benchmark comparativo"]
  B2 --> P2["Prova: recall@10 su 100.000 embedding reali"]
  B3 --> P3["Prova: nDCG@10 contro API ospitate"]
  B4 --> P4["Prova: statistiche e cinque interviste"]
  B5 --> P5["Prova: checklist di sicurezza e test di autorizzazione"]
  B6 --> P6["Prova: conformance suite sul fork locale"]
  B7 --> P7["Prova: issue e cinque interviste"]
  P1 --> W1["Prima della stima"]
  P2 --> W1
  P3 --> W1
  P4 --> W1
  P5 --> W1
  P6 --> W2["Nel ciclo di elicitazione"]
  P7 --> W2
```

## 7. Data sources, actors and output types

Caption: le fonti usate dall’analisi, gli attori che le producono o le leggono e i tipi di output della fase.

```mermaid
flowchart LR
  DOC["Documentazione in docs/ e README"]
  CODE["Codice, test e benchmark"]
  WEB["README ufficiali dei prodotti analoghi"]
  STACK["StackProfile e vincoli di progetto"]
  HUM["Attore umano: maintainer e team"]
  AGT["Attore LLM: agente di analisi"]
  SYS["Attore di sistema: build, test e benchmark"]
  DOS["Output: dossier di scouting"]
  DIA["Output: set di diagrammi"]
  SUM["Output: sintesi di fase"]
  OQ["Output: domande aperte"]
  DOC -->|"Evidenze di prodotto e limiti"| AGT
  CODE -->|"Capacità realizzate e lacune"| AGT
  WEB -->|"Pratiche attese e limiti documentati"| AGT
  STACK -->|"Vincoli di stack e licenza"| AGT
  HUM -->|"Obiettivi e preferenze di lettura"| AGT
  SYS -->|"Esiti misurati"| AGT
  AGT -->|"Conclusioni e decisioni"| DOS
  DOS -->|"Mappa della fase"| DIA
  DOS -->|"Sintesi di una pagina"| SUM
  AGT -->|"Q1–Q10 e P1–P7"| OQ
  OQ -->|"Richieste di chiarimento"| HUM
```

## 8. Management services per step

Caption: il flusso logico della fase, con le responsabilità di gestione, i dati scambiati e il percorso delle domande senza risposta.

```mermaid
flowchart LR
  IN["Documentazione, codice, benchmark e fonti esterne"] -->|"Materiale da leggere"| E["Raccolta e lettura delle evidenze"]
  E -->|"Fatti e citazioni"| F["Framing del problema"]
  F -->|"Problema, vincoli, non-goal"| I["Interrogazione delle ipotesi"]
  I -->|"Ipotesi falsificabili"| S["Ricerca di soluzioni e analoghi"]
  S -->|"Decisioni adottare, estendere, costruire"| T["Valutazione di fattibilità"]
  T -->|"Rischi noti e ignoti"| U["Piano delle prove"]
  U -->|"Domande e prove"| M["Stima per capacità"]
  M -->|"Intervallo e incertezza"| R["Raccomandazione"]
  R -->|"Esito esplicito"| OUT["Dossier, diagrammi e sintesi"]
  I -->|"Domande senza risposta"| GAP["Domande aperte"]
  T -->|"Rischio non chiuso"| GAP
  GAP -->|"Q1–Q10 e P1–P7"| U
  S -->|"Candidato rifiutato"| GAP
```

## 9. Service data flows grouped by deployment

Caption: le unità di deployment documentate e i flussi tra esse. Il diagramma rappresenta la documentazione corrente, non una nuova decisione.

```mermaid
flowchart LR
  CLIENT["Client .NET o Python"]
  subgraph APP["Processo dell’applicazione .NET"]
    STORE["Store embedded"]
    IDX["Indice HNSW"]
    EMB["Generatore ONNX locale"]
    STORE -->|"Vettori da indicizzare"| IDX
    EMB -->|"Vettori calcolati"| STORE
  end
  subgraph AIO["Container jigendb-all-in-one"]
    SRV1["Server REST e gRPC"]
    DBM1["DatabasesManager"]
    EMBS["Modulo embedding in-process"]
    SRV1 -->|"Richieste di lettura e scrittura"| DBM1
    SRV1 -->|"Richieste di embedding"| EMBS
    EMBS -->|"Vettori calcolati"| DBM1
  end
  subgraph SRVDB["Container jigendb"]
    SRV2["Server REST e gRPC"]
    DBM2["DatabasesManager"]
    SRV2 -->|"Richieste di lettura e scrittura"| DBM2
  end
  subgraph WRK["Container jigen-embeddings"]
    W["Worker ONNX"]
  end
  subgraph MQ["Container RabbitMQ"]
    B["Broker Kaido"]
  end
  CLIENT -->|"gRPC 3223 e REST 13223"| SRV1
  CLIENT -->|"gRPC 3223 e REST 13223"| SRV2
  SRV2 -->|"Richieste di embedding"| B
  B -->|"Richieste di embedding"| W
  W -->|"Vettori calcolati"| B
  B -->|"Vettori calcolati"| SRV2
```

## Gaps

- Il diagramma 9 non mostra la UI amministrativa Jigen Insight, fuori scope documentale.
- Nessun diagramma mostra l’adozione esterna: non esistono dati di telemetria o di utilizzo.
- Il diagramma 6 copre le domande sopravvissute dopo l’interrogazione. Q6 e Q10 restano nel ciclo di elicitazione e non hanno una prova pianificata in questa fase.
- Il contrasto dei colori non è misurato. I diagrammi non usano palette personalizzate.
