# Sparse vectors — scouting summary

**Raccomandazione: procedere con lo scope ridotto.** Vettore sparso calcolato dal chiamante, memorizzato sulla stessa voce del vettore denso, cercato in modo esatto, con fusione denso più sparso per posizione. Fuori scope: tokenizzazione e BM25 nel server, generazione dei pesi da modelli, indice approssimato per gli sparsi.

## Scope

Dentro lo scope:

- Secondo slot vettoriale sulla voce, con nome distinto, coppie (termine, peso).
- Ricerca per vettore sparso con top-k, filtro e raggruppamento per record condiviso.
- Fusione denso e sparso in un ordinamento unico, con pesi dichiarati.
- Parità fra API in-process, REST e binaria; client .NET e Python; workbench e osservazione.
- Indice ricostruibile dall'archivio, coerente con tombstone e compattazione.

Fuori scope:

- Analizzatore, statistiche di corpus e BM25 nel server.
- SPLADE, ELSER e i pesi lessicali di BGE-M3 generati da Jigen.
- Indice approssimato per gli sparsi e quantizzazione degli impatti in questo ciclo.
- Multi-vettore e ColBERT.
- Somma diretta dei punteggi densi e sparsi.

## Risultati principali

- Tutti i prodotti esaminati rappresentano lo sparsa come coppie (dimensione, peso), lo cercano con il prodotto scalare e lo indicizzano con un indice invertito, mai con un grafo. Vedi [Analogue solutions](dossier.md#analogue-solutions) (dossier.md#analogue-solutions).
- Qdrant è il modello di contratto più vicino: vettore con nome, pesi dal chiamante, fusione `rrf` o `dbsf`. Weaviate, Milvus, Elasticsearch, Vespa e LanceDB calcolano invece i pesi nel server, con analizzatore e statistiche.
- La fusione per posizione è la scelta comune: non confronta punteggi di scale diverse. La calibrazione esistente resta per le modalità diverse, non per denso contro sparso.
- La potatura dei candidati cambia la garanzia: senza potatura la ricerca è esatta, con potatura no. Con un filtro attivo la potatura non è valida.
- Lucene.NET è stato valutato e rifiutato per questo slot: porta di Lucene 4.8, senza Block-Max WAND, con un ciclo di commit proprio che duplicherebbe la durabilità.
- La capacità critica è S2, l'indice dei termini: decide memoria, tempi di costruzione e usabilità. Vedi [Capability and risk map](diagrams.md#4-capability-and-risk-map) (diagrams.md#4-capability-and-risk-map).
- La stima è di 42–69 giornate-uomo, ±20%, contro un residuo complessivo stimato in precedenza di 39–98: è una seconda struttura di indice, non un ritocco. Vedi [Order of magnitude estimate](dossier.md#order-of-magnitude-estimate) (dossier.md#order-of-magnitude-estimate).

## Rischi e domande aperte

- U1: memoria e latenza dell'indice a 100.000 e 1.000.000 di voci. Decide il limite dichiarabile.
- U3: correttezza della potatura con filtro. Un risultato mancante non è osservabile dal chiamante.
- U4: la fusione migliora la qualità misurabile? Senza un insieme etichettato non si può dire.
- U5: chi usa i vettori sparsi e per quale caso? I concorrenti che li offrono non sono una prova di domanda.
- A-1: senza pesi calcolati dal server il prodotto non può dichiarare «ricerca full-text», solo «pesi lessicali forniti dal chiamante». Va scritto nella documentazione.

## Prossime azioni

| Azione | Attore |
| --- | --- |
| Prova U2: riapertura di un archivio scritto prima dell'estensione del formato | Sviluppo |
| Prova U1: banco di costruzione e ricerca dell'indice dei termini a 100k e 1M | Sviluppo |
| Prova U3: potatura contro ricerca esaustiva, con filtro | Sviluppo |
| Prova U4: nDCG su insieme etichettato con pesi SPLADE o BGE-M3 | Maintainer |
| Chiudere l'incremento di analisi: requisiti, modello, casi d'uso, pacchetti | Analisi |
| Chiudere l'incremento di architettura: slot, vincoli, forme, diagrammi | Architettura |

## Diagrammi di riferimento

- [Competitor techniques](diagrams.md#6-competitor-techniques) (diagrams.md#6-competitor-techniques)
- [Who computes the weights](diagrams.md#7-who-computes-the-weights) (diagrams.md#7-who-computes-the-weights)
- [Service data flows grouped by deployment](diagrams.md#11-service-data-flows-grouped-by-deployment) (diagrams.md#11-service-data-flows-grouped-by-deployment): incluso.

Il dossier completo è in [Sparse vectors — scouting dossier](dossier.md) (dossier.md). La raccomandazione e le condizioni che la cambierebbero sono in [Recommendation and handoff](dossier.md#recommendation-and-handoff) (dossier.md#recommendation-and-handoff).
