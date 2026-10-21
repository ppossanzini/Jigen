# Scouting summary

**Raccomandazione: ridurre lo scope.** Il prossimo ciclo si concentra sull’engine embedded, sulla prova like-for-like, sulla recall e sulla qualità degli embedding su dati reali, e sulla baseline di sicurezza. Indice KMeans/IVF, espansione GPU, scala orizzontale, ricerca ibrida e offerta gestita escono dal ciclo.

## Scope

Dentro lo scope:

- Engine embedded .NET 10 con store append-only, WAL, shared metadata e indice HNSW.
- Topologia server con gRPC e REST, identità e metriche.
- Embedding locali con ONNX Runtime, testo e immagine, multi-modello.
- Client .NET, Python e provider Microsoft.Extensions.VectorData.
- Prova di performance like-for-like, recall e qualità degli embedding su dati reali.
- Baseline di sicurezza del server.

Fuori scope:

- Indice KMeans/IVF.
- Provider GPU oltre la validazione CUDA già avviata.
- Sharding e replica lato server.
- Ricerca ibrida con parole chiave.
- Documentazione della UI Jigen Insight.
- Client ulteriori, pannello di controllo e offerta gestita.

## Risultati principali

- Il problema e i non-goal sono fissati in [Problem framing](dossier.md#problem-framing) (dossier.md#problem-framing).
- La capacità critica è K2 Cercare per similarità. Il rischio dominante è il recall su embedding reali ai valori di default. Vedi [Capability and risk map](diagrams.md#4-capability-and-risk-map) (diagrams.md#4-capability-and-risk-map).
- Le decisioni adottare, estendere o costruire coprono sette capacità. Nessuna build è senza giustificazione. Vedi [Solution scouting](dossier.md#solution-scouting) (dossier.md#solution-scouting).
- Il benchmark attuale confronta un engine in-process con servizi remoti. Non sostiene il confronto server-to-server.
- Non esiste evidenza di adozione esterna. Download, issue e deploy non sono misurati.
- La stima residua è di 39–98 giornate-uomo, da due a cinque mesi per una persona, prove incluse. Vedi [Order of magnitude estimate](dossier.md#order-of-magnitude-estimate) (dossier.md#order-of-magnitude-estimate).

## Rischi bloccanti e domande aperte

- P1: il vantaggio di performance regge server-to-server? Blocca la priorità del canale server.
- P2: i default HNSW bastano su dati reali? Blocca la configurazione di default e il claim di qualità.
- P3: gli embedding locali sono competitivi con le API ospitate? Blocca la ragione d’uso degli embedding integrati.
- P4: qualcuno usa Jigen fuori dal maintainer e per quale motivo? Blocca l’ampiezza dello scope.
- P5: il server è pronto per la produzione? Blocca il claim di prontezza.
- Q1 e Q2: chi è il beneficiario e quale metrica misura l’adozione? Restano senza risposta. Vedi [Unknowns plan](dossier.md#unknowns-plan) (dossier.md#unknowns-plan).

## Prossime azioni

| Azione | Attore |
| --- | --- |
| Aggiungere l’adapter Jigen server al benchmark comparativo ed eseguire 10k e 100k | Maintainer |
| Misurare recall@10 su 100.000 embedding reali a tre valori di efSearch | Maintainer |
| Confrontare nDCG@10 tra modelli locali e API ospitate | Maintainer |
| Raccogliere statistiche NuGet, Docker e GitHub e cinque interviste | Maintainer |
| Verificare credenziali di default, TLS gRPC, interceptor e autorizzazione | Maintainer |
| Eseguire la conformance suite MEVD sul fork locale | Maintainer |
| Aprire il ciclo di elicitazione con il dossier come base | Analisi e prototipo |

## Diagrammi di riferimento

- [Data sources, actors and output types](diagrams.md#7-data-sources-actors-and-output-types) (diagrams.md#7-data-sources-actors-and-output-types)
- [Management services per step](diagrams.md#8-management-services-per-step) (diagrams.md#8-management-services-per-step)
- [Service data flows grouped by deployment](diagrams.md#9-service-data-flows-grouped-by-deployment) (diagrams.md#9-service-data-flows-grouped-by-deployment): incluso.

Il dossier completo è in [Scouting dossier](dossier.md) (dossier.md). La raccomandazione e il passaggio al ciclo sono in [Recommendation and handoff](dossier.md#recommendation-and-handoff) (dossier.md#recommendation-and-handoff).
