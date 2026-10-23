# Scouting: sintesi

**Decisione confermata.** Implementare Block-Max MaxScore per incrementi, senza quantizzare i float. Il primo incremento riguarda il blocco persistente di posting su `StoredList`; algoritmo e integrazione nello Store seguono separatamente. Vedi [Recommendation and handoff](dossier.md#recommendation-and-handoff).

Il codice specifico appartiene al progetto `Jigen.Index.Sparse`; gli indici e i
relativi file sono per collezione. `Jigen.Primitives` conserva `StoredList`.

## Ambito

Dentro: struttura su disco dei vettori sparsi, apertura del database, ricerca per termine.

Fuori: calcolo dei pesi, fusione con il punteggio denso, compressione, quantizzazione e superfici di rete.

## Risultati principali

- La causa del comportamento precedente è doppia: le coppie esistevano in tre forme e a ogni apertura lo `Store` rileggeva l'intero log delle posizioni sparse. Entrambe sono ora vincoli espliciti in [Problem framing](dossier.md#problem-framing) (dossier.md#problem-framing).
- Il costo di apertura con `StoredList` dipende dal numero di termini e di blocchi. Con blocchi a capacità fissa cresce anche con le coppie archiviate: conteggio in [Costo di apertura](dossier.md#costo-di-apertura) (dossier.md#costo-di-apertura).
- La capacità critica è C-2, l'apertura senza ricostruzione: [Capability and risk map](diagrams.md#6-capability-and-risk-map) (diagrams.md#6-capability-and-risk-map).
- Il modello a segmenti immutabili con tombstone è la forma usata dai motori che non ricostruiscono nulla all'apertura: [Analogue solutions](dossier.md#analogue-solutions) (dossier.md#analogue-solutions).
- Lo strato di file si riusa da `StoredList`, già impiegato dal grafo HNSW; l'indirizzamento per termine va costruito: [Solution scouting](dossier.md#solution-scouting) (dossier.md#solution-scouting).
- Nessuna dipendenza esterna è adottabile per compressione o potatura in .NET.
- Il confronto dei paper distingue liste per termine con block-max, blocchi per intervallo di documenti come BMP e finestre con accessi sequenziali come SINDI. SEISMIC usa gruppi per similarità; SeismicWave aggiunge un grafo; DSP una gerarchia di superblocchi. Vedi [Paper su prestazioni e struttura dei dati](dossier.md#paper-su-prestazioni-e-struttura-dei-dati).
- Milvus espone più algoritmi di ricerca sparsa; questo non dimostra un layout fisico comune. Qdrant dichiara l'indice esatto senza approssimazione: [Analogue solutions](dossier.md#analogue-solutions) (dossier.md#analogue-solutions).

## Rischi bloccanti

| Rischio | Stato | Prova |
| --- | --- | --- |
| L'apertura torna a crescere con i dati | aperto: dipende dalla cardinalità degli elementi di `StoredList` | [Pr-1](dossier.md#unknowns-plan) (dossier.md#unknowns-plan) |
| Lo spazio delle liste supera il budget | aperto | [Pr-2](dossier.md#unknowns-plan) (dossier.md#unknowns-plan) |
| Tombstone e compattazione producono duplicati nei risultati | aperto | [Pr-4](dossier.md#unknowns-plan) (dossier.md#unknowns-plan) |

## Domande aperte

1. Un solo vettore sparso per voce o più di uno, coerente con i vettori densi? Vedi [Assumption interrogation](dossier.md#assumption-interrogation) (dossier.md#assumption-interrogation), A-6.
2. La ricerca esatta basta alla latenza richiesta, o serve la potatura? Vedi [Pr-3](dossier.md#unknowns-plan) (dossier.md#unknowns-plan).
3. Quale layout offre meno letture a cache fredda sullo stesso corpus? Le accelerazioni pubblicate non dimostrano prestazioni su SSD né apertura senza ricostruzione. Vedi [Paper su prestazioni e struttura dei dati](dossier.md#paper-su-prestazioni-e-struttura-dei-dati).

## Prossime azioni

| Azione | Attore |
| --- | --- |
| Approvare il codice del primo incremento, poi verificare il nuovo blocco | utente, poi sviluppo |
| Definire cursori e directory, poi implementare e confrontare la ricerca esatta | sviluppo con decisioni dell'utente |
| Verificare apertura e aggiornamenti prima di chiudere l'integrazione | sviluppo |

Dettaglio completo in [dossier](dossier.md) (dossier.md) e [Scouting diagrams](diagrams.md) (diagrams.md). Il terzo overview è omesso: nessuna decisione di deployment in questa fase.
