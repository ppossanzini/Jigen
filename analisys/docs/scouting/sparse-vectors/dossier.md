# Sparse vectors — scouting dossier

Giro di scouting dedicato all'inserimento dei vettori sparsi in Jigen, con la ricerca sulle tecniche adottate dai prodotti concorrenti. Input: codice al commit `b706e06`, [Analisi](../../analysis/dossier.md) (analysis/dossier.md) e [Architettura](../../architecture/dossier.md) (architecture/dossier.md) del giro 2.

## Scouting diagrams

Preferenza di lettura: diagram-first. Il set completo è in [Scouting diagrams](diagrams.md) (diagrams.md).

Indice:

1. [Phase flow](diagrams.md#1-phase-flow) (diagrams.md#1-phase-flow)
2. [Position in the lifecycle](diagrams.md#2-position-in-the-lifecycle) (diagrams.md#2-position-in-the-lifecycle)
3. [Problem and outcome frame](diagrams.md#3-problem-and-outcome-frame) (diagrams.md#3-problem-and-outcome-frame)
4. [Capability and risk map](diagrams.md#4-capability-and-risk-map) (diagrams.md#4-capability-and-risk-map)
5. [Adopt-extend-build map](diagrams.md#5-adopt-extend-build-map) (diagrams.md#5-adopt-extend-build-map)
6. [Competitor techniques](diagrams.md#6-competitor-techniques) (diagrams.md#6-competitor-techniques)
7. [Who computes the weights](diagrams.md#7-who-computes-the-weights) (diagrams.md#7-who-computes-the-weights)
8. [Question and proof map](diagrams.md#8-question-and-proof-map) (diagrams.md#8-question-and-proof-map)
9. [Data sources, actors and output types](diagrams.md#9-data-sources-actors-and-output-types) (diagrams.md#9-data-sources-actors-and-output-types)
10. [Management services per step](diagrams.md#10-management-services-per-step) (diagrams.md#10-management-services-per-step)
11. [Service data flows grouped by deployment](diagrams.md#11-service-data-flows-grouped-by-deployment) (diagrams.md#11-service-data-flows-grouped-by-deployment)

```mermaid
flowchart LR
  IN["Richiesta: inserire vettori sparsi"] --> PF["Problem framing"]
  PF --> AI["Assumption interrogation"]
  AI --> SS["Solution scouting"]
  SS --> AS["Analogue solutions"]
  AS --> TF["Technical feasibility"]
  TF --> UP["Unknowns plan"]
  UP --> OE["Estimate"]
  OE --> D["Diagrammi"]
  D --> H["Recommendation and handoff"]
  H --> V{"Verdetto"}
  V -- "procedi con scope ridotto" --> CY["Ciclo di elicitazione"]
```

Diagrammi non applicabili: nessuno. Lacune del set: vedi [Scouting diagrams](diagrams.md#12-gaps) (diagrams.md#12-gaps).

## Problem framing

**Problema.** La ricerca densa risponde alla similarità semantica, non al termine esatto. Chi cerca un codice, un nome proprio o un identificatore non ottiene il documento che lo contiene, perché l'embedding non conserva l'occorrenza letterale. Il rimedio noto è un secondo vettore, sparso, che rappresenta i termini con un peso, cercato con il prodotto scalare sulle sole dimensioni condivise.

**Per chi.** Lo sviluppatore che usa l'engine embedded o le superfici di rete, e l'amministratore che prova una ricerca dal workbench.

**Esito osservabile.** Con una voce indicizzata e una query che contiene un suo termine, la voce compare nei risultati; con denso e sparso insieme, l'ordine unico non seppellisce sistematicamente una delle due modalità.

**Vincoli non negoziabili.**

| Vincolo | Origine | Cosa impone |
| --- | --- | --- |
| Un solo scrittore per database | Requisito R-01 | L'indice dei termini si aggiorna nella stessa disciplina dell'indice denso |
| Nessun servizio esterno nella modalità embedded | Scouting 1 | L'indice dei termini vive nel processo |
| Licenza permissiva | Scouting 1 | Nessuna dipendenza incompatibile con Apache-2.0 |
| Parità fra superfici | Requisito R-21 | Le stesse operazioni su API in-process, REST e binaria |
| Il modello canonico non si duplica | Analisi, giro 2 | Il vettore sparso è una forma di Vettore, non una seconda entità |

**Fuori scope, per decisione dell'utente.**

- Calcolare i pesi nel server: nessun analizzatore, nessuna statistica di corpus, nessuna funzione BM25.
- Generare vettori sparsi da modelli: SPLADE, ELSER o i pesi lessicali di BGE-M3 restano del chiamante. La documentazione del prodotto dichiara già che BGE-M3 espone solo il vettore denso.
- Sostituire o affiancare all'indice approssimato denso un indice approssimato per gli sparsi.
- Multi-vettore per colonna, ColBERT e quantizzazione avanzata degli impatti.

**Decisione.** Il problema è reale e la tecnica è standard. Restano da verificare costo dell'indice e qualità della fusione: sono le domande del piano delle incognite.

## Assumption interrogation

| Id | Assunzione | Come si falsifica | Stato |
| --- | --- | --- | --- |
| A-1 | Accettare i pesi dal chiamante basta a soddisfare il bisogno | Un utente chiede ricerca testuale senza fornire pesi | Aperta: il prodotto non potrà dire «ricerca full-text», solo «pesi lessicali forniti dal chiamante». Va scritto nella documentazione. |
| A-2 | Una voce può portare due vettori senza rompere il modello | Una struttura dati, un indice o una superficie che assume un solo vettore per voce | Falsa oggi: `VectorEntry` ha un solo `Embedding`, il record su disco ha una sola dimensione per collezione, i contratti hanno un solo campo. La modifica è necessaria e va tracciata. |
| A-3 | L'indice dei termini non è un secondo proprietario del dato | Un dato che esiste solo nell'indice e non nell'archivio | Vera se l'indice è ricostruibile dall'archivio, come il grafo. |
| A-4 | La ricerca sparsa resta esatta | La potatura dei candidati scarta una voce che sarebbe entrata nel top-k | Falsa con la potatura: la potatura rende la ricerca approssimata. Va dichiarata, oppure la potatura si applica solo senza filtro e con esito verificato. |
| A-5 | La fusione non richiede calibrazione | Due punteggi di scale diverse sommati direttamente | Vera solo per la fusione per posizione. La somma dei punteggi richiede normalizzazione e non è prevista. |
| A-6 | Il costo di memoria è accettabile | Misura oltre la soglia dichiarata a 100.000 e 1.000.000 di voci | Aperta: U1. |
| A-7 | La domanda esiste | Nessun uso dichiarato, nessuna richiesta | Aperta: eredità di P4 dello scouting 1. Il fatto che i concorrenti lo offrano non è una prova di domanda. |
| A-8 | Il formato del registro si può estendere senza rompere gli archivi | La riapertura di un archivio scritto dalla versione precedente fallisce | Vera solo se il formato è versionato e la migrazione è dichiarata. Oggi il registro ha un formato unico e non c'è una seconda versione. |

## Solution scouting

**Criterio di rifiuto, definito prima del confronto.** Un candidato è rifiutato se: richiede un servizio esterno nella modalità embedded; non gira in-process su .NET 10; impone un proprio ciclo di commit che duplica la durabilità dell'archivio; non può essere ricostruito dall'archivio; ha licenza incompatibile con la ridistribuzione.

| Capacità | Decisione | Soluzione | Vincolo attuale | Giustificazione |
| --- | --- | --- | --- | --- |
| S1 Conservare il vettore sparso | extend | Archivio append-only, registro di scrittura, riconciliazione esistenti; nuovo record e nuova versione del formato | Formato del registro unico, nessuna versione | La durabilità deve restare una sola strada: un secondo meccanismo di commit sarebbe un secondo proprietario |
| S2 Indicizzare i termini | build | Indice dei termini proprietario su file mappati e `StoredList` | Nessuna struttura invertita nel repository | Lucene.NET è una porta di Lucene 4.8, senza potatura WAND, con un proprio ciclo di commit e un proprio formato: duplicherebbe la durabilità e non sarebbe riconciliabile con l'archivio |
| S3 Cercare per vettore sparso | build | Prodotto scalare su elenchi ordinati per peso, con potatura per limite superiore | Nessuna astrazione per query sparse: `IIndexer` accetta solo `float[]` | L'astrazione densa non esprime una query sparsa; serve un contratto parallelo |
| S4 Fondere denso e sparso | adopt | RRF, la fusione per posizione usata da Qdrant, Weaviate e Milvus | La calibrazione esistente fonde gruppi con lo z-score | L'RRF non confronta punteggi di scale diverse: è l'unica fusione che non richiede normalizzazione |
| S4 Fondere denso e sparso | extend | `Jigen.Calibration` per le modalità diverse | Fonde gruppi, non due scale della stessa collezione | Resta lo strumento per testo contro immagine, non per denso contro sparso |
| S5 Esporre | extend | Protobuf, REST, client .NET e Python, workbench | Un solo campo vettoriale nei contratti | La parità fra superfici è già un requisito |

**Candidati esaminati e rifiutati.**

| Candidato | Esito | Motivo |
| --- | --- | --- |
| Lucene.NET | rifiutato per l'indice sparso | Porta di Lucene 4.8, ancora in serie beta, Apache-2.0. La potatura Block-Max WAND non è nella 4.8; il ciclo di commit e il formato dell'indice sono propri e non si riconciliano con l'archivio. Resta candidato solo se entrasse in scope l'analisi del testo nel server. |
| Tantivy | rifiutato | Nessun binding .NET mantenuto; introdurrebbe un secondo runtime nativo accanto a ONNX. |
| BM25 nel server | rifiutato in questo ciclo | Richiede analizzatore, statistiche di corpus e normalizzazione della lunghezza; cambia la natura del prodotto da motore vettoriale a motore di ricerca testuale. |
| Motore esterno (Qdrant, Milvus, Weaviate) | rifiutato | Viola il vincolo della modalità embedded del primo scouting. |
| Indice approssimato per gli sparsi | rifiutato | Gli sparsi non hanno una metrica geometrica utile all'approssimazione; il guadagno viene dalla potatura esatta, non dal grafo. |

**Ambiti di ricerca.** Librerie: coperto. Servizi ospitati: valutati e rifiutati. Asset interni: coperto, `StoredList` e i file mappati sono le primitive riusate. Skill disponibili: nessuna copre l'indice invertito.

**Decisione.** Costruire l'indice dei termini e la ricerca sparsa; estendere archivio e superfici; adottare l'RRF per la fusione.

## Analogue solutions

Le tecniche adottate dai prodotti che già offrono ricerca lessicale o ibrida. Ogni scheda dichiara rappresentazione, inserimento, indice, punteggio, fusione e la lezione per Jigen.

| Prodotto | Rappresentazione e inserimento | Indice | Punteggio | Fusione |
| --- | --- | --- | --- | --- |
| Qdrant | Coppie `indices`/`values`, indici unici, nessuna dimensione dichiarata; il vettore sparso è un vettore con nome, quindi coesiste con il denso sulla stessa voce | Indice invertito separato, con opzioni `on_disk`, `datatype` (`uint8`, `float16`, `float32`) e `full_scan_threshold` | Prodotto scalare; la ricerca sull'indice invertito è esatta | `prefetch` di più query e fusione `rrf` o `dbsf` |
| Milvus 2.5 | Il server trasforma il testo in vettore sparso con una funzione BM25 su un campo analizzato; accetta anche vettori sparsi espliciti | Indice invertito con analizzatori; potatura WAND | BM25, oppure prodotto interno su vettori espliciti | Ibrido con RRF |
| Weaviate | Il testo è indicizzato e pesato dal server; il vettore sparso non è il contratto principale | Indice invertito | BM25 con parametri `k1`, `b`, tokenizzazione, stopword | `alpha` più `relativeScoreFusion` (predefinita) o `rankedFusion`, cioè RRF |
| Vespa | Tensori sparsi e campi di testo nel documento | Indice testuale con `weakAnd` e `wand` | BM25 o espressione di ranking arbitraria | Fusione dentro il profilo di ranking |
| Elasticsearch, OpenSearch | Campo `sparse_vector` con coppie termine/peso, prodotto da ELSER o SPLADE al momento dell'indicizzazione, con potatura dei termini poco significativi | Indice invertito Lucene con impatti | Somma dei pesi dei termini in comune | RRF o combinatore lineare |
| pgvector | Tipo `sparsevec`, massimo 1.000 elementi non nulli, inserito come valore | HNSW o IVFFlat con operatori dedicati agli sparsi | Distanza L2, prodotto interno, coseno | Nessuna fusione integrata: si compone in SQL |
| LanceDB | BM25 su una colonna di testo, indice creato a parte | Indice full-text; le righe aggiunte dopo non sono indicizzate finché non si esegue l'ottimizzazione, e fino ad allora si legge con una scansione | BM25 | Reranker con RRF |

**Che cosa fanno tutti allo stesso modo.**

- Rappresentano il vettore sparso come coppie (dimensione, peso) con dimensioni uniche.
- Cercano con il prodotto scalare sulle sole dimensioni condivise.
- Usano un indice invertito, mai un grafo: la dimensione vettoriale non ha una geometria da approssimare.
- Fondono denso e sparso per posizione, oppure normalizzando i punteggi prima di sommarli.

**Dove differiscono, e la lezione per Jigen.**

| Differenza | Chi sta da una parte | Lezione |
| --- | --- | --- |
| Chi calcola i pesi | Qdrant dal chiamante; Weaviate, Milvus, Elasticsearch, Vespa e LanceDB dal server | La scelta del chiamante elimina analizzatore e statistiche di corpus, e con essi l'intera superficie di errore testuale. È lo scope scelto. |
| Esattezza dichiarata | Qdrant dichiara la ricerca sparsa esatta e offre una soglia sotto la quale non usa l'indice | La potatura è un'ottimizzazione che va dichiarata come perdita di esattezza, non nascosta. |
| Precisione degli impatti | Qdrant permette `uint8` e `float16` dentro l'indice, conservando i valori originali su disco | La memoria dell'indice si governa con la precisione degli impatti, senza perdere il valore restituito. |
| Indice in memoria o su disco | Qdrant lo tiene in memoria per default e offre `on_disk` | A un milione di voci la scelta non è un dettaglio: è la differenza fra un prodotto usabile e uno che non parte. |
| Indice aggiornato o differito | LanceDB richiede un'ottimizzazione esplicita e nel frattempo legge in scansione | L'aggiornamento incrementale è un requisito di prodotto, non un dettaglio realizzativo. |
| Dimensione del vettore sparso | pgvector impone un massimo di 1.000 elementi non nulli | Un limite dichiarato è preferibile a un decadimento imprevedibile. |

**Che cosa i concorrenti non offrono e Jigen può offrire.** Un solo processo, nessun servizio esterno, archivio su file con durabilità propria, e un indice dei termini ricostruibile dall'archivio. Qdrant è il modello di contratto più vicino: vettore con nome, pesi dal chiamante, fusione per posizione.

**Fonti.** [Qdrant — vettori](https://qdrant.tech/documentation/manage-data/vectors/) (qdrant.tech), [Qdrant — hybrid and multi-stage queries](https://qdrant.tech/documentation/search/hybrid-queries/) (qdrant.tech), [Qdrant — corso sugli indici invertiti](https://qdrant.tech/course/essentials/day-3/sparse-vectors/) (qdrant.tech), [Milvus — BM25 function](https://milvus.io/docs/bm25-function.md) (milvus.io), [Milvus 2.5 — full-text search](https://zilliz.com/blog/milvus-2-5-built-in-full-text-search-advanced-query-optimization-and-more) (zilliz.com), [Weaviate — hybrid search](https://docs.weaviate.io/weaviate/concepts/search/hybrid-search) (docs.weaviate.io), [Vespa — WAND](https://docs.vespa.ai/en/ranking/wand.html) (docs.vespa.ai), [Elasticsearch — sparse vector field](https://www.elastic.co/docs/reference/elasticsearch/mapping-reference/sparse-vector) (elastic.co), [OpenSearch — neural sparse](https://opensearch.org/blog/a-deep-dive-into-faster-semantic-sparse-retrieval-in-os-2-12/) (opensearch.org), [pgvector](https://github.com/pgvector/pgvector) (github.com), [LanceDB — full-text search](https://docs.lancedb.com/search/full-text-search) (docs.lancedb.com), [Lucene.NET](https://lucenenet.apache.org/) (lucenenet.apache.org).

**Limite dichiarato della ricerca.** Per Milvus e Vespa la scheda si basa sulla documentazione e sui blog citati, non su una prova eseguita. Nessun confronto di prestazioni è stato misurato: le cifre non sono riportate perché non verificate su questa macchina.

## Technical feasibility

**Capacità, rischio dominante e criticità.**

| Id | Capacità | Rischio dominante | Criticità |
| --- | --- | --- | --- |
| S1 | Conservare il vettore sparso per voce | Estensione del formato del registro e migrazione degli archivi esistenti | Bassa, se il formato è versionato |
| S2 | Indicizzare i termini | Memoria e tempo di costruzione a un milione di voci, con un indice per collezione ricostruito all'apertura | **Critica**: decide se la funzione è usabile |
| S3 | Cercare per vettore sparso | Potatura e filtro insieme: il filtro rompe il limite superiore su cui la potatura si appoggia | Alta: correttezza prima della velocità |
| S4 | Fondere denso e sparso | Scale non confrontabili se si sommano i punteggi | Bassa con la fusione per posizione |
| S5 | Esporre su superfici, client e interfaccia | Parità fra canali e compatibilità dei contratti | Media: lavoro largo ma noto |

**Capacità critica.** S2. Senza un indice dei termini che stia in memoria e si costruisca in tempi accettabili, le altre capacità non servono a nulla.

**Forma realizzabile, senza scegliere la tecnologia.**

- Il vettore sparso è un secondo slot vettoriale della voce: coppie (termine, peso), termini unici e ordinati, pesi non negativi con almeno un peso maggiore di zero.
- Il record su disco aggiunge una sezione variabile alla voce; il registro di scrittura la trasporta; il formato porta un numero di versione e la riapertura di un archivio precedente è dichiarata.
- L'indice dei termini mappa ogni termine all'elenco delle voci che lo contengono, con il peso, ordinato per peso decrescente; cancellazioni come tombstone; ricostruzione dall'archivio all'apertura, come per il grafo.
- La ricerca somma i pesi delle sole dimensioni condivise; senza potatura è esatta; con la potatura salta gli elenchi il cui limite superiore non può entrare nel top-k, e questo va dichiarato come perdita di esattezza.
- Con un filtro attivo, la potatura si disattiva: il limite superiore non è valido su un insieme filtrato.
- La fusione prende due elenchi già ordinati e produce un ordine unico per posizione, con pesi dichiarati e parità risolte in modo deterministico.
- L'osservazione espone termini distinti, elenchi, lunghezza media degli elenchi, memoria occupata e tombstone.

**Cosa si riusa e cosa manca.**

| Elemento | Stato |
| --- | --- |
| Archivio append-only, registro di scrittura, checkpoint, riconciliazione | Esistono: si estendono |
| `StoredList` con persistenza incrementale | Esiste: è la primitiva naturale per l'indice dei termini |
| Astrazione dell'indice | Esiste ma accetta solo `float[]`: serve un contratto parallelo per le query sparse |
| Dimensione unica per collezione | Esiste: il vettore sparso non può stare nel file dei vettori densi, perché non ha una dimensione |
| Contratti gRPC e REST | Un solo campo vettoriale: vanno estesi con un secondo slot e una query sparsa |
| Client .NET e Python, workbench | Nessuna nozione di vettore sparso: vanno estesi |
| Potatura dei candidati | Non esiste per nessuna struttura: va costruita e verificata |

**Cosa rende la funzione più costosa del previsto.** La potatura non è un'ottimizzazione locale: cambia la garanzia di esattezza e interagisce con il filtro. La sua assenza è accettabile in un primo rilascio; la sua presenza sbagliata produce risultati mancanti silenziosi, che sono peggio di una ricerca lenta.

## Unknowns plan

| Id | Incognita | Perché conta | Prova minima | Costo |
| --- | --- | --- | --- | --- |
| U1 | Memoria e latenza dell'indice dei termini a 100.000 e 1.000.000 di voci | Decide se l'indice sta in memoria e quale sia il limite dichiarabile | Banco nel progetto di benchmark: costruzione, memoria residente, ricerca su query da 5, 20 e 100 termini | 3–5 giorni |
| U2 | Costo dell'estensione del formato | Un archivio esistente che non si riapre è una rottura | Test di andata e ritorno: archivio scritto prima, riaperto dopo, voci e vettori intatti | 2–3 giorni |
| U3 | Correttezza della potatura con filtro | Un risultato perso non è osservabile dal chiamante | Confronto fra ricerca potata e ricerca esaustiva sullo stesso filtro, su dati con distribuzione nota | 3–4 giorni |
| U4 | Qualità della fusione | Senza misura, la funzione è una scommessa | nDCG su un insieme etichettato: denso da solo contro ibrido, con pesi dichiarati | 3–5 giorni |
| U5 | Domanda reale | Un prodotto in più senza uso è costo puro | Interviste e uso dichiarato, eredità di P4 dello scouting 1 | 2–3 giorni |
| U6 | Fedeltà delle schede su Milvus e Vespa | Le schede dei concorrenti guidano le scelte | Lettura delle fonti primarie indicate e correzione delle schede | 1 giorno |

**Ordine.** U2 prima di tutto il resto, perché senza formato compatibile il resto non è distribuibile. Poi U1, che decide il limite. Poi U3 e U4, che decidono le garanzie. U5 non blocca la costruzione, ma blocca il posizionamento del prodotto.

## Order of magnitude estimate

| Voce | Giornate-uomo | Nota |
| --- | --- | --- |
| S1 Archivio, registro, formato e migrazione | 6–10 | Include U2 |
| S2 Indice dei termini, riconciliazione, compattazione | 12–20 | Include U1; è la voce che decide |
| S3 Ricerca sparsa, filtro, raggruppamento, potatura | 8–12 | Include U3 |
| S4 Fusione e pesi | 3–6 | Include U4 |
| S5 Superfici, client, workbench, osservazione | 7–11 | Parità fra canali |
| Test, banchi e misura del richiamo | 6–10 | Estensione dei banchi esistenti |
| **Totale** | **42–69** | Incertezza dichiarata: ±20% |

**Come varia.**

- Senza potatura, e con dichiarazione di ricerca esatta: 38–63.
- Con l'indice solo su disco, se U1 mostrasse una memoria insostenibile: 46–75.
- Con BM25 calcolato nel server, che è fuori scope: oltre 90, per analizzatore, statistiche e normalizzazione.

**Confronto con la stima del primo scouting.** Il lavoro residuo era stimato in 39–98 giornate. Questa funzione da sola ne vale 42–69: è una seconda struttura di indice, non un'estensione marginale. Va inserita come incremento separato, non come completamento del ciclo corrente.

## Recommendation and handoff

**Raccomandazione: procedere con lo scope ridotto dichiarato.**

Dentro lo scope:

- Vettore sparso calcolato dal chiamante, memorizzato sulla stessa voce del vettore denso, con nomi distinti.
- Ricerca per vettore sparso esatta, con top-k, filtro e raggruppamento per record condiviso.
- Fusione denso e sparso in un ordinamento unico, per posizione, con pesi dichiarati.
- Parità fra API in-process, superficie REST e superficie binaria; client .NET e Python; workbench e osservazione dell'indice.
- Ricostruzione dell'indice dall'archivio, tombstone e compattazione coerenti con l'indice denso.

Fuori dallo scope, con motivo:

| Esclusione | Motivo |
| --- | --- |
| Tokenizzazione e BM25 nel server | Cambia la natura del prodotto e aggiunge analizzatore e statistiche; nessun requisito lo chiede |
| Generazione di vettori sparsi da modelli | Il contratto di embedding dichiara BGE-M3 denso soltanto; i pesi restano del chiamante |
| Indice approssimato per gli sparsi | La potatura esatta domina; un grafo non aggiunge nulla alla metrica a prodotto scalare |
| Quantizzazione degli impatti in questo ciclo | Ottimizzazione successiva, da misurare prima |
| Multi-vettore e ColBERT | Nessun requisito; il multi-vettore è già coperto da più voci con record condiviso |
| Somma diretta dei punteggi densi e sparsi | Scale non confrontabili; richiede normalizzazione e una calibrazione per collezione |

**Condizioni che cambierebbero il verdetto.**

- Se U1 mostrasse una memoria insostenibile anche con l'indice su disco: procedere con la sola ricerca lessicale su collezioni piccole, con soglia dichiarata.
- Se U3 mostrasse risultati mancanti con il filtro: mantenere la potatura solo senza filtro, e dichiarare l'esattezza come condizionata.
- Se U5 non trovasse alcun uso dichiarato: fermare dopo U2, e conservare solo la capacità di conservare il vettore sparso senza ricerca.

**Passaggio al ciclo di elicitazione.**

- All'analisi: etichette del vocabolario (vettore sparso, termine, peso, indice dei termini, fusione), flussi, regole di validazione, requisiti con criterio, modello canonico con il secondo slot vettoriale, casi d'uso, tracciabilità e pacchetti.
- All'architettura: slot dello stack per l'indice dei termini, vincoli verificabili su formato, esattezza, memoria e parità, forme per l'indice invertito e la fusione, aggiornamento del profilo dello stack e dei diagrammi.
- Allo sviluppo: nessun task eseguibile prima che analisi e architettura incrementali siano chiuse.

**Non da riaprire.** Il vincolo del singolo scrittore, la modalità embedded, il modello canonico a una entità per concetto, la parità fra superfici e l'uso di RRF per la fusione per posizione.

**Domande aperte, in ordine di impatto.**

1. U1: quanto costa l'indice a un milione di voci?
2. U5: chi lo usa e per quale caso?
3. U3: la potatura con filtro è corretta?
4. U4: la fusione migliora la qualità misurabile?
