# Task list — vettori sparsi

Elenco eseguibile, atomico, dell'incremento R-36…R-41. Le card e la loro motivazione sono in [Task plan](task-plan.md) (task-plan.md); qui ogni card è decomposta in passi verificabili. Riferimenti: [analisi](../analysis/dossier.md) (analysis/dossier.md), [architettura](../architecture/dossier.md) (architecture/dossier.md), [casi d'uso](../use-cases.md) (use-cases.md).

## Come si usa questo elenco

| Stato | Significato |
| --- | --- |
| `[ ]` | Da fare |
| `[~]` | In corso, una sola voce per volta |
| `[x]` | Fatto e verificato |
| `[!]` | Bloccato: la voce non dice perché, il motivo va nel [dossier](dossier.md#task-execution-log) (dossier.md#task-execution-log) |
| `[-]` | Rinviato con motivo |

Regole di esecuzione:

1. Una voce per volta, nell'ordine scritto. Nessuna voce si chiude senza il comando eseguito e il risultato annotato.
2. Dopo i passi di produzione di ogni card, **gate di qualità**: si chiede approvazione del codice prima di scrivere o completare i test.
3. Una voce che non può rispettare una decisione bloccata si ferma: la card diventa `[!]` e la deviazione torna all'architettura.
4. Nessuna voce apre un file per la prima volta senza aver letto i file vicini che ne definiscono lo stile.

## Definizione di fatto, comune a ogni card

- [ ] Il codice compila: `dotnet build Jigen.sln -m:1`.
- [ ] I test del perimetro passano, con il comando dichiarato nella card.
- [ ] I test esistenti dello stesso perimetro restano verdi.
- [ ] Nessun file di test temporaneo, nessun `Console.WriteLine`, nessun `TODO` senza card.
- [ ] Le skill attivate sono annotate nel [dossier](dossier.md#task-execution-log) (dossier.md#task-execution-log).
- [ ] Le opzioni nuove vivono in un oggetto di configurazione, non lette sparse.

## Matrice dei comandi

| Ambito | Comando | Attesa |
| --- | --- | --- |
| Soluzione | `dotnet build Jigen.sln -m:1` | 0 errori |
| Primitivi | `dotnet test tests/PrimitiveTests/PrimitiveTests.csproj` | tutti verdi |
| Archivio | `dotnet test tests/JigenStoreTests/JigenStoreTests.csproj` | tutti verdi |
| Indice sparso | `dotnet test tests/SparseIndexerTests/SparseIndexerTests.csproj` | tutti verdi |
| Superfici | `dotnet test tests/Jigen.API.Tests/Jigen.API.Tests.csproj` e `tests/Jigen.Grpc.Tests/Jigen.Grpc.Tests.csproj` | tutti verdi |
| Client .NET | `dotnet test tests/JigenClientTest/JigenClientTest.csproj` | tutti verdi |
| Client Python | in `src/Client/Jigen.Python`: `pytest` | tutti verdi |
| Interfaccia | in `src/Management/Jigen.Insight`: `npm run typecheck`, `npm run build` | 0 errori, build riuscita |

---

## T-16.1 Vocabolario dello sparso

Requisiti: R-36, K-35, K-36. Caso d'uso: UC-19. Dipendenze: nessuna.

### Passi

- [x] **T-16.1.1** Creare `src/Jigen/Jigen.Primitives/DataStructures/SparseVector.cs`: classe immutabile, `ReadOnlyMemory<int> Terms`, `ReadOnlyMemory<float> Weights`, `Count`, `IsEmpty`, `TermsSpan`/`WeightsSpan`. Nessun setter pubblico.
- [x] **T-16.1.2** Aggiungere la validazione statica riusabile: termini unici e strettamente crescenti, pesi finiti e non negativi, almeno un peso maggiore di zero, `Count` entro il massimo. Ogni violazione indica il termine in errore.
- [x] **T-16.1.3** Aggiungere una factory da coppie e una da due array, che normalizzano l'ordine o rifiutano: scegliere **rifiuta**, così il chiamante sa di aver sbagliato invece di ricevere un vettore diverso da quello scritto.
- [x] **T-16.1.4** Aggiungere `SparseOptions` con `MaxTerms` (predefinito 1024) e collegarlo a `StoreOptions` in `src/Jigen/Jigen.Store/StoreOptions.cs`, senza letture sparse.
- [x] **T-16.1.5** Aggiungere `SparseVector Sparse { get; set; }` a `src/Jigen/Jigen.Primitives/DataStructures/VectorEntry.cs`, documentando la regola: `null` = assente; un vettore con `Count == 0` non esiste, perché la validazione lo rifiuta. Nessuna ambiguità fra assente e vuoto.
- [x] **T-16.1.6** Aggiornare `VectorEntry.Empty` per lasciare `Sparse` a `null`.
- [x] **T-16.1.7** **Gate di qualità**: chiedere approvazione del codice.
- [x] **T-16.1.8** Scrivere `tests/PrimitiveTests/SparseVectorTests.cs` con: valido, termini duplicati, termini non ordinati, peso negativo, peso non finito, tutti i pesi a zero, oltre il massimo, insieme vuoto.
- [x] **T-16.1.9** Verificare: `dotnet build Jigen.sln -m:1` e `dotnet test tests/PrimitiveTests/PrimitiveTests.csproj`.
- [x] **T-16.1.10** Annotare nel dossier: data, esito, comando, numero di test.

### Fine card

Il tipo esiste e valida. `VectorEntry` lo porta in memoria. Nessun file cambia, nessun test di archivio toccato.

---

## T-16.2 Sezione sparsa nel registro di scrittura

Requisiti: R-36, K-37, K-44. Casi d'uso: UC-19, UC-16. Dipendenze: T-16.1.

### Passi

- [x] **T-16.2.1** In `src/Jigen/Jigen.Store/WalRecord.cs`, estendere `InsertRecordSize` con la sezione sparsa: `[count:int32][term:int32][weight:float32]×count`. Il contatore c'è sempre; con zero termini la sezione è di 4 byte.
- [x] **T-16.2.2** Estendere `SerializeInsert` con la stessa scrittura, dopo il vettore denso, dentro il CRC esistente.
- [x] **T-16.2.3** Estendere `TryReadRecord`: dopo il denso, leggere il contatore solo se restano almeno 4 byte; poi le coppie solo se la lunghezza residua le contiene. Un record vecchio, senza sezione, resta leggibile e produce `Sparse = null`. Non introdurre un nuovo tipo di record.
- [x] **T-16.2.4** In `src/Jigen/Jigen.Store/StoreWal.cs`, `ReplayWal` ricostruisce `Sparse` sulle voci inserite, sia fuori sia dentro una transazione.
- [x] **T-16.2.5** In `src/Jigen/Jigen.Store/Transaction.cs`, `CommitAsync` conta la sezione in `totalWalSize` e la serializza: senza questo conteggio il blocco sfora il buffer.
- [x] **T-16.2.6** In `src/Jigen/Jigen.Store/Extensions/StoreWritingExtensions.cs`, rifiutare con messaggio esplicito la scrittura con `Sparse` presente quando `Options.Wal?.Enabled != true`, **prima** di accodare o aprire file. Messaggio: la scrittura di vettori sparsi richiede il registro di scrittura.
- [x] **T-16.2.7** Correggere l'uscita anticipata di `ValidateVectorDimensions` in `src/Jigen/Jigen.Store/Store.cs`: se il vettore denso è vuoto ma lo sparso è presente, validare comunque il nome della collezione e i termini.
- [x] **T-16.2.8** **Gate di qualità**: chiedere approvazione del codice.
- [x] **T-16.2.9** Scrivere `tests/JigenStoreTests/SparseWalTests.cs`: andata e ritorno con e senza termini; record troncato a metà sezione; record vecchio senza sezione; rifiuto con registro spento; transazione con termini.
- [x] **T-16.2.10** Estendere `tests/JigenStoreTests/TransactionTests.cs` con un caso che supera la dimensione del buffer per la sola sezione sparsa.
- [x] **T-16.2.11** Verificare: build e `dotnet test tests/JigenStoreTests/JigenStoreTests.csproj`. Attesa: i test di WAL, transazione e affidabilità esistenti restano verdi.

### Fine card

Una scrittura con termini produce **una** registrazione, con un CRC e un confine di fsync. Il replay la ricostruisce. Lo sparso non è ancora su file: questa card dimostra l'ordine, non la persistenza.

---

## T-16.3 File sparso, posizioni e cancellazione

Requisiti: R-36, K-35, K-38, K-43. Caso d'uso: UC-19. Dipendenze: T-16.2.

Vincolo di progetto emerso dal codice: la tupla di `IStore.GetCollectionIndexOf` è consumata da `Jigen.Indexer.HNSW` e da `BruteForceIndexer`. **Non si estende**: lo sparso ha un registro delle posizioni proprio.

### Passi

- [x] **T-16.3.1** Creare `src/Jigen/Jigen.Store/StoreSparse.cs` come `partial class Store`: nome del file dei vettori sparsi, nome del registro delle posizioni sparso, dizionario `collection → key → (position, count)`, apertura degli handle.
- [x] **T-16.3.2** Definire il record: `[idLen:int32][id][count:int32][(term:int32)(weight:float32)]×count`, con `SparseRecordSize`. L'intestazione del file è di 8 byte, così la posizione `0` resta il valore «assente», come per i vettori densi.
- [x] **T-16.3.3** Aprire il file solo alla prima voce con termini: una collezione senza sparso non crea file né handle.
- [x] **T-16.3.4** In `AppendContentBatch`, scrivere i record sparsi nel proprio buffer e restituirne le posizioni accanto a quelle dense, senza toccare i buffer esistenti.
- [x] **T-16.3.5** In `WriterThread.WriterJob`, scrivere il registro delle posizioni sparso nello stesso blocco `_ioLock` in cui scrive l'indice denso.
- [x] **T-16.3.6** Implementare `GetSparse(collection, key)` in `StoreSparse.cs`, con la stessa disciplina di `GetEmbedding`: lettura per posizione, verifica dell'identificativo, `null` se il record è incoerente.
- [x] **T-16.3.7** Scrivi la tombstone sparsa in `DeleteContentCore` e in `ClearContentCore`, e rimuovi la chiave dal dizionario.
- [x] **T-16.3.8** Caricare il registro sparso all'apertura, con lo stesso trattamento della coda troncata di `LoadIndex`: scarta la coda illeggibile, non l'intero file.
- [x] **T-16.3.9** In `ReconcileIndexAsync`, riscrivere il record sparso delle voci che hanno termini e nessuna posizione valida.
- [x] **T-16.3.10** **Gate di qualità**: chiedere approvazione del codice.
- [x] **T-16.3.11** Scrivere `tests/JigenStoreTests/SparsePersistenceTests.cs`: andata e ritorno; riapertura dopo chiusura; aggiornamento che sostituisce i termini; eliminazione; voce senza termini che non crea il file; `Clear` di collezione.
- [x] **T-16.3.12** Verificare: `dotnet test tests/JigenStoreTests/JigenStoreTests.csproj`, più `tests/HnswTest/HnswTest.csproj` per escludere regressioni sull'indice denso.

### Fine card

La voce con due vettori si rilegge dopo un riavvio. La ricerca sparsa non esiste ancora: è deliberato.

---

## T-16.4 Checkpoint, compattazione ed elenco dei file

Requisiti: R-36, K-45, K-43. Casi d'uso: UC-19, UC-17. Dipendenze: T-16.3.

### Passi

- [x] **T-16.4.1** In `src/Jigen/Jigen.Store/WalCheckpointer.cs`, `PerformCheckpointCore`: fsync del file dei vettori sparsi e del registro sparso **prima** di `SetLength(0)`, dentro la stessa sezione esclusiva.
- [x] **T-16.4.2** In `src/Jigen/Jigen.Store/StoreShrink.cs`, `CompactCore`: riscrivere il file sparso e il registro sparso in file `.compact`, copiando i record per posizione crescente come per i densi.
- [x] **T-16.4.3** Estendere lo scambio atomico a cinque file e riaprire gli handle; aggiornare il dizionario sparso con le nuove posizioni.
- [x] **T-16.4.4** Contabilizzare lo spazio morto sparso e **includerlo** in `DeadBytes`; oggi `DeadContentBytes` è incrementato ma escluso dalla somma, e non va ripetuto lo stesso errore.
- [x] **T-16.4.5** Aggiungere i due file nuovi a `Store.GetFileNames`, così l'eliminazione del database non lascia orfani e le dimensioni riportate sono corrette.
- [x] **T-16.4.6** In `DatabasesManager`, aprire i database esistenti senza sparso senza creare file vuoti.
- [x] **T-16.4.7** **Gate di qualità**: chiedere approvazione del codice.
- [x] **T-16.4.8** Estendere `tests/JigenStoreTests/StoreReliabilityTests.cs` con un arresto simulato dopo un checkpoint e voci con termini.
- [x] **T-16.4.9** Estendere `tests/JigenStoreTests/StoreShrinkTests.cs` con spazio morto sparso e verifica che `DeadBytes` scenda.
- [x] **T-16.4.10** Aggiungere a `tests/JigenStoreTests/DeletePersistenceTests.cs` il caso di eliminazione del database con i due file nuovi.
- [x] **T-16.4.11** Verificare: `dotnet test tests/JigenStoreTests/JigenStoreTests.csproj`.

### Fine card

Il file sparso partecipa a tutte le operazioni che già coinvolgono gli altri. Nessuna scrittura confermata si perde in un arresto.

---

## T-16.5 Indice dei termini e osservazione

Requisiti: R-41, K-38, K-42, K-43. Caso d'uso: UC-20. Dipendenze: T-16.4.

### Passi

- [x] **T-16.5.1** Creare il progetto `src/Jigen/Jigen.Indexer.Sparse/Jigen.Indexer.Sparse.csproj` (`net10.0`, `MessagePack` e `System.IO.Hashing` come `Jigen.Indexer.HNSW`) e aggiungerlo con `dotnet sln add`.
- [x] **T-16.5.2** Definire `ISparseIndex` in `src/Jigen/Jigen.Primitives/`: aggiunta, rimozione, ricostruzione, metriche. Contratto separato: `IIndexer` non si tocca.
- [x] **T-16.5.3** Implementare l'indice: `term → elenco ordinato per peso decrescente`, con tombstone e conteggio dei ritirati.
- [x] **T-16.5.4** Persistenza incrementale su file mappato, con lo stesso schema di `GraphStorage`: append + flush periodico, ricostruzione quando il file manca o è più vecchio del contenuto.
- [x] **T-16.5.5** Aggiungere `SparseIndex` a `StoreOptions` e istanziarlo in `DatabasesManager` accanto a `SmallWorldIndexer`, con le opzioni del server.
- [x] **T-16.5.6** Agganciare l'aggiornamento alla pipeline: nel fan-out di `WriterThread.EnqueueForIndexing` **copiare anche `Sparse`** nella voce ricostruita, altrimenti i termini si perdono prima dell'indice.
- [x] **T-16.5.7** Esporre le metriche: `SparseIndexInfo` con termini distinti, elenchi, lunghezza media, byte occupati, termini ritirati; agganciarlo a `CollectionInfo` e al DTO di risposta.
- [x] **T-16.5.8** Estendere `JigenIndexSettings` con `SparseEnabled`, `SparseMemLimitMB`, `SparseMaxTerms`, e portarli in `appsettings.json`.
- [x] **T-16.5.9** **Gate di qualità**: chiedere approvazione del codice.
- [x] **T-16.5.10** Creare `tests/SparseIndexerTests/` con: inserimento, aggiornamento, cancellazione, ricostruzione da archivio, metriche.
- [x] **T-16.5.11** Aggiungere a `tests/JigenStoreTests/` il caso «cancello i file dell'indice e riapro: la ricerca futura deve dare lo stesso esito», limitato alle metriche finché T-17.1 non esiste.
- [x] **T-16.5.12** Misurare memoria e tempo di costruzione a 100.000 voci; registrare il valore nel dossier e fissare `SparseMemLimitMB` da quel numero, non da un'ipotesi.
- [x] **T-16.5.13** Verificare: build, `dotnet test tests/SparseIndexerTests/...`, `dotnet test tests/JigenStoreTests/...`.

### Fine card

L'indice esiste, si aggiorna, si ricostruisce e si osserva. Non risponde a una query: è il confine con T-17.

---

## T-17.1 Ricerca sparsa esatta

Requisiti: R-37, K-39. Caso d'uso: UC-20. Dipendenze: T-16.5.

### Passi

- [x] **T-17.1.1** Definire la query sparsa e il risultato nel contratto di `Jigen.Store`, accanto a quelli densi: `SearchSparse(collection, SparseVector query, top)`.
- [x] **T-17.1.2** Implementare la visita: per ogni termine della query, l'elenco delle voci con quel peso; somma dei prodotti sulle sole dimensioni condivise.
- [x] **T-17.1.3** Escludere le tombstone e i termini ritirati.
- [x] **T-17.1.4** Ordinare per punteggio decrescente, con parità risolta per chiave crescente, e troncare a `top`.
- [x] **T-17.1.5** Restituire vuoto, senza errore, quando nessun termine della query esiste nella collezione o la collezione non ha indice.
- [x] **T-17.1.6** Verificare che `GetSparse` restituisca i contenuti con la stessa semantica di `GetEmbedding` (contenuto facoltativo, `null` se assente).
- [x] **T-17.1.7** **Gate di qualità**: chiedere approvazione del codice.
- [x] **T-17.1.8** Scrivere il confronto contro una ricerca esaustiva calcolata a mano su 1.000 voci, con tre query di lunghezza 1, 5 e 20.
- [x] **T-17.1.9** Aggiungere il caso di parità di punteggio e il caso di query con termini inesistenti.
- [x] **T-17.1.10** Verificare: `dotnet test tests/JigenStoreTests/JigenStoreTests.csproj` e `tests/SparseIndexerTests`.

### Fine card

Ricerca lessicale esatta, senza filtri, con confronto dimostrato contro la ricerca esaustiva.

---

## T-17.2 Filtro e raggruppamento sulla ricerca sparsa

Requisiti: R-38, K-39. Caso d'uso: UC-20. Dipendenze: T-17.1.

### Passi

- [x] **T-17.2.1** Riusare `IFilterExpression` e il traduttore esistente: nessun secondo linguaggio di filtro.
- [x] **T-17.2.2** Applicare il predicato dopo la visita, senza potatura quando il filtro è attivo.
- [x] **T-17.2.3** Riusare `SearchByMetadata` per il raggruppamento, con budget di candidati obbligatorio e punteggio massimo come criterio di vittoria.
- [x] **T-17.2.4** Rifiutare con `400` il budget non positivo, come fa già il percorso denso.
- [x] **T-17.2.5** **Gate di qualità**: chiedere approvazione del codice.
- [x] **T-17.2.6** Test: predicato che ammette, predicato che esclude, due voci sullo stesso record, budget esaurito.
- [x] **T-17.2.7** Verificare: `dotnet test tests/JigenStoreTests/JigenStoreTests.csproj`.

### Fine card

Parità funzionale con la ricerca densa su filtro e raggruppamento, con esattezza garantita.

---

## T-17.3 Fusione denso e sparso

Requisiti: R-39, K-40. Caso d'uso: UC-21. Dipendenze: T-17.1, T-04.

### Passi

- [x] **T-17.3.1** Definire in `src/Server/Jigen.Core/` la query ibrida: vettore denso facoltativo, query sparsa facoltativa, pesi, `top`, filtro.
- [x] **T-17.3.2** Implementare la fusione per posizione su due elenchi già ordinati, con la costante dichiarata nel codice e nella documentazione.
- [x] **T-17.3.3** Rifiutare: nessuna modalità fornita; pesi entrambi a zero; pesi mancanti quando entrambe le modalità sono presenti.
- [x] **T-17.3.4** Con un peso a zero, eseguire una sola ricerca e restituirne il risultato, senza fusione.
- [x] **T-17.3.5** Marcare ogni risultato con la modalità di provenienza e con il punteggio originale accanto a quello fuso.
- [x] **T-17.3.6** **Gate di qualità**: chiedere approvazione del codice.
- [x] **T-17.3.7** Test con tabella di pesi `0`, `0,5`, `1`, due elenchi noti e ordine atteso; ripetere dieci volte per dimostrare il determinismo.
- [x] **T-17.3.8** Test: una modalità senza risultati; parità di posizione fra le due modalità.
- [x] **T-17.3.9** Verificare: `dotnet test tests/Jigen.API.Tests/Jigen.API.Tests.csproj`.

### Fine card

Una sola ricerca combinata, nel server, senza confronto fra scale.

---

## T-18.1 Superfici REST e binaria

Requisiti: R-40, K-41. Casi d'uso: UC-19, UC-20, UC-21. Dipendenze: T-16.3, T-17.1, T-17.3.

### Passi

- [x] **T-18.1.1** Estendere `src/Server/Jigen.Grpc/Protos/Jigen.proto`: messaggio del vettore sparso, campo nei messaggi di scrittura, richieste di ricerca sparsa e ibrida, campi di risposta. Campi nuovi con numeri nuovi; mai riusare il campo denso.
- [x] **T-18.1.2** Rigenerare il codice dal protocollo con il target `Grpc.Tools` esistente.
- [x] **T-18.1.3** Estendere i comandi di `Jigen.Core` già esistenti (`SetVector`, `AppendVector`, `SetDocument`, …) con la sezione sparsa, invece di aggiungere comandi paralleli.
- [x] **T-18.1.4** Implementare i metodi nel servizio `Jigen.Grpc/Service.cs`, traducendo gli errori di validazione in `Status.InvalidArgument` con il termine in errore.
- [x] **T-18.1.5** Aggiungere le rotte REST accanto alle esistenti in `CollectionsController`, con i DTO in `Jigen.API/Dto`, e la mappatura degli errori in `400`.
- [x] **T-18.1.6** Aggiornare l'OpenAPI: le nuove rotte devono comparire nella documentazione generata.
- [x] **T-18.1.7** **Gate di qualità**: chiedere approvazione del codice.
- [x] **T-18.1.8** Matrice di test in `tests/Jigen.API.Tests` e `tests/Jigen.Grpc.Tests`: tre operazioni per due superfici, confronto degli esiti.
- [x] **T-18.1.9** Test di errore: termini non ordinati, peso negativo, oltre il massimo, pesi a zero nella fusione.
- [x] **T-18.1.10** Verificare: build e i due progetti di test.

### Fine card

Stesso stato e stesso ordine su entrambe le superfici, dimostrato da una matrice.

---

## T-18.2 Client .NET e Python

Requisiti: R-40. Casi d'uso: UC-19, UC-20, UC-21. Dipendenze: T-18.1. Skill Python assente: si seguono le convenzioni del pacchetto.

### Passi

- [x] **T-18.2.1** In `src/Client/Jigen.Client/BaseTypes/VectorEntry.cs`, aggiungere `Sparse` con il tipo condiviso, senza duplicare la validazione.
- [x] **T-18.2.2** In `VectorCollection.cs`, aggiungere scrittura con termini, ricerca sparsa e ricerca ibrida, con la stessa forma dei metodi densi.
- [x] **T-18.2.3** In `src/Client/Jigen.Python/src/jigen/`, aggiungere i tipi e i metodi equivalenti, seguendo la struttura dei moduli esistenti.
- [x] **T-18.2.4** Aggiornare gli esempi in `src/Client/Jigen.Python/examples/` e in `docs/client/`.
- [ ] **T-18.2.5** **Gate di qualità**: chiedere approvazione del codice.
- [x] **T-18.2.6** Test .NET in `tests/JigenClientTest/`: andata e ritorno, errore di validazione propagato con il messaggio del server.
- [x] **T-18.2.7** Test Python in `src/Client/Jigen.Python/tests/`: gli stessi due casi.
- [x] **T-18.2.8** Verificare: `dotnet test tests/JigenClientTest/...` e `pytest`.

### Fine card

Due client allineati al contratto, con esempi eseguibili.

---

## T-18.3 Workbench e documentazione

Requisiti: R-40, R-34, K-25, K-28. Casi d'uso: UC-20, UC-21. Dipendenze: T-18.1.

Skill: `capabilities-interface-design` dichiarata per estendere una superficie complessa; nessuna skill per Naive UI, si usano i controlli già presenti nella vista.

### Passi

- [x] **T-18.3.1** In `src/Management/Jigen.Insight/src/service/api/collections.ts`, aggiungere le chiamate alle nuove rotte.
- [x] **T-18.3.2** In `src/Management/Jigen.Insight/src/views/workbench/index.vue`, aggiungere il selettore di modalità (densa, sparsa, ibrida) riusando il controllo già usato per le collezioni.
- [x] **T-18.3.3** Aggiungere i campi dei termini e dei pesi della fusione, con validazione locale che ripete quella del server solo dove serve a evitare un invio inutile.
- [x] **T-18.3.4** In `results-table.vue`, aggiungere le colonne della provenienza e del punteggio originale; in `timing-strip.vue`, il tempo della ricerca sparsa.
- [x] **T-18.3.5** Aggiungere le etichette in `src/Management/Jigen.Insight/src/locales/langs/en-us.ts`, usando i termini congelati nel [riferimento all'esperienza](../ux-reference.md) (ux-reference.md). Nessuna seconda lingua.
- [x] **T-18.3.6** Verifica della tastiera sulle nuove parti: selettore, campi, invio, tabella.
- [ ] **T-18.3.7** **Gate di qualità**: chiedere approvazione del codice.
- [x] **T-18.3.8** Aggiornare `docs/server/rest-api.md`, `docs/server/grpc-api.md`, `docs/in-process/store-options.md` e `docs/concepts/` con le nuove operazioni e opzioni.
- [x] **T-18.3.9** Verificare: `npm run typecheck`, `npm run build`, e una prova manuale dal workbench sulle tre modalità.

### Fine card

S-04.4 estesa con le etichette congelate; documentazione allineata.

---

## T-17.4 Potatura dei candidati — rinviata

Requisiti: R-37, K-39. Stato: rinviata fino alla prova U3 dello scouting.

- [-] **T-17.4.1** Misurare la perdita: confronto fra ricerca potata e ricerca esaustiva senza filtro.
- [-] **T-17.4.2** Implementare il limite superiore per termine.
- [-] **T-17.4.3** Disattivare la potatura quando un filtro è attivo.
- [-] **T-17.4.4** Dichiarare nella risposta se la potatura è stata usata.

Condizione per riaprirla: U3 mostra un guadagno misurabile con perdita nulla o dichiarata e accettata.

---

## T-19 Generazione del vettore sparso nel modulo di embedding

Requisiti: R-42, K-46, K-47. Dipendenze: T-16.1, T-17.1. Eseguita.

### Passi

- [x] **T-19.1** Aggiungere al contratto di embedding la generazione dello sparso (`GenerateSparseEmbedding`, `SparseEmbedding`, `SparseEmbeddingOptions`), senza toccare i metodi densi.
- [x] **T-19.2** Implementarla nell'`OnnxEmbeddingGenerator`: termine = id del tokenizer, peso = `Offset + LogBase × ln(occorrenze)`, esclusione dei marcatori di sequenza, tetto dei termini.
- [x] **T-19.3** Delegare nel `QueuedEmbeddingGenerator` senza passare dalla coda: nessuna inferenza da serializzare.
- [x] **T-19.4** DTO e comandi del modulo (`SparseEmbeddingDto`, `VectorPairDto`, `CalculateSparseEmbedding`, `CalculateSparseEmbeddingBatch`, `CalculateVectorPair`, `CalculateVectorPairBatch`), in forma serializzabile per il worker remoto.
- [x] **T-19.5** Handler del modulo per i quattro comandi.
- [x] **T-19.6** Configurazione in un solo oggetto: `JigenEmbeddings:Sparse` e override per modello.
- [x] **T-19.7** Scrittura con frase: una dispatch per entrambi gli slot, il vettore del chiamante prevale, senza frase nulla viene generato.
- [x] **T-19.8** Stream di documenti gRPC: `CalculateVectorPairBatch` sulla finestra, non una chiamata per documento.
- [x] **T-19.9** Superfici esplicite: RPC `CalculateSparseEmbedding`(`Batch`) e rotta REST `/api/embeddings/sparse`.
- [x] **T-19.10** Errore esplicito quando la generazione produce uno sparso e il registro di scrittura è spento.
- [x] **T-19.11** Test della generazione su modello reale: unicità e ordine dei termini, peso crescente con le occorrenze, nessun token speciale, determinismo, tetto dei termini, sovrapposizione fra testi.
- [x] **T-19.12** Test end-to-end su archivio reale: frase → due slot → rilettura → ricerca lessicale.
- [x] **T-19.13** Test delle superfici: la frase riempie i due slot, lo sparso del chiamante prevale.
- [x] **T-19.14** Documentazione: configurazione degli embedding, concetto, uso del client, REST, gRPC, opzioni del deposito.
- [x] **T-19.16** La generazione copre anche la ricerca ibrida: `Sentence` nel contratto di query, risoluzione nell'handler, campo nuovo su gRPC, passaggio da REST, overload con frase nei due client. Nessun peso richiesto quando è la frase a produrre il secondo vettore.
- [ ] **T-19.17** Testare la risoluzione dei vettori nell'handler: `Jigen.Handlers` non ha un progetto di test, quindi la regola è coperta solo dal contratto e dalle superfici. Serve un progetto di test o un punto di verifica equivalente.
- [ ] **T-19.15** **Gate di qualità**: chiedere approvazione del codice.

### Fine card

Una frase basta a rendere una voce ricercabile in denso, in lessicale e in ibrido.

---

## Punti in cui lo sparso si perde in silenzio

Cinque trappole verificate nel codice. Ognuna ha una voce che la copre.

| # | Punto | Cosa succede | Voce |
| --- | --- | --- | --- |
| 1 | Fan-out dell'indicizzazione, `Writer.EnqueueForIndexing` | La voce ricostruita copia solo `Id`, `CollectionName`, `Embedding`, `Content`: i termini spariscono prima dell'indice | T-16.5.6 |
| 2 | `VectorEntry.Embedding` vuoto contro `Sparse` assente | Con una `ReadOnlyMemory` vuota non si distingue «non specificato» da «cancella»: lo sparso usa `null` come assente, e il vuoto è rifiutato | T-16.1.5 |
| 3 | `ValidateVectorDimensions` esce se il denso è vuoto | Una voce con solo sparso non validerebbe né collezione né termini | T-16.2.7 |
| 4 | `PerformCheckpointCore` fa fsync di tre file | Il quarto file non è durevole quando il registro viene troncato: la scrittura confermata sparisce | T-16.4.1 |
| 5 | `CompactCore` scambia quattro file | Lo sparso non riscritto lascia posizioni che puntano a un file che non corrisponde più | T-16.4.2, T-16.4.3 |

## Tracciabilità

| Requisito | Card | Verifica |
| --- | --- | --- |
| R-36 Conservare il vettore sparso | T-16.1, T-16.2, T-16.3 | Rilettura dopo riavvio |
| R-37 Cercare per vettore sparso | T-17.1, T-17.2 | Confronto con esaustiva |
| R-38 Filtro e raggruppamento | T-17.2 | Parità con la ricerca densa |
| R-39 Fondere denso e sparso | T-17.3 | Tabella dei pesi e determinismo |
| R-40 Parità fra superfici e client | T-18.1, T-18.2, T-18.3 | Matrice per superficie |
| R-41 Osservare l'indice dei termini | T-16.5 | Metriche e misura di memoria |

## Avanzamento

| Card | Stato | Evidenza |
| --- | --- | --- |
| T-16.1 Vocabolario dello sparso | `[x]` | 12 test in `SparseVectorTests`, 78 verdi in PrimitiveTests |
| T-16.2 Sezione sparsa nel registro | `[x]` | 4 test in `SparseWalTests` |
| T-16.3 File sparso, posizioni e cancellazione | `[x]` | 8 test in `SparseStorageTests` |
| T-16.4 Checkpoint, compattazione, elenco file | `[x]` | checkpoint e shrink coperti in `SparseStorageTests` |
| T-16.5 Indice dei termini e osservazione | `[x]` | `SparseIndex` + metriche in `CollectionInfo`; ricostruzione verificata |
| T-17.1 Ricerca sparsa esatta | `[x]` | confronto con ricerca esaustiva su 1.000 voci, tre query |
| T-17.2 Filtro e raggruppamento | `[x]` | filtro e gruppi per record condiviso coperti |
| T-17.3 Fusione denso e sparso | `[x]` | `RankFusion` + `SearchHybrid`; 8 test in `RankFusionTests`, 3 in `SparseSearchTests` |
| T-18.1 Superfici REST e binaria | `[ ]` | Fuori da questo passaggio |
| T-18.2 Client .NET e Python | `[ ]` | — |
| T-18.3 Workbench e documentazione | `[ ]` | — |
| T-17.4 Potatura dei candidati | `[-]` | Rinviata a U3 |
