# Development dossier

Fase di sviluppo. Esegue le decisioni di [analisi](../analysis/dossier.md) (analysis/dossier.md) e [architettura](../architecture/dossier.md) (architecture/dossier.md). Non le riapre: una deviazione passa da `phase-development-deviation-routing`.

Incremento corrente: **vettori sparsi**, R-36…R-41.

## Actor task planning

Piano eseguibile in [Task plan](task-plan.md) (task-plan.md); elenco atomico dei passi in [Task list](tasklist.md) (tasklist.md).

| Voce | Valore confermato |
| --- | --- |
| Attori | Uno, identificativo `A1` |
| Parallelismo | Sequenziale: una card alla volta, nessuna scrittura concorrente |
| Responsabilità | Produzione, test e verifica della card in corso; nessuna delega |
| Revisione di base | `b706e06` |
| Stato del piano | Confermato il 2026-10-21 |
| Ambito di questo passaggio | Solo piano: nessuna modifica al codice |

Blocchi aperti: nessuno. Le prove U1, U3 e U4 dello [scouting](../scouting/sparse-vectors/dossier.md#unknowns-plan) (scouting/sparse-vectors/dossier.md#unknowns-plan) non bloccano le card; ne misurano gli esiti e possono trasformare T-17.4 da rinviata a necessaria.

## Technology resolution

Risoluzione unica per tutta la fase, dallo [Stack Profile](../architecture/StackProfile.md) (architecture/StackProfile.md).

| Slot dello stack | Tecnologia | Skill di implementazione | Note |
| --- | --- | --- | --- |
| Piattaforma backend | .NET 10 / C# | `implementation-be-dotnet-dev` | Quattro progetti per modulo, dipendenze a senso unico |
| Ricerca vettoriale | Jigen.Store + Jigen.Indexer.HNSW | `implementation-be-dotnet-jigen-store` | Archivio, indici, ciclo di vita del database |
| Mediazione | Hikyaku 9.4.3 | `implementation-be-dotnet-hikyaku` | Comandi e query, `Send` non bloccante |
| Framework frontend | Vue 3 | `implementation-fe-vue-dev` | Struttura, routing, stato, i18n |
| Libreria di componenti | Naive UI 2.44.1 | **nessuna** | **Lacuna**: nessuna skill copre Naive UI. Le due skill del slot realizzano Element Plus e shadcn-vue, librerie che il profilo non ha scelto |
| Client Python | Python 3.10+ / gRPC | **nessuna** | **Lacuna**: nessuna skill copre il client Python. Nel piano la card T-18.2 segue le convenzioni del progetto |
| Test .NET | xUnit 2.9.3 | `implementation-be-dotnet-dev` | Progetti per area |
| Qualità interfaccia | `vue-tsc`, oxlint, eslint | `implementation-fe-vue-dev` | Comandi del progetto |

**Base sempre attive.** `base-be-base-rules` per ogni card backend; `base-fe-base-rules` per T-18.3.

**Capability attivate da task.** `capabilities-interface-design` per T-18.3, che estende una superficie complessa già esistente: decide come, non cosa. `capabilities-dev-problem-solving-methodology` quando una card incontra un esito inatteso.

**Conflitti fra skill e architettura.** Nessuno rilevato. Le due lacune non si colmano improvvisando: le card si attengono al profilo e alle convenzioni del repository.

**Slot non risolti per scelta.** Indice dei termini: nessuno slot dello stack lo dichiara come tecnologia separata, perché è codice proprietario dentro `Jigen.Store`, coperto da `implementation-be-dotnet-jigen-store`.

## Project conventions

Lette prima di toccare un file.

| Voce | Convenzione adottata |
| --- | --- |
| Struttura | Quattro progetti per modulo: Host, API, Core, Handlers |
| Naming | Tipi `PascalCase`, campi privati `_camelCase`, un file per tipo |
| Nullabilità | Nullable abilitata; `[]` per le collezioni vuote, non `null` |
| Linguaggio | Forma C# corrente dove la versione effettiva lo consente |
| Formattazione | Due spazi, graffe su riga propria, stile dei file circostanti |
| Test | xUnit, un progetto per area, nomi che descrivono il comportamento |
| Verifica | `dotnet build Jigen.sln -m:1` e `dotnet test <progetto>`; niente comandi arbitrari |
| Temporanei | Solo sotto `.temp/`, mai `/tmp` |
| Migrazioni | Nessuna generazione automatica; il formato del registro è codice, non EF |

## Configuration options

Le opzioni dello sparso vivono in un oggetto di configurazione, non lette sparse nel codice.

| Opzione | Dove | Predefinito | Requisito |
| --- | --- | --- | --- |
| `SparseOptions.MaxTerms` | `Jigen.Store` | 1024 (`SparseVector.DefaultMaxTerms`) | K-36 |
| `ISparseIndex SparseIndex` | `Jigen.Store` | `null` negli host che non lo impostano | K-35, K-38 |
| `JigenServer:Index.SparseEnabled` | `appsettings.json` | `true` | K-35 |
| `JigenServer:Index.SparseMaxTerms` | `appsettings.json` | 1024 | K-36 |
| `WalOptions.Enabled` | `Jigen.Store` | spento; obbligatorio quando una voce porta termini | K-06, K-44 |

La potatura dei candidati (K-39) resta rinviata: non esiste un'opzione di configurazione perché non esiste la funzione (T-17.4).

## Task execution log

Esecuzione del 2026-10-21: card T-16.1…T-16.5 e T-17.1…T-17.3. Attore `A1`, sequenziale.

| Card | Esito | Evidenza |
| --- | --- | --- |
| T-16.1 Vocabolario dello sparso | chiusa | `SparseVector`, `SparseOptions`, `VectorEntry.Sparse`; 12 test in `tests/PrimitiveTests/SparseVectorTests.cs`; `PrimitiveTests` 78/78 |
| T-16.2 Sezione sparsa nel registro | chiusa | `WalRecord` con sezione variabile, replay, transazione, rifiuto senza registro; 4 test in `tests/JigenStoreTests/SparseWalTests.cs` |
| T-16.3 File sparso, posizioni e cancellazione | chiusa | `src/Jigen/Jigen.Store/StoreSparse.cs`, registro delle posizioni proprio, tombstone, riconciliazione; 8 test in `SparseStorageTests.cs` |
| T-16.4 Checkpoint, compattazione, elenco file | chiusa | fsync nel checkpoint, compattazione e scambio a cinque file, `DeadSparseBytes` in `DeadBytes`, `GetFileNames`; coperti da `SparseStorageTests` |
| T-16.5 Indice dei termini e osservazione | chiusa | `src/Jigen/Jigen.Store/Indexers/SparseIndex.cs`, `ISparseIndex`, `SparseIndexInfo`, metriche in `CollectionInfo`; ricostruzione dall'archivio verificata |
| T-17.1 Ricerca sparsa esatta | chiusa | `Store.SearchSparse`; confronto con ricerca esaustiva su 1.000 voci, tre query |
| T-17.2 Filtro e raggruppamento | chiusa | `SearchSparse` con `IFilterExpression`, `SearchSparseByMetadata` con budget |
| T-17.3 Fusione denso e sparso | chiusa | `RankFusion` in `Jigen.Primitives`, `Store.SearchHybrid`; 8 test in `RankFusionTests`, 3 in `SparseSearchTests` |
| T-16 e T-17 complessive | chiuse | `JigenStoreTests` 106/115 (6 fallimenti preesistenti, vedi sotto), `PrimitiveTests` 78/78, `HnswTest` 30/30, `JigenClientTest` 22/26, `Jigen.API.Tests` 57/57 |

Esecuzione del 2026-10-22: T-18.1, messa in servizio dell'indice e misura K-42. Attore `A1`, sequenziale.

| Card | Esito | Evidenza |
| --- | --- | --- |
| T-18.1.1 Protocollo esteso | chiusa | `Jigen.proto`: messaggio `SparseVector`, campo `Sparse` su `Vector` e `Document` (numero 6, il 5 resta il denso), `GetSparse`, `SearchSparse`, `SearchSparseByMetadata`, `SearchHybrid`, campi di provenienza su `SearchVectorResult` |
| T-18.1.2 Rigenerazione | chiusa | `Grpc.Tools` rigenera `Generated/Jigen.cs` e `Generated/JigenGrpc.cs`, versionati nel repository |
| T-18.1.3 Comandi estesi | chiusa | `SetVector`, `AppendVector`, `SetRawVector`, `SetDocument`, `AppendDocument` portano `Sparse`; nessun comando parallelo |
| T-18.1.4 Metodi del servizio | chiusa | `Service.cs`: quattro operazioni nuove; `ArgumentException` → `InvalidArgument`, `InvalidOperationException` (scrittura senza registro) → `FailedPrecondition` |
| T-18.1.5 Rotte REST | chiusa | `CollectionsController`: `GET vectors/{key}/sparse`, `POST search/sparse`, `POST search/sparse-by-metadata`, `POST search/hybrid`; rigetto → `400` con il messaggio del server |
| T-18.1.6 OpenAPI | chiusa | Il documento è generato a runtime (`AddOpenApi`/`MapOpenApi`): non esiste un artefatto da aggiornare, le rotte compaiono per scoperta e gli attributi `ProducesResponseType` ne fissano gli esiti |
| T-18.1.7 Gate di qualità | **in attesa** | Codice pronto per l'approvazione |
| T-18.1.8 Matrice di test | chiusa | `tests/Jigen.API.Tests/CollectionsControllerSparseTests.cs` (19 casi) e `tests/Jigen.Grpc.Tests/ServiceSparseTests.cs` (13 casi), stessi nomi di caso sulle due superfici: scrittura con termini, lettura, ricerca, raggruppamento, fusione |
| T-18.1.9 Test di errore | chiusa | Termini non ordinati, peso negativo, pesi tutti a zero, oltre il massimo, fusione con entrambi i pesi a zero, fusione senza pesi dichiarati, scrittura senza registro |
| T-18.1.10 Verifica | chiusa | `Jigen.API.Tests` 76/76 (erano 57), `Jigen.Grpc.Tests` 35/35 (erano 22) |
| Messa in servizio dell'indice | chiusa | `JigenServer:Index.SparseEnabled` e `SparseMaxTerms` in `JigenServerSettings` e nei due `appsettings`; `DatabasesManager.OpenStore` costruisce `SparseIndex` con la stessa factory di creazione e riapertura |
| Messa in servizio del registro di scrittura | chiusa | `JigenServer:Wal` (`Enabled`, `Durability`, `MaxGroupDelayMs`, `MaxGroupBatchCount`) in `JigenServerSettings` e nei due `appsettings`; `DatabasesManager.BuildWalOptions` lo applica a ogni database. Senza il registro lo sparso è rifiutato (K-44) |
| K-42 Misura a 100.000 voci | chiusa | `tests/JigenStoreTests/SparseIndexMeasurementTests.cs`; numeri e soglia sotto |

**Documentazione aggiornata.** `docs/server/configuration.md` (sezione `JigenServer:Wal`, opzioni `SparseEnabled`/`SparseMaxTerms`, costo e soglia dell'indice lessicale), `docs/in-process/store-options.md` (`Sparse`, `SparseIndex`, i due file sparso, l'obbligo del registro).

**Misura K-42.** Banco deterministico: 100.000 voci, 6 termini ciascuna, distribuzione dei termini sbilanciata (`Vocabolario^(u^2.5)` su 50.000 termini), 600.000 elenchi, 12,06 elenchi per termine in media.

| Grandezza | Valore misurato | Note |
| --- | --- | --- |
| Termini distinti | 49.761 | |
| Elenchi (posting) | 600.000 | |
| Costruzione dell'indice | 352–458 ms | una passata, dalla prima ricerca della collezione |
| Memoria, modello dell'indice (`SparseIndexInfo.MemoryBytes`) | 37,4 MB — 373,9 B per voce | stima conservativa dell'indice |
| Memoria, heap gestito misurato | 62,4 MB — 623,6 B per voce | `GC.GetTotalMemory(true)` prima e dopo la costruzione |
| Ricerca esatta a 6 termini | 4,8–6,1 ms | media su 50 esecuzioni dopo il riscaldamento |
| **Soglia dichiarata** | **800 B per voce** | misurato 623,6 B più margine; oltre la soglia l'indice deve passare su disco |

La soglia è scritta in [configuration.md](../../../docs/server/configuration.md) accanto alle due opzioni, con l'avvertenza di dimensionare sulla misura e non sulla soglia. Il test asserisce entrambe le grandezze contro la soglia e il tempo di costruzione contro un tetto di 30 s: una regressione dell'ordine di grandezza rompe la build.

Esecuzione del 2026-10-22 (seconda tornata): chiusura delle lacune di T-18.1, T-18.2 e T-18.3. Attore `A1`, sequenziale.

### Lacune di T-18.1 chiuse dopo la verifica

La verifica della tornata precedente aveva trovato cinque punti in cui lo sparso si perdeva o non era esposto. Ognuno ha ora un test che lo copre.

| Lacuna | Correzione | Copertura |
| --- | --- | --- |
| `SetDocuments` perdeva lo sparso dei documenti in streaming | `IngestDocumentsWindow` propaga `Sparse` | `SetDocuments_KeepsTheSparseSlotOfEveryStreamedDocument` |
| Le rotte REST dei vettori rifiutavano una voce con il solo sparso | La scrittura accetta `embeddings` **o** `sparse`; senza nessuno dei due resta `400` | `SetVector_WithOnlyTheSparseVector_IsAccepted`, `SetVector_WithoutAnyVector_ReturnsBadRequest` |
| La fusione rifiutava solo il caso di **entrambi** i pesi assenti | Con entrambi i vettori servono **entrambi** i pesi, su gRPC e su REST | `SearchHybrid_WithBothVectorsAndOnlyOneWeight_*` |
| Il tetto dei termini non si applicava alle query | Gli handler rivalidano la query contro `SparseOptions.MaxTerms` del deposito | percorso di validazione condiviso con la scrittura |
| Le metriche dell'indice non arrivavano su gRPC | `CollectionSparseInfoResponse` in `CollectionInfoResponse` | `GetCollectionInfo_ReportsTheTermIndex`, `GetCollectionInfo_LeavesSparseAbsent_WhenTheIndexIsDisabled` |

### T-18.2 Client .NET e Python

| Passo | Esito | Evidenza |
| --- | --- | --- |
| T-18.2.1 Tipo del vettore sparso nel client | chiusa | `BaseTypes/SparseVector.cs` (`Create`, `FromPairs`), `VectorEntry<T>.Sparse` |
| T-18.2.2 Scrittura, ricerca sparsa e ibrida | chiusa | `AddAsync(key, content, embeddings, sparse)`, `GetSparseAsync`, `SearchAsync(sparse)`, `SearchHybrid`/`SearchHybridAsync`, `AddRangeAsync` con la tupla a quattro elementi |
| T-18.2.3 Client Python | chiusa | `models.SparseVector`, `CollectionSparseInfo`, `upsert(..., sparse=)`, `get_sparse`, `search_sparse`, `search_hybrid` (sync e async); protobuf rigenerato |
| T-18.2.4 Esempi e documentazione | chiusa | `examples/basic.py`, `docs/client/usage.md`, `docs/client/python.md` |
| T-18.2.5 Gate di qualità | **in attesa** | Codice pronto per l'approvazione |
| T-18.2.6 Test .NET | chiusa | `tests/JigenClientTest/VectorCollectionSparseTests.cs`, 9 casi; `JigenClientTest` 31/35 (4 saltati, preesistenti) |
| T-18.2.7 Test Python | chiusa | `tests/test_sparse.py`, 19 casi; `pytest` 27/27 |
| T-18.2.8 Verifica | chiusa | build del client, `dotnet test`, `pytest` |

### T-18.3 Workbench e documentazione

| Passo | Esito | Evidenza |
| --- | --- | --- |
| T-18.3.1 Chiamate alle nuove rotte | chiusa | `fetchSearchSparse`, `fetchSearchHybrid` in `src/service/api/collections.ts` |
| T-18.3.2 Selettore di modalità | chiusa | Quattro modalità (Sentence, Embeddings, Sparse, Hybrid) sullo stesso `NRadioGroup` già presente |
| T-18.3.3 Campi dei termini e dei pesi | chiusa | Campo `term:weight`, pesi della fusione, validazione locale solo per evitare un invio inutile |
| T-18.3.4 Colonne di provenienza e tempi | chiusa | `foundBy`, `denseScore`, `sparseScore` in `results-table.vue`; tempo della ricerca sparsa nella striscia dei tempi |
| T-18.3.5 Etichette | chiusa | `en-us.ts` con i termini congelati (Sparse vector, Sparse terms, Term, Weight, Mode, Fusion); schema `I18nKey` aggiornato |
| T-18.3.6 Tastiera | chiusa | I controlli sono quelli nativi già in uso (radio, input, numero), raggiungibili da tastiera; invio dal campo dei termini |
| T-18.3.7 Gate di qualità | **in attesa** | Codice pronto per l'approvazione |
| T-18.3.8 Documentazione | chiusa | `docs/server/rest-api.md`, `docs/server/grpc-api.md`, `docs/in-process/store-options.md`, nuova pagina `docs/concepts/sparse-vectors.md` collegata da `docs/index.md` |
| T-18.3.9 Verifica | chiusa | `npm run typecheck` e `npm run build` verdi |

**Stato delle suite.** `Jigen.API.Tests` 79/79, `Jigen.Grpc.Tests` 39/39, `JigenClientTest` 31/35 (4 saltati), `PrimitiveTests` 78/78, `JigenStoreTests` 108/117 (6 fallimenti preesistenti), `pytest` 27/27, `vue-tsc` e `vite build` verdi. Build del server: 0 errori.

**Skill attivate.** `base-be-base-rules`, `implementation-be-dotnet-dev`, `implementation-be-dotnet-jigen-store`, `base-fe-base-rules`, `implementation-fe-vue-dev`, `implementation-fe-vue-element-plus`, `capabilities-interface-design` (dichiarata per l'estensione del workbench), `base-dossier-writing`.

Esecuzione del 2026-10-22 (terza tornata): T-19, generazione del vettore sparso nel modulo di embedding. Attore `A1`, sequenziale.

Requisito R-42 (nuovo), decisioni K-46 e K-47 (nuove). La richiesta contraddiceva l'esclusione «analisi del testo nel server»: l'esclusione è stata ristretta, non aggirata — le statistiche di corpus restano fuori, la generazione lessicale per voce entra.

| Passo | Esito | Evidenza |
| --- | --- | --- |
| Contratto del generatore | chiusa | `IEmbeddingGenerator.GenerateSparseEmbedding`/`Async`, `SparseEmbedding` e `SparseEmbeddingOptions` in `Jigen.SemanticTools`; `QueuedEmbeddingGenerator` delega senza passare dalla coda (nessuna inferenza, nessun motivo di serializzare) |
| Generazione lessicale | chiusa | `OnnxEmbeddingGenerator`: termine = id del tokenizer, peso = `Offset + LogBase × ln(occorrenze)`, tetto `MaxTerms` che tiene i termini più pesanti; i marcatori di sequenza sono esclusi chiedendoli al tokenizer invece di indovinarli |
| Contratti del modulo | chiusa | `SparseEmbeddingDto`, `VectorPairDto`, comandi `CalculateSparseEmbedding`(`Batch`) e `CalculateVectorPair`(`Batch`); il DTO è a soli array per attraversare il mediatore anche verso un worker remoto |
| Configurazione | chiusa | `JigenEmbeddings:Sparse` (globale) e `Models:<name>:Sparse` (per modello, prevale), in un solo oggetto di configurazione |
| Scrittura con frase | chiusa | `CollectionCommandHandlers.ResolveVectors`: una sola dispatch riempie i due slot, il vettore del chiamante prevale, senza frase nulla viene generato |
| Stream di documenti | chiusa | `IngestDocumentsWindow` usa `CalculateVectorPairBatch`, così la finestra da 64 documenti resta una sola dispatch |
| Superfici | chiusa | gRPC `CalculateSparseEmbedding`(Batch), REST `POST /api/embeddings/sparse` con `?model=` |
| Test della generazione | chiusa | `tests/Jigen.SemanticTools.Tests/SparseEmbeddingTests.cs`, 8 casi su modello reale |
| Test end-to-end | chiusa | `tests/JigenStoreTests/GeneratedSparseIntegrationTests.cs`: frase → denso + sparso → scrittura → rilettura → ricerca lessicale che ritrova la voce |
| Test delle superfici | chiusa | `ServiceSparseTests`: la frase riempie i due slot, lo sparso del chiamante prevale, i due RPC mappano termini e pesi; la ricerca ibrida da frase non richiede vettori né pesi e conserva uno sparso esplicito |
| Ricerca ibrida da frase | chiusa | `Sentence` nel contratto in-process, campo 10 su gRPC, passaggio da REST, `SearchHybrid(sentence, …)` nei client .NET e Python; regola in un solo punto (`ResolveQueryVectors`) |
| Documentazione | chiusa | `docs/embeddings/configuration.md` (sezione Sparse vectors con cosa i pesi **non** sono), `docs/concepts/sparse-vectors.md`, `docs/client/usage.md`, `docs/server/rest-api.md`, `docs/server/grpc-api.md`, `docs/in-process/store-options.md` |
| Gate di qualità | **in attesa** | Codice pronto per l'approvazione |

### Verifica su nomic

Il generatore ha due rami di tokenizzazione diversi, e provarli con lo stesso test sarebbe stato un errore: nomic è configurato con `tokenizer.onnx` (una sessione di tokenizzazione), BGE-M3 con `tokenizer.json`. Le due strade sono state provate separatamente sul modello reale.

| Verifica | BGE-M3 (`tokenizer.json`) | nomic (`tokenizer.onnx`) |
| --- | --- | --- |
| Termini prodotti | sì | sì |
| Marcatori di sequenza esclusi | 0, 1, 2 assenti | 0 (PAD), 101 (CLS), 102 (SEP) assenti |
| Stessa parola, stesso termine fra due testi | sì | sì |
| Ripetizione pesa più di una occorrenza singola | sì | sì |
| Determinismo | sì | sì |

Il primo test scritto per nomic usciva in silenzio quando il modello non era installato, quindi non dimostrava nulla: è stato sostituito da un test che fallisce se i file mancano ed è stato esteso alle proprietà lessicali. Vocabolario nomic: 30.528 termini, nessun adattamento richiesto — il termine è già un `int32` accettato dal deposito.

### Divario chiuso su richiesta dell'utente

La generazione copriva la scrittura e non la ricerca. Su richiesta esplicita il divario è stato chiuso: `SearchHybrid` accetta `Sentence` (in-process, REST, gRPC e i due client), e l'handler genera da essa il vettore che manca. UC-21 e R-42 sono stati estesi di conseguenza; la regola vive in un solo punto (`ResolveQueryVectors`) perché REST e gRPC non possano divergere.

Due comportamenti da conoscere:

- i pesi della fusione servono solo quando i due vettori sono **espliciti**: se il secondo lo genera la frase, i due vengono pesati uguali per default;
- un vettore esplicito non viene mai sostituito, quindi una frase accompagnata da uno sparso del chiamante usa quello.

**Divario che resta.** La risoluzione dei vettori vive negli handler e `Jigen.Handlers` non ha un progetto di test: la regola è verificata sul contratto e sulle superfici, non nell'handler stesso. È annotata come T-19.17 nel tasklist invece di essere dichiarata coperta.

### Conseguenza registrata

Una scrittura con la sola frase ora produce anche lo sparso, quindi è una scrittura sparsa: con `JigenServer:Wal:Enabled = false` viene rifiutata. Il rifiuto è esplicito e nomina la causa (`ResolveVectors`), invece di lasciare emergere il messaggio del deposito, che non spiegherebbe da dove viene il vettore. Il registro è attivo per default, quindi il comportamento cambia solo per chi lo ha spento di proposito.

### Scostamenti

| Scostamento | Motivo | Effetto |
| --- | --- | --- |
| Pesi lessicali dal tokenizer, non una testa sparsa del modello | Il `model.onnx` di BGE-M3 installato è un `XLMRobertaModel` con sole uscite `sentence_embedding` e `token_embeddings`: la testa `sparse_linear` non è esportata | Qualità lessicale, non appresa; la stessa interfaccia accoglierà una testa sparsa senza cambiare contratto (K-46) |
| `_jsonTokenizer` tipizzato `SentencePieceTokenizer` invece di `Tokenizer` | Serve l'overload che spegne bos/eos: sulla classe base quel parametro non esiste, e la regola alternativa (dedurre i marcatori da una stringa vuota) non regge — verificata e scartata | Nessuno sul percorso denso: il test BGE-M3 di similarità resta nella sua banda 0.33–0.34 |
| Nessuna modifica al client .NET e Python | Il client manda già la frase; la generazione avviene sul server e non richiede un contratto nuovo | Il client beneficia dello sparso senza una riga in più |

**Stato delle suite.** `PrimitiveTests` 78/78, `Jigen.API.Tests` 79/79, `Jigen.Grpc.Tests` 45/45 (erano 39), `JigenStoreTests` 110/119 (6 fallimenti preesistenti), `JigenClientTest` 33/37 (4 saltati), `pytest` 30/30, `SparseEmbeddingTests` 8/8, `BgeM3TokenizerTests` verde (nessuna regressione del denso). Build: 0 errori su tutti i progetti toccati.

**Instabilità osservata, non riprodotta.** Una delle quattro esecuzioni complete di `JigenStoreTests` ha riportato 7 fallimenti invece di 6; le tre successive e i due test nuovi eseguiti da soli (3 volte) sono tornati verdi. Non è stato possibile attribuire il settimo fallimento, quindi non viene dichiarato risolto né attribuito a questo lavoro: resta un'osservazione da riprendere se ricompare.

**Skill attivate.** `base-be-base-rules`, `implementation-be-dotnet-dev`, `capabilities-dev-problem-solving-methodology` (la scelta della regola dei token speciali è stata falsificata con una prova), `base-project-decision-log`, `base-dossier-writing`.

**Comandi eseguiti.** `dotnet build` dei singoli progetti (la soluzione ha un errore preesistente, vedi sotto); `dotnet test tests/PrimitiveTests`, `tests/JigenStoreTests`, `tests/HnswTest`, `tests/JigenClientTest`, `tests/Jigen.API.Tests`.

**Skill attivate.** `base-be-base-rules`, `implementation-be-dotnet-jigen-store`, `implementation-be-dotnet-dev`.

### Scostamenti dal piano, registrati

| Scostamento | Motivo | Effetto |
| --- | --- | --- |
| `SparseIndex` vive in `Jigen.Store` (`Indexers/SparseIndex.cs`), non in un progetto `Jigen.Indexer.Sparse` | L'indice si ricostruisce leggendo il file dei vettori sparsi e vive sotto lo stesso scrittore; un progetto separato avrebbe richiesto un contratto pubblico di accesso ai dati o `InternalsVisibleTo`, senza aggiungere isolamento | Nessuno sui requisiti; il contratto resta separato in `ISparseIndex`, in `Jigen.Primitives` |
| L'indice è in memoria e si ricostruisce dall'archivio alla prima ricerca (`Store.EnsureSparseIndexBuilt`) | K-38 chiede che sia ricostruibile, non che sia persistito. Una persistenza propria duplicherebbe l'archivio e richiederebbe una misura (K-42) che non esiste ancora | Apertura del database invariata; il costo si paga alla prima ricerca sparsa |
| Aggiunto `Store.SearchHybrid` oltre a `RankFusion` | Senza un punto che usi la fusione, T-17.3 non era verificabile end-to-end | La superficie REST/gRPC resta a T-18.1 |
| `SearchHybrid` restituisce anche i punteggi originali delle due modalità, non solo la provenienza | Serve al contratto di risposta (T-18.1.1) e alla tabella dei risultati del workbench (T-18.3.4): la posizione decide l'ordine, il punteggio di modalità spiega la posizione | Estensione additiva della tupla: i chiamanti esistenti leggono gli stessi nomi |
| Le superfici validano la sola presenza (vettore sparso, budget dei candidati) e la forma dei termini; il tetto dei termini resta al deposito | Il tetto è configurazione del deposito (`SparseOptions.MaxTerms`): replicarlo nella superficie significherebbe tenerne due copie e poterle far divergere | Il messaggio del rifiuto riporta comunque il limite configurato |
| La ricerca ibrida accetta il solo vettore sparso | Il contratto in-process lo permetteva già (`denseQuery` nullo); rifiutarlo sulla rete avrebbe introdotto un'asimmetria fra superfici | Nessuno: senza vettori resta un rifiuto |
| Il tipo del vettore sparso è **ripetuto** nel client .NET invece di essere condiviso con `Jigen.Primitives` | `Jigen.Primitives` è `net10.0` e il client è `net8.0` (pubblicato su NuGet): condividerlo avrebbe richiesto di multi-targetizzare la primitiva o di pubblicarla come dipendenza del client | Le regole di validazione sono le stesse, ripetute in `BaseTypes/SparseVector.cs`, e il server rivalida: il client anticipa l'errore, non decide |
| Il client Python ripete le regole di validazione | Stessa ragione: il pacchetto non dipende dagli assembly .NET | `models.SparseVector` valida la forma; il tetto dei termini resta al server |
| La ricerca ibrida del client .NET si chiama `SearchHybrid`, non è un overload di `Search` | Un secondo argomento `null` era ambiguo fra «nessun predicato» e «nessun vettore sparso»: la compilazione lo ha segnalato | Nome esplicito, nessuna ambiguità; `Search(sparse, ...)` resta un overload perché il primo argomento è il tipo che discrimina |
| Il workbench limita sparso e ibrido a **una** collezione | Le rotte del server sono per collezione, mentre la ricerca densa è multi-collezione: mescolarli avrebbe richiesto un endpoint che l'analisi non prevede | Messaggio esplicito nel workbench; la ricerca densa resta multi-collezione |
| Il tempo della ricerca sparsa è la durata del giro, non una scomposizione per fase | Le rotte sparse e ibride restituiscono solo i risultati: la scomposizione per fase esiste sulla ricerca multi-collezione | Dichiarato nell'etichetta; aggiungere le fasi richiederebbe un cambio di contratto |
| I tipi REST dello sparso sono scritti a mano in `api-types.ts` | `api-schema.d.ts` si genera con `pnpm gen:api-types` contro un server in esecuzione con modelli ONNX, non disponibile in questa sede | Tipi locali documentati; la rigenerazione li sostituirà senza cambiare i nomi |
| WAL abilitato nel server per impostazione predefinita (`JigenServer:Wal`) (`JigenServer:Wal`) | K-06 lo imponeva già ("registro di scrittura attivo per default nel server, con durabilità `Group`") e il server non lo applicava affatto: con K-44 ogni scrittura con termini veniva rifiutata, quindi lo sparso era inutilizzabile via REST e gRPC su un'installazione predefinita. Qui si chiude il divario fra decisione e codice, non si riapre la decisione | Ogni scrittura diventa durevole per gruppo di commit e non solo al checkpoint: il throughput di ingestione scende di circa il 7% (misura in `docs/in-process/store-options.md`). Disattivabile da configurazione, ma allora lo sparso resta rifiutato |
| L'indice nasce in `DatabasesManager.OpenStore` con `SparseEnabled` | Senza istanza l'opzione `SparseIndex` resta `null` nel processo del server e ogni ricerca lessicale torna vuota | La creazione e la riapertura usano la stessa factory, come già per HNSW |

### Difetto preesistente corretto

`WalRecord.TryReadRecord` copiava i byte del vettore nella direzione sbagliata: ogni voce riprodotta dal registro tornava con il vettore denso **azzerato**. Era invisibile perché nessun test leggeva un vettore attraverso il replay. Corretto in `WalRecord.cs`; il test `InsertRecord_RoundTripsTheSparseSection` lo copre insieme alla sezione sparsa.

### Fallimenti preesistenti, non attribuibili a questo lavoro

| Test | Causa |
| --- | --- |
| `ReadWriteTest` (6 casi) | Apre il percorso fisso `/data/jigendb/readwritetest`, dove su questa macchina esiste un database scritto il 14 luglio con un formato di metadati precedente: `Unknown metadata mutation kind` in `SharedMetadataStore.OpenSources`. Il test dipende da `/data/jigendb` e `/data/onnx` e non è isolato |
| `dotnet build Jigen.sln` | `MSB5004: the solution file has two projects named "Jigen"`; errore del file di soluzione, indipendente da queste modifiche. La build è stata eseguita progetto per progetto |
| `HnswTest` 30/30, `JigenClientTest` 22/26 (4 saltati), `Jigen.API.Tests` 57/57 | Verdi: nessuna regressione sul percorso denso |
