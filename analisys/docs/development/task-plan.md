# Task plan — vettori sparsi

Piano eseguibile dell'incremento R-36…R-41. I passi atomici, con la definizione di fatto e le trappole, sono in [Task list](tasklist.md) (tasklist.md). Riferimenti: [analisi](../analysis/dossier.md) (analysis/dossier.md), [architettura](../architecture/dossier.md) (architecture/dossier.md), [catalogo dei casi d'uso](../use-cases.md) (use-cases.md).

## Attori e coordinamento

| Voce | Valore |
| --- | --- |
| Attori | Uno: `A1` |
| Parallelismo | Nessuno. Una card alla volta, in ordine di onda |
| Area di lavoro | Un solo albero di lavoro; nessuna scrittura concorrente, nessun worktree |
| Revisione di base | `b706e06` |
| Gate di qualità | Dopo i passi di produzione di ogni card, `A1` si ferma e chiede approvazione del codice prima di scrivere o completare i test |
| Gate di verifica | Nessuna card si chiude senza il comando eseguito e il risultato riportato nel [dossier](dossier.md#task-execution-log) (dossier.md#task-execution-log) |
| Regola di deviazione | Una card che non può rispettare una decisione bloccata si ferma e torna all'architettura, senza variante locale |

## Regole comuni a tutte le card

- Lo sparso attraversa **un solo registro di scrittura**: nessun WAL separato, nessun secondo scrittore.
- La registrazione precede sempre coda e file; senza registro la scrittura con termini è rifiutata (K-44).
- Il formato del registro è versionato; un archivio precedente si apre e viene aggiornato (K-37).
- L'indice dei termini è ricostruibile dall'archivio: nessun dato esiste solo nell'indice (K-38).
- La ricerca è esatta per default; la potatura è opt-in e mai con un filtro (K-39).
- Un solo proprietario per dato: l'indice possiede termini ed elenchi e riferisce la chiave della voce.
- Ogni card dichiara le skill attivate; nessuna skill è applicata senza dichiarazione.
- Verifica minima di ogni card backend: `dotnet build Jigen.slnx -m:1`, poi il progetto di test indicato.

---

## Onda 1 — T-16.1 Vocabolario dello sparso

| Campo | Valore |
| --- | --- |
| Origine | T-16 *Conservare e indicizzare i vettori sparsi* |
| Requisiti | R-36, K-35, K-36 |
| Casi d'uso | UC-19 |
| Attore / stato / onda | A1 / `ready` / 1 |
| Skill | `base-be-base-rules`, `implementation-be-dotnet-jigen-store`, `implementation-be-dotnet-dev` |
| Dipendenze | Nessuna |

**Obiettivo.** Un valore immutabile che rappresenta un vettore sparso e una validazione unica, usabili da tutte le parti senza toccare la persistenza.

**Risultato osservabile.** Il tipo esiste, valida termini e pesi, e `VectorEntry` lo porta in memoria; nessun file cambia.

**Dentro.** Il tipo `SparseVector` con termini e pesi; la regola «termini unici e crescenti, pesi finiti e non negativi, almeno un peso maggiore di zero, al più `MaxTerms`»; l'opzione `SparseOptions.MaxTerms`; il campo `Sparse` su `VectorEntry`; l'accesso `HasSparseVector`.

**Fuori.** Serializzazione, registro, file, indice, superfici. Li introducono T-16.2 e seguenti.

**Percorsi.** `src/Jigen/Jigen.Primitives/DataStructures/SparseVector.cs` (nuovo), `src/Jigen/Jigen.Primitives/DataStructures/VectorEntry.cs`, `src/Jigen/Jigen.Store/StoreOptions.cs`, `tests/PrimitiveTests/`.

**Incremento.** Un tipo con la sua validazione e i suoi test. Una voce con solo sparso è rappresentabile in memoria ma non ancora scrivibile.

**Passi.**

1. Definire `SparseVector` con `ReadOnlyMemory<int> Terms` e `ReadOnlyMemory<float> Weights`, più un costruttore che valida e una factory da coppie.
2. Esporre la validazione come metodo statico riusabile, così il registro e la superficie REST rifiutano con la stessa regola.
3. Aggiungere `SparseOptions` con `MaxTerms` e collegarlo a `StoreOptions` senza leggerlo altrove.
4. Aggiungere `Sparse` a `VectorEntry`, con distinzione fra «assente» e «vuoto» documentata nel tipo.
5. Test: termini duplicati, non ordinati, peso negativo, oltre il limite, insieme vuoto.

**Verifica.** `dotnet build Jigen.slnx -m:1`, poi `dotnet test tests/PrimitiveTests/PrimitiveTests.csproj`. Attesa: tutti verdi, casi di rifiuto coperti.

**Uscita.** Handoff a T-16.2.

---

## Onda 2 — T-16.2 Sezione sparsa nel registro di scrittura

| Campo | Valore |
| --- | --- |
| Origine | T-16 *Conservare e indicizzare i vettori sparsi* |
| Requisiti | R-36, K-37, K-44 |
| Casi d'uso | UC-19, UC-16 |
| Attore / stato / onda | A1 / `planned` / 2 |
| Skill | `base-be-base-rules`, `implementation-be-dotnet-jigen-store` |
| Dipendenze | T-16.1 |

**Obiettivo.** La voce con termini entra nel registro nella stessa registrazione, con lo stesso CRC e lo stesso confine di fsync.

**Risultato osservabile.** Una scrittura con termini produce una registrazione che il replay ricostruisce integra; il conteggio del buffer di transazione la include.

**Dentro.** Sezione variabile dopo il vettore denso in `InsertRecordSize`, `SerializeInsert`, `TryReadRecord`; il replay che ricostruisce i termini; il conteggio in `Transaction.CommitAsync`; il rifiuto esplicito della scrittura con termini quando `Wal.Enabled` è falso (K-44); la versione del formato.

**Fuori.** Il file dei vettori sparsi, le posizioni, l'indice. La sezione è trasportata ma non ancora persistita: la card successiva la scrive. Una voce registrata e riletta dal solo registro è il risultato di questa card.

**Percorsi.** `src/Jigen/Jigen.Store/WalRecord.cs`, `src/Jigen/Jigen.Store/StoreWal.cs`, `src/Jigen/Jigen.Store/Transaction.cs`, `src/Jigen/Jigen.Store/Extensions/StoreWritingExtensions.cs`, `tests/JigenStoreTests/`.

**Incremento.** Una registrazione sola con tre sezioni: contenuto, denso, sparso. Il WAL è l'unica verità dello sparso finché T-16.3 non aggiunge il file.

**Passi.**

1. Aggiungere il campo alla dimensione e alla serializzazione, con un contatore di termini prima delle coppie.
2. Estendere la lettura e la firma di `TryReadRecord` senza rompere i chiamanti esistenti.
3. Nel replay, ricostruire i termini sulla voce riprodotta.
4. In `Transaction.CommitAsync`, contare e serializzare la sezione.
5. Rifiutare con `InvalidOperationException` esplicita la scrittura con termini e registro spento, prima di toccare la coda.
6. Test: andata e ritorno con e senza termini; registrazione troncata; transazione con termini; rifiuto senza registro.

**Verifica.** `dotnet test tests/JigenStoreTests/JigenStoreTests.csproj`. Attesa: i test di WAL e transazione esistenti restano verdi, i nuovi coprono i tre esiti.

**Uscita.** Handoff a T-16.3.

---

## Onda 3 — T-16.3 File sparso, posizioni e cancellazione

| Campo | Valore |
| --- | --- |
| Origine | T-16 *Conservare e indicizzare i vettori sparsi* |
| Requisiti | R-36, K-35, K-38, K-43 |
| Casi d'uso | UC-19 |
| Attore / stato / onda | A1 / `planned` / 3 |
| Skill | `base-be-base-rules`, `implementation-be-dotnet-jigen-store` |
| Dipendenze | T-16.2 |

**Obiettivo.** Lo sparso vive in un file proprio, con posizioni proprie, e la voce si rilegge per chiave.

**Risultato osservabile.** Scrittura, rilettura e cancellazione di una voce con termini funzionano anche dopo un riavvio; una voce senza termini non crea il file.

**Dentro.** Il file `<db>.sparse.jigen` con record `[idLen][id][count][(term, weight)]…`; un registro delle posizioni sparso separato (`<db>.sparse.index.jigen`) con il proprio dizionario, così la tupla di `IStore` e l'indice HNSW restano intatti; la scrittura in `AppendContentBatch`; la lettura `GetSparse`; la tombstone all'eliminazione; la riconciliazione.

**Fuori.** Checkpoint, compattazione ed elenco dei file: T-16.4. L'indice: T-16.5.

**Percorsi.** `src/Jigen/Jigen.Store/Store.cs`, `StoreRead.cs`, `StoreSparse.cs` (nuovo), `Extensions/StoreWritingExtensions.cs`, `WriterThread.cs`, `StoreOptions.cs`, `tests/JigenStoreTests/`.

**Incremento.** Persistenza completa della voce con due vettori, senza indice dei termini: la ricerca sparsa non esiste ancora, la rilettura sì.

**Passi.**

1. Definire il record e le funzioni di dimensione, scrittura e lettura accanto a quelle del denso.
2. Aprire il file solo quando la prima voce con termini arriva, e tenerlo in `EnableWriting`.
3. Scrivere il record nel batch, dopo il denso, e calcolare la posizione.
4. Scrivere un registro delle posizioni sparso e il suo caricamento, senza toccare `IStore.GetCollectionIndexOf` né i consumatori in `Jigen.Indexer.HNSW`.
5. Scrivere la tombstone in `DeleteContentCore` e azzerare la posizione.
6. Riconciliare in `ReconcileIndexAsync`: voci con termini senza record sparso vengono riscritte.
7. Test: andata e ritorno, riavvio, aggiornamento che cambia i termini, eliminazione, voce senza termini.

**Verifica.** `dotnet test tests/JigenStoreTests/JigenStoreTests.csproj`. Attesa: il test di riapertura legge le stesse coppie; il file sparso non esiste per collezioni senza termini.

**Uscita.** Handoff a T-16.4.

---

## Onda 4 — T-16.4 Checkpoint, compattazione ed elenco dei file

| Campo | Valore |
| --- | --- |
| Origine | T-16 *Conservare e indicizzare i vettori sparsi* |
| Requisiti | R-36, K-45, K-43 |
| Casi d'uso | UC-19, UC-17 |
| Attore / stato / onda | A1 / `planned` / 4 |
| Skill | `base-be-base-rules`, `implementation-be-dotnet-jigen-store` |
| Dipendenze | T-16.3 |

**Obiettivo.** Il ciclo di vita del file sparso è lo stesso degli altri: durevole al checkpoint, riscritto alla compattazione, elencato fra i file del database.

**Risultato osservabile.** Dopo un arresto subito dopo un checkpoint nessuna voce con termini manca; dopo una compattazione le posizioni sono coerenti; l'eliminazione del database non lascia file orfani.

**Dentro.** Fsync del file sparso in `PerformCheckpointCore` prima del troncamento; riscrittura e scambio in `CompactCore`; contabilità dello spazio morto sparso; il file in `GetFileNames`; il rifiuto esplicito se il registro è spento.

**Fuori.** L'indice dei termini e la sua osservazione: T-16.5.

**Percorsi.** `src/Jigen/Jigen.Store/WalCheckpointer.cs`, `StoreShrink.cs`, `Store.cs`, `tests/JigenStoreTests/`.

**Incremento.** Il file sparso partecipa a tutte le operazioni che già coinvolgono gli altri tre.

**Passi.**

1. Aggiungere il fsync nella sezione esclusiva del checkpoint, prima di `SetLength(0)`.
2. Includere il file dei vettori e il registro delle posizioni sparso nella compattazione, nello scambio atomico e nella riapertura degli handle.
3. Contabilizzare lo spazio morto sparso e includerlo in `DeadBytes`.
4. Aggiungere il file a `GetFileNames`.
5. Test di arresto non pulito con termini; test di compattazione con spazio morto; test di eliminazione del database.

**Verifica.** `dotnet test tests/JigenStoreTests/JigenStoreTests.csproj`. Attesa: nessuna voce persa dopo l'arresto simulato; `DeadBytes` scende dopo la compattazione.

**Uscita.** Handoff a T-16.5.

---

## Onda 5 — T-16.5 Indice dei termini e osservazione

| Campo | Valore |
| --- | --- |
| Origine | T-16 *Conservare e indicizzare i vettori sparsi* |
| Requisiti | R-41, K-38, K-42, K-43 |
| Casi d'uso | UC-20 |
| Attore / stato / onda | A1 / `planned` / 5 |
| Skill | `base-be-base-rules`, `implementation-be-dotnet-jigen-store`, `capabilities-dev-problem-solving-methodology` |
| Dipendenze | T-16.4 |

**Obiettivo.** Un indice invertito per collezione, aggiornato nella stessa pipeline, ricostruibile dall'archivio e osservabile.

**Risultato osservabile.** `GetCollectionInfo` riporta termini distinti, elenchi, lunghezza media, memoria e termini ritirati; cancellare i file dell'indice e riaprire non cambia i risultati.

**Dentro.** Il progetto `Jigen.Indexer.Sparse`: elenchi ordinati per peso decrescente, tombstone, costruzione incrementale dallo stesso worker; ricostruzione all'apertura; le metriche in `CollectionInfo`; la misura di memoria a 100.000 voci.

**Fuori.** La ricerca e la fusione: T-17. La potatura: T-17.4, rinviata.

**Percorsi.** `src/Jigen/Jigen.Indexer.Sparse/` (nuovo progetto), `Jigen.slnx`, `src/Jigen/Jigen.Store/Store.cs`, `StoreOptions.cs`, `src/Jigen/Jigen.Primitives/DataStructures/CollectionInfo.cs`, `tests/HnswTest/` o nuovo progetto di test.

**Incremento.** L'indice esiste, si aggiorna e si ricostruisce. Non risponde ancora a una query.

**Passi.**

1. Creare il progetto e aggiungerlo alla soluzione, sul modello di `Jigen.Indexer.HNSW`.
2. Definire il file degli elenchi e il dizionario termine → elenco, con ordinamento per peso.
3. Agganciare l'aggiornamento alla pipeline esistente, in parallelo all'indice denso, senza toccare `IIndexer`.
4. Costruire dall'archivio all'apertura, quando il file manca o è più vecchio del contenuto.
5. Esporre le metriche in `CollectionInfo` e nel contratto di lettura.
6. Misurare memoria e tempo di costruzione a 100.000 voci; registrare il valore nel dossier.
7. Test: inserimento, aggiornamento, cancellazione, ricostruzione, metriche.

**Verifica.** `dotnet build Jigen.slnx -m:1`, `dotnet test tests/JigenStoreTests/JigenStoreTests.csproj`, più il test dell'indice. Attesa: risultati invariati dopo la cancellazione dei file dell'indice; memoria misurata e scritta.

**Uscita.** Handoff a T-17.1.

---

## Onda 6 — T-17.1 Ricerca sparsa esatta

| Campo | Valore |
| --- | --- |
| Origine | T-17 *Cercare per vettore sparso e fondere* |
| Requisiti | R-37, K-39 |
| Casi d'uso | UC-20 |
| Attore / stato / onda | A1 / `planned` / 6 |
| Skill | `base-be-base-rules`, `implementation-be-dotnet-jigen-store` |
| Dipendenze | T-16.5 |

**Obiettivo.** Top-k per prodotto scalare sulle sole dimensioni condivise, esatto.

**Risultato osservabile.** Con una query di N termini, i risultati coincidono, voce per voce e con lo stesso ordine, con una ricerca esaustiva calcolata a mano sul campione.

**Dentro.** La query sparsa e il suo contratto; la visita degli elenchi; la somma dei pesi; l'ordinamento; il top-k; il caso di query senza termini coincidenti.

**Fuori.** Filtro, raggruppamento, fusione, potatura.

**Percorsi.** `src/Jigen/Jigen.Indexer.Sparse/`, `src/Jigen/Jigen.Store/Extensions/SearchExtensions.cs`, `src/Server/Jigen.Core/Query/`, `tests/JigenStoreTests/`.

**Incremento.** Una ricerca lessicale esatta, senza filtri.

**Passi.**

1. Definire il contratto di query sparsa accanto a quello denso, senza modificare `IIndexer`.
2. Implementare la visita e la somma, con esclusione dei termini ritirati.
3. Ordinare per punteggio decrescente, con parità per chiave crescente.
4. Restituire vuoto, senza errore, quando nessun termine è presente.
5. Test di confronto contro ricerca esaustiva su 1.000 voci.

**Verifica.** `dotnet test tests/JigenStoreTests/JigenStoreTests.csproj`. Attesa: confronto identico su tre query di lunghezza diversa.

**Uscita.** Handoff a T-17.2.

---

## Onda 7 — T-17.2 Filtro e raggruppamento sulla ricerca sparsa

| Campo | Valore |
| --- | --- |
| Origine | T-17 *Cercare per vettore sparso e fondere* |
| Requisiti | R-38, K-39 |
| Casi d'uso | UC-20 |
| Attore / stato / onda | A1 / `planned` / 7 |
| Skill | `base-be-base-rules`, `implementation-be-dotnet-jigen-store` |
| Dipendenze | T-17.1 |

**Obiettivo.** Lo stesso predicato e lo stesso raggruppamento della ricerca densa, con l'esattezza garantita.

**Risultato osservabile.** Con un filtro attivo il risultato coincide con la ricerca esaustiva ristretta al predicato; il raggruppamento restituisce al più `top` gruppi con la chiave vincente.

**Dentro.** Il predicato esistente applicato alla ricerca sparsa; il raggruppamento per record condiviso con budget di candidati; la revoca della potatura quando c'è un filtro.

**Fuori.** La fusione: T-17.3.

**Percorsi.** `src/Jigen/Jigen.Store/Extensions/SearchExtensions.cs`, `src/Server/Jigen.Handlers/CQRS/SharedMetadataHandlers.cs`, `tests/JigenStoreTests/`.

**Incremento.** Parità funzionale con la ricerca densa su filtro e raggruppamento.

**Passi.**

1. Riusare il traduttore di espressioni esistente, senza un secondo linguaggio di filtro.
2. Applicare il predicato dopo la visita: nessuna potatura con filtro attivo.
3. Riusare `SearchByMetadata` per il raggruppamento, con il punteggio massimo come criterio.
4. Test: predicato che ammette e che esclude; due voci sullo stesso record; budget esaurito.

**Verifica.** `dotnet test tests/JigenStoreTests/JigenStoreTests.csproj`. Attesa: stesso insieme della ricerca esaustiva filtrata; gruppi coerenti.

**Uscita.** Handoff a T-17.3.

---

## Onda 8 — T-17.3 Fusione denso e sparso

| Campo | Valore |
| --- | --- |
| Origine | T-17 *Cercare per vettore sparso e fondere* |
| Requisiti | R-39, K-40 |
| Casi d'uso | UC-21 |
| Attore / stato / onda | A1 / `planned` / 8 |
| Skill | `base-be-base-rules`, `implementation-be-dotnet-dev` |
| Dipendenze | T-17.1, T-04 |

**Obiettivo.** Un ordinamento unico per posizione, con pesi dichiarati, senza confrontare scale.

**Risultato osservabile.** Con un peso a zero il risultato coincide con l'altra modalità; con entrambi a zero la richiesta è rifiutata; a parità di posizione l'ordine è stabile.

**Dentro.** La fusione reciproca per posizione; i pesi; la parità deterministica; il rifiuto dei casi non validi; la provenienza di ogni risultato.

**Fuori.** La fusione nel client e la calibrazione delle modalità diverse, che restano dove sono.

**Percorsi.** `src/Server/Jigen.Handlers/`, `src/Server/Jigen.Core/`, `tests/Jigen.API.Tests/`.

**Incremento.** Una sola ricerca combinata, nel server.

**Passi.**

1. Definire il contratto della richiesta ibrida con pesi.
2. Implementare la fusione su due elenchi già ordinati, con la costante dichiarata.
3. Rifiutare pesi entrambi nulli e richieste senza vettori.
4. Marcare ogni risultato con la modalità di provenienza e con il punteggio originale.
5. Test: tabella di pesi 0, 0,5, 1; elenco con un solo risultato; parità.

**Verifica.** `dotnet test tests/Jigen.API.Tests/Jigen.API.Tests.csproj`. Attesa: ordini attesi dalla tabella, determinismo su dieci esecuzioni.

**Uscita.** Handoff a T-18.1.

---

## Onda 9 — T-18.1 Superfici REST e binaria

| Campo | Valore |
| --- | --- |
| Origine | T-18 *Esporre la parità dei vettori sparsi* |
| Requisiti | R-40, K-41 |
| Casi d'uso | UC-19, UC-20, UC-21 |
| Attore / stato / onda | A1 / `planned` / 9 |
| Skill | `base-be-base-rules`, `implementation-be-dotnet-dev`, `implementation-be-dotnet-hikyaku` |
| Dipendenze | T-16.3, T-17.1, T-17.3 |

**Obiettivo.** Le tre operazioni su entrambe le superfici, con lo stesso esito.

**Risultato osservabile.** La stessa richiesta produce lo stesso stato e lo stesso ordine su REST e su gRPC.

**Dentro.** I contratti protobuf; i campi della richiesta e della risposta; le rotte REST; il mapping; gli errori `400` coerenti.

**Fuori.** I client e l'interfaccia.

**Percorsi.** `src/Server/Jigen.Grpc/Protos/Jigen.proto`, `src/Server/Jigen.Grpc/Service.cs`, `src/Server/Jigen.Core/`, `src/Server/Jigen.API/CollectionsController.cs`, `src/Server/Jigen.API/Dto/`, `tests/Jigen.Grpc.Tests/`, `tests/Jigen.API.Tests/`.

**Incremento.** Parità fra le due superfici, verificata da una matrice.

**Passi.**

1. Aggiungere i campi al protocollo, senza riusare il campo vettoriale denso.
2. Aggiungere le rotte REST accanto a quelle esistenti, con la stessa semantica.
3. Mappare gli errori di validazione in `400` con il termine in errore.
4. Test: matrice di tre operazioni per due superfici, confronto degli esiti.

**Verifica.** `dotnet test tests/Jigen.Grpc.Tests/Jigen.Grpc.Tests.csproj` e `tests/Jigen.API.Tests`. Attesa: matrice completa verde.

**Uscita.** Handoff a T-18.2.

---

## Onda 10 — T-18.2 Client .NET e Python

| Campo | Valore |
| --- | --- |
| Origine | T-18 *Esporre la parità dei vettori sparsi* |
| Requisiti | R-40 |
| Casi d'uso | UC-19, UC-20, UC-21 |
| Attore / stato / onda | A1 / `planned` / 10 |
| Skill | `base-be-base-rules`, `implementation-be-dotnet-dev` (client `net8.0`); nessuna skill per il Python |
| Dipendenze | T-18.1 |

**Obiettivo.** Le stesse tre operazioni nei due client, senza logica di dominio nel client.

**Risultato osservabile.** Un programma di esempio per client scrive, cerca e fonde senza modificare il server.

**Dentro.** I tipi del client .NET; la facciata per collezione; il client Python e i suoi tipi; gli esempi; i test dei due client.

**Fuori.** L'interfaccia amministrativa.

**Percorsi.** `src/Client/Jigen.Client/`, `src/Client/Jigen.Python/src/jigen/`, `src/Client/Jigen.Python/examples/`, `tests/JigenClientTest/`, `src/Client/Jigen.Python/tests/`.

**Incremento.** Due client allineati al contratto, con esempi eseguibili.

**Passi.**

1. Esporre i tipi dello sparso nel client .NET, coerenti con `VectorEntry<T>`.
2. Aggiungere i metodi di scrittura, ricerca e fusione alla facciata.
3. Ripetere nel client Python seguendo le convenzioni del pacchetto esistente.
4. Aggiornare gli esempi.
5. Test: andata e ritorno per client; errore di validazione propagato con il messaggio del server.

**Verifica.** `dotnet test tests/JigenClientTest/JigenClientTest.csproj`; in `src/Client/Jigen.Python`, `pytest`. Attesa: verdi, esempi eseguibili.

**Uscita.** Handoff a T-18.3.

---

## Onda 11 — T-18.3 Workbench e documentazione

| Campo | Valore |
| --- | --- |
| Origine | T-18 *Esporre la parità dei vettori sparsi* |
| Requisiti | R-40, R-34, K-25, K-28 |
| Casi d'uso | UC-20, UC-21 |
| Attore / stato / onda | A1 / `planned` / 11 |
| Skill | `base-fe-base-rules`, `implementation-fe-vue-dev`, `capabilities-interface-design` (superficie complessa), nessuna skill per Naive UI |
| Dipendenze | T-18.1 |

**Obiettivo.** Il workbench prova le tre modalità con le etichette congelate; la documentazione descrive le nuove operazioni e opzioni.

**Risultato osservabile.** Dal workbench si sceglie densa, sparsa o ibrida, si vedono i pesi e i tempi; la documentazione elenca le operazioni nuove.

**Dentro.** Il selettore di modalità; i campi dei termini; i pesi; le colonne del risultato; le etichette del [riferimento all'esperienza](../ux-reference.md) (ux-reference.md); le pagine di documentazione del prodotto.

**Fuori.** Nuove superfici o nuove voci di menu: il riferimento non le prevede.

**Percorsi.** `src/Management/Jigen.Insight/src/views/workbench/`, `src/Management/Jigen.Insight/src/locales/langs/en-us.ts`, `src/Management/Jigen.Insight/src/service/api/collections.ts`, `docs/server/rest-api.md`, `docs/server/grpc-api.md`, `docs/in-process/store-options.md`.

**Incremento.** La superficie S-04.4 estesa e la documentazione allineata.

**Passi.**

1. Aggiungere la modalità e i campi, riusando i controlli Naive UI già in uso nella vista.
2. Mostrare punteggio fuso, punteggio originale e provenienza.
3. Aggiungere le etichette inglesi congelate, senza introdurre una seconda lingua.
4. Aggiornare le tre pagine di documentazione.
5. Verifica manuale della tastiera sulle nuove parti della superficie.

**Verifica.** In `src/Management/Jigen.Insight`: `npm run typecheck` e `npm run build`. Attesa: nessun errore di tipo, build riuscita.

**Uscita.** Handoff a `phase-development-development-handoff`.

---

## Onda rinviata — T-17.4 Potatura dei candidati

| Campo | Valore |
| --- | --- |
| Origine | T-17 *Cercare per vettore sparso e fondere* |
| Requisiti | R-37, K-39 |
| Stato / onda | `deferred` / dopo la prova U3 |
| Motivo | La potatura riduce l'esattezza e va attivata solo con una misura che ne dichiari il costo. Il primo rilascio è esatto per default |
| Dipendenze | T-17.1, prova U3 |
| Verifica attesa | Confronto fra ricerca potata e ricerca esaustiva senza filtro, con perdita dichiarata e misurata |

---

## Panoramica cronologica per attore

| Onda | A1 | Gate e handoff |
| --- | --- | --- |
| 1 | [T-16.1 Vocabolario dello sparso](#onda-1--t-161-vocabolario-dello-sparso) — `ready` | Approvazione del codice, poi test; abilita T-16.2 |
| 2 | [T-16.2 Sezione sparsa nel registro di scrittura](#onda-2--t-162-sezione-sparsa-nel-registro-di-scrittura) — `planned` | Attende T-16.1 Vocabolario dello sparso; abilita T-16.3 |
| 3 | [T-16.3 File sparso, posizioni e cancellazione](#onda-3--t-163-file-sparso-posizioni-e-cancellazione) — `planned` | Attende T-16.2 Sezione sparsa nel registro di scrittura; abilita T-16.4 |
| 4 | [T-16.4 Checkpoint, compattazione ed elenco dei file](#onda-4--t-164-checkpoint-compattazione-ed-elenco-dei-file) — `planned` | Attende T-16.3 File sparso, posizioni e cancellazione; abilita T-16.5 |
| 5 | [T-16.5 Indice dei termini e osservazione](#onda-5--t-165-indice-dei-termini-e-osservazione) — `planned` | Attende T-16.4 Checkpoint, compattazione ed elenco dei file; abilita T-17.1 |
| 6 | [T-17.1 Ricerca sparsa esatta](#onda-6--t-171-ricerca-sparsa-esatta) — `planned` | Attende T-16.5 Indice dei termini e osservazione; abilita T-17.2 |
| 7 | [T-17.2 Filtro e raggruppamento sulla ricerca sparsa](#onda-7--t-172-filtro-e-raggruppamento-sulla-ricerca-sparsa) — `planned` | Attende T-17.1 Ricerca sparsa esatta; abilita T-17.3 |
| 8 | [T-17.3 Fusione denso e sparso](#onda-8--t-173-fusione-denso-e-sparso) — `planned` | Attende T-17.1 Ricerca sparsa esatta; abilita T-18.1 |
| 9 | [T-18.1 Superfici REST e binaria](#onda-9--t-181-superfici-rest-e-binaria) — `planned` | Attende T-16.3, T-17.1 e T-17.3; abilita T-18.2 e T-18.3 |
| 10 | [T-18.2 Client .NET e Python](#onda-10--t-182-client-net-e-python) — `planned` | Attende T-18.1 Superfici REST e binaria; abilita la chiusura della fase |
| 11 | [T-18.3 Workbench e documentazione](#onda-11--t-183-workbench-e-documentazione) — `planned` | Attende T-18.1 Superfici REST e binaria; abilita la chiusura della fase |
| — | [T-17.4 Potatura dei candidati](#onda-rinviata--t-174-potatura-dei-candidati) — `deferred` | Rinviata fino alla prova U3 |

Le onde sono ordine di dipendenza, non stime di durata. La stima dell'incremento è in [Order of magnitude estimate](../scouting/sparse-vectors/dossier.md#order-of-magnitude-estimate) (scouting/sparse-vectors/dossier.md#order-of-magnitude-estimate).
