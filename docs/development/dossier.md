# Development Dossier

## SP-06E Pesi sparsi appresi di BGE-M3

- Ambito: `Jigen.SemanticTools` (`LearnedSparseHead.cs` nuovo,
  `EmbeddingGenerator.cs`, `QueuedEmbeddingGenerator.cs`, `IEmbeddingGenerator.cs`,
  `SparseEmbedding.cs`), `Jigen.Embedding.Handlers` (`CommandHandlers.cs`,
  `EmbeddingGeneratorRegistry.cs`), test e docs.
- Skill: `base-be-base-rules`, `implementation-be-dotnet-dev`.
- Nuova opzione `Sparse.HeadPath` (solo profilo `BgeM3`, relativa alla cartella
  del modello): carica `sparse_linear.pt` di `BAAI/bge-m3` leggendo lo zip
  PyTorch senza PyTorch. Peso del token `relu(w·h+b)` sui `token_embeddings`
  dell'export ONNX, massimo per termine, marker XLM-R esclusi, `MaxTerms` invariato.
  Senza `HeadPath` i pesi restano lessicali.
- Densa e sparsa dalla stessa inferenza (`GenerateEmbeddingWithSparseAsync`, usato
  da `CalculateVectorPair` quando l'input non ha prefisso di task). Con la testa la
  sparsa passa dalla coda del generatore; in modalità lessicale continua a saltarla.
- Configurazione errata (profilo diverso, file mancante o malformato) fallisce
  all'avvio, prima di aprire le sessioni ONNX.
- Da fare lato operativo: scaricare `sparse_linear.pt` nella cartella del modello
  (qui non scaricabile), reindicizzare le collezioni e ritarare `minSparseScore`.
- Verifica: build `Jigen.slnx` 0 errori; `Jigen.SemanticTools.Tests` 50/50 (9 nuovi
  con testa sintetica nel formato PyTorch); `Jigen.API.Tests` 55/55;
  `Jigen.Grpc.Tests` 25/25. Non verificato con la testa reale.
- Esito: completato, in attesa del file della testa per la prova reale.

## SP-06D Timeout del client web Insight configurabile

- Ambito: `src/Management/Jigen.Insight` (`utils/app-settings.ts`, `main.ts`,
  `public/settings.json`).
- Skill: `base-fe-base-rules`, `implementation-fe-axios-dev`.
- Il timeout axios di default era 10 s: con il server sotto carico (ingest
  massivo) il client chiudeva la richiesta prima della risposta.
- Nuova impostazione runtime `api.timeoutMs` in `settings.json` (default
  300000 = 5 minuti, `0` = nessun limite), applicata all'avvio come default
  dell'istanza axios. Le chiamate con timeout proprio (shrink, `0`) lo
  mantengono.
- Verifica: `tsc --noEmit` 0 errori. Non provato nel browser.
- Esito: completato.

## SP-06C Build e test backend di nuovo verdi

- Ambito: `tests/JigenClientTest`, `tests/Jigen.API.Tests`.
- Skill: `base-be-base-rules`, `implementation-be-dotnet-dev`.
- `JigenClientTest` non compilava dopo SP-05Z: tolti `SearchDense(frase)`,
  gli overload frase+sparso e `sparseFromSentence`. Il test frase+sparso ora
  verifica che il server derivi lo sparso; rimosso il test duplicato.
- `SearchCollection_UsesSearchQueryTask_ForSentence` falliva con
  NullReferenceException: dopo SP-05Y la frase calcola anche lo sparso, ma il
  test non simulava `CalculateSparseEmbedding`. Aggiunto il mock con risultato
  vuoto.
- Nota: `Jigen.API.Tests` e `Jigen.Grpc.Tests` non sono in `Jigen.slnx`; vanno
  compilati e lanciati per progetto.
- Verifica: `dotnet build Jigen.slnx` 0 errori. Superati: JigenStoreTests 89,
  Jigen.Index.Sparse.Tests 127, JigenClientTest 22 (+5 skip), PrimitiveTests 14,
  Jigen.IdentityTests 3, HnswTest 20, Jigen.SemanticTools.Tests 41,
  Jigen.API.Tests 55, Jigen.Grpc.Tests 25.
- Esito: completato.

## SP-06B Shrink da Insight e segmenti sparsi vincolati per dimensione

Stato: backend completato e verificato; frontend da verificare con typecheck.

- Shrink: comando `ShrinkDatabase`, endpoint `POST /api/database/{name}/shrink`
  (policy `database.admin`, `409` se già in corso). In Insight, pagina
  Databases: azione "Shrink" con conferma, senza timeout sulla chiamata.
- Segmenti: rimosso `MaxSegments`. `SparseMergePolicy` sceglie le fusioni per
  dimensione. Un segmento che ha raggiunto `MaxSegmentBytes` (256 MB) non si
  fonde più. I più piccoli si fondono con segmenti più recenti di dimensione
  simile. Sotto `MinSegmentBytes` (1 MB) un segmento conta come 1 MB.
- `SparseCollectionCompactor.CompactSegments` fonde solo i segmenti scelti e
  conserva i tombstone, che restano validi sui segmenti esclusi.
- File: `SparseMergePolicy.cs`, `SparseCollectionCompactor.cs`,
  `SparseOptions.cs`, `StoreSparse.cs`, `ShrinkDatabase.cs`,
  `DatabaseCommandHandlers.cs`, `DatabaseController.cs`; Insight
  `service/api/database.ts`, `views/databases/index.vue`, `en-us.ts`,
  `typings/app.d.ts`; documenti `docs/indexes/sparse.md`,
  `docs/server/rest-api.md`.
- Verifica: `JigenStoreTests` 89/89, `Jigen.Index.Sparse.Tests` 127/127,
  `Jigen.Grpc.Tests` 25/25. `Jigen.API.Tests` 54/55: fallisce
  `SearchCollection_UsesSearchQueryTask_ForSentence`, su codice già modificato
  prima di questa task e non toccato qui. Typecheck di Insight non eseguito:
  `node_modules` incompleto e `pnpm install` fallisce per autenticazione.
- Skill attivate: `phase-development-task-execution`,
  `phase-development-configuration-options`, `base-be-base-rules`,
  `base-fe-base-rules`, `implementation-be-dotnet-dev`.

## SP-06A Buffer dei commit sparsi e compattazione in background

Stato: completato e verificato.

- Problema: ogni lotto di ingest scriveva un segmento sparso; un ingest massivo
  produceva migliaia di segmenti, letti tutti a ogni ricerca.
- Buffer: il writer conta i termini distinti in attesa. Il segmento si scrive
  quando sono almeno `SparseOptions.CommitMinTerms` (1000) oppure quando sono
  passati `CommitMaxDelay` (60 s) dall'ultimo commit della collezione. Una
  cancellazione non forza più il commit: la copia bufferizzata viene scartata.
- Worker: `SparseMaintenanceWorker` gira ogni `MaintenanceInterval` (5 s) e
  committa i buffer scaduti. Le fusioni sono per dimensione (vedi SP-06B).
  Intervallo zero lo disattiva.
- Commit forzato: `Store.FlushSparseIndexes()`, checkpoint WAL (prima del
  troncamento), shrink e `Close`. Senza WAL un crash perde il buffer sparso.
- Lettori: le ricerche tengono un lease sul lettore; la compattazione e il
  rinnovo attendono la fine delle ricerche in corso prima di chiudere i file.
  Il lettore in cache si invalida anche quando cambia il numero di tombstone.
- Server: opzioni in `JigenServer:Sparse`.
- File: `SparseOptions.cs`, `SparseMaintenanceWorker.cs`, `StoreSparse.cs`,
  `Store.cs`, `StoreOptions.cs`, `WalCheckpointer.cs`,
  `SparseCollectionWriter.cs`, `JigenServerSettings.cs`, `DatabasesManager.cs`,
  `docs/indexes/sparse.md`, test `SparseBufferingTests.cs`,
  `SparseCollectionInfoTests.cs` (commit a ogni lotto esplicito).
- Verifica: `JigenStoreTests` 80/80, `Jigen.Index.Sparse.Tests` 127/127,
  `Jigen.Grpc.Tests` 25/25.
- Skill attivate: `phase-development-task-execution`,
  `phase-development-configuration-options`, `base-be-base-rules`,
  `implementation-be-dotnet-dev`.

## SP-05Z Client .NET: la frase implica la metà sparsa

Stato: codice pronto, test del client da aggiornare. Attende approvazione.

- Regola: un metodo che riceve una frase non riceve vettori, quindi non riceve
  nemmeno termini sparsi; il server calcola il denso e deriva lo sparso dalla
  stessa frase. Il flag non esiste più nella superficie del client.
- Rimossi: `sparseFromSentence` da `Add`, `AddAsync`, `AddRangeAsync`, `Search`,
  `SearchAsync`; gli overload `Add`/`AddAsync(key, content, sentence, sparse)` e
  `Add(key, content, sentence, bool)`; `sparseQuery` dalle forme
  `Search`/`SearchAsync(sentence, ...)`; `SearchDense(sentence, ...)` e
  `SearchDenseAsync(sentence, ...)`.
- Restano: `Add`/`AddAsync`/`AddRangeAsync` con frase derivano sempre lo sparso;
  `SearchDense(float[])`, `SearchSparse` e `Search(float[], sparseQuery)` restano
  i percorsi a vettori, dove lo sparso è esplicito.
- Contratto sul filo invariato: REST e gRPC continuano ad accettare il campo,
  così gli altri client non cambiano. Rimuoverlo dal trasporto è una decisione
  separata, non presa qui.
- Documentazione: `docs/client/usage.md` e l'esempio del `README.md` aggiornati.
- File: `src/Client/Jigen.Client/VectorCollection.cs`, `docs/client/usage.md`,
  `README.md`.
- Verifica: build di `Jigen.Client` 0 errori. `JigenClientTest` non compila per
  cinque chiamate alle API rimosse (`VectorCollectionSparseTests` righe 71, 195,
  215; `VectorCollectionSearchTests` righe 101, 141): da riscrivere nella task di
  test dopo l'approvazione, senza modificare la produzione.
- Skill attivate: `phase-development-task-execution`,
  `phase-development-project-conventions`, `base-be-base-rules`,
  `implementation-be-dotnet-dev`.
## SP-05Y Metà sparsa attiva di default nei percorsi ibridi

Stato: codice pronto, una verifica di regressione rossa. Attende approvazione e
una decisione sugli archivi in formato 0.

- Motivo: `SparseFromSentence` era opt-in, ma la decisione di progetto e che la
  metà sparsa sia attiva di default su ricerca e ingestion quando il testo e
  presente; il flag resta solo per rinunciarvi.
- REST: `SearchData`, `DocumentPayload`, `BulkDocumentItem` e
  `SearchCollectionsData` inizializzano il flag a `true`; un `false` esplicito
  nel corpo resta rispettato.
- gRPC: i due campi diventano `optional bool`, cosi l'assenza e distinguibile da
  un `false` esplicito; `Service.cs` mappa l'assenza su `true` in `SetDocument`,
  `AppendDocument` e `SearchDocument`. Stub rigenerati da protoc in build.
- Client .NET: `BuildDocument` e `BuildSearchDocumentRequest` impostano sempre
  il campo e i default passano a `true` (`Search`, `SearchAsync`, `AddRangeAsync`
  per sentenze), quindi `false` continua a significare denso-solo.
- Non toccato: client Python, dove il campo viene ancora inviato solo quando
  richiesto; l'assenza vale `true` lato server, quindi il default effettivo e
  gia quello nuovo, ma un `False` esplicito non disattiva la metà sparsa.
- Archivio formato 0: con il default attivo, una scrittura con frase su
  `WT-Production` o `WT-Staging` ora incontra `NotSupportedException`; la
  decisione se scartare la metà sparsa o lasciare fallire la scrittura e aperta.
- File: DTO REST e Core, due `Jigen.proto`, `Service.cs`, `VectorCollection.cs`,
  stub generati, `rest-api.md`, `grpc-api.md`, `client/usage.md`,
  `client/python.md`, `indexes/sparse.md`.
- Verifica: build di `Jigen.Client` e `Jigen.Grpc` 0 errori; `Jigen.Grpc.Tests`
  25 superati; `JigenClientTest` 23 superati piu 5 saltati;
  `Jigen.API.Tests` 54 superati e 1 fallito
  (`SearchCollection_UsesSearchQueryTask_ForSentence`), perche il test presume
  che una ricerca per frase non richieda la metà sparsa: premessa superata dalla
  decisione, da aggiornare nella task di test dopo l'approvazione.
- Skill attivate: `phase-development-task-execution`,
  `phase-development-project-conventions`, `base-be-base-rules`,
  `implementation-be-dotnet-dev`.
## SP-05X Punteggi separati e soglie nella ricerca del Workbench

Stato: codice pronto. Attende approvazione esplicita del codice.

- Motivo: la ricerca multi-collezione (`POST /api/database/{db}/collections/search`)
  eseguiva solo la meta densa e restituiva il solo `Score`; il client di
  management mostrava quindi un unico punteggio denso, mentre il percorso a
  collezione singola esponeva gia le due meta e le soglie.
- Contratto: `SearchCollectionsData` accetta `SparseFromSentence`,
  `MinDenseScore` e `MinSparseScore`; `CollectionSearchResultItem` espone
  `DenseScore` e `SparseScore` accanto a `Score`, che resta il valore fuso
  (RRF) quando la ricerca e ibrida.
- Backend: con `SparseFromSentence` l'handler deriva la meta sparsa dalla frase
  con `CalculateSparseEmbedding`, poi usa `SearchHybrid` e applica entrambe le
  soglie. Senza meta sparsa usa `SearchDense` con la sola soglia densa.
  Scelta confermata dall'utente: ibrida automatica dalla frase, densa sola in
  modalita embeddings.
- Client: la modalita frase invia `sparseFromSentence`; il form espone le due
  soglie; la tabella e il dettaglio mostrano RRF, denso e sparso su colonne
  separate, con `—` dove la meta non esiste.
- Schema OpenAPI del client aggiornato a mano: la rigenerazione richiede un
  server con queste modifiche, mentre il server raggiungibile su 13223 e una
  build precedente. Alla prossima rigenerazione i campi devono coincidere.
- File: `SearchCollectionsData.cs`, `SearchCollectionsResult.cs`,
  `CollectionQueryHandlers.cs`, `api-schema.d.ts`, `workbench/index.vue`,
  `results-table.vue`, `result-detail-drawer.vue`, `en-us.ts`, `app.d.ts`.
- Verifica: build di `Jigen.API` 0 errori, `Jigen.API.Tests` 55 superati,
  `vue-tsc` senza errori, `oxfmt` eseguito sui file toccati, `eslint` 0 errori
  (resta il warning preesistente sul nome del componente `workbench`).
- Confronto visivo: nessun prototipo congelato nel repository. La form e stata
  aperta nel client in esecuzione a due larghezze (mobile e 1600 px) e mostra le
  due soglie accanto a Top K e al selettore di modalita. Le colonne dei tre
  punteggi non sono state esercitate con risultati reali: l'istanza locale non
  ha database visibili per l'utente e il server remoto rifiuta la redirect URI
  di sviluppo.
- Skill attivate: `phase-development-task-execution`,
  `phase-development-project-conventions`, `base-be-base-rules`,
  `base-fe-base-rules`, `implementation-be-dotnet-dev`,
  `implementation-fe-vue-dev`, `implementation-fe-vue-element-plus` non
  applicabile (il client usa Naive UI), `capabilities-interface-design` non
  attivata: la modifica estende una surface esistente con colonne e due campi.
## SP-05W Un solo nome per la soglia densa: via MinScore

Stato: completato. Attendono approvazione esplicita del codice.

- Motivo: `MinScore` e `MinDenseScore` significavano la stessa cosa su percorsi
  diversi (il primo solo sulla ricerca densa, il secondo solo sulla meta densa
  dell'ibrida). Due nomi per un'unica nozione, e nessuno dei due funzionava dove
  ci si aspettava l'altro.
- Contratto: `MinScore` rimosso. `SearchTuning` riserva il campo 3 (nessun
  riuso: un client vecchio che lo mandava ora riceve un errore di
  deserializzazione invece di un taglio silenzioso). `MinDenseScore` (4) e
  `MinSparseScore` (5) restano gli unici due limiti.
- Semantica unificata: `MinDenseScore` vale sia sulla ricerca densa sia sulla meta
  densa dell'ibrida; `MinSparseScore` sia sulla sparso-sola sia sulla meta sparsa
  dell'ibrida. Il ramo denso dell'handler ora legge `MinDenseScore` (prima
  leggeva `MinScore`), il ramo ibrido era gia corretto.
- Rimossi: `SearchOptions.MinScore` (client .NET), `SearchVector.MinScore`
  (Core), `ApplyTuning` per quel campo, `SearchOptions.min_score` (client
  Python), il mapping nel `_tuning` Python. L'integrazione LangChain mappa
  `score_threshold` su `min_dense_score`, che e il campo che serve davvero.
- REST non aveva `MinScore`: `SearchData.MinDenseScore`/`MinSparseScore` ora
  documentati come validi su entrambi i percorsi.
- Test: 2 nuovi sul client (la ricerca densa manda `MinDenseScore`, la sparsa
  manda `MinSparseScore`), 2 nuovi sul servizio gRPC (i tagli arrivano al Core,
  e restano null quando il tuning non li porta). Aggiornato il test Python che
  usava `min_score`.
- Verifica: soluzione 0 errori, API 55, gRPC 25, client 23 piu 5 saltati, Store
  73, Python 30 con `ruff` pulito, `mkdocs --strict` verde.
- Differenza residua, non toccata: sul ramo denso il taglio avviene dopo che
  l'indice ha restituito il top-k, quindi con `top: 10` e una soglia alta si
  possono ottenere meno di 10 risultati anche se altri documenti sopra soglia
  esistono piu in basso in classifica. Sul ramo sparso la soglia entra nel
  motore, pota e riempie k. Allinearla richiede una soglia nell'`IIndexer`
  (o un over-fetch): decisione dell'utente.
## SP-05V Soluzione in formato .slnx

Stato: completato.

- Il `.sln` non era convertibile cosi com'era: conteneva quattro cartelle di
  soluzione vuote tutte chiamate "Jigen" (quindi lo stesso percorso `/Jigen/`) e
  una dichiarazione duplicata dello stesso GUID con due nomi ("Store" e "Jigen").
  `dotnet sln migrate` falliva con "Duplicate item '/Jigen/' of type 'Folder'".
- Ripulite le cinque dichiarazioni (erano tutte vuote tranne quella reale, che
  resta col nome "Jigen" e contiene i sei progetti del motore), poi
  `dotnet sln migrate`: 37 progetti convertiti.
- Tolti dal nuovo file i quattro progetti sotto `.temp/` e il `Dockerfile` di
  radice, che non esistono piu: con quelli `dotnet restore Jigen.slnx` falliva
  con MSB3202 (anche il `.sln` vecchio era rotto per lo stesso motivo). I
  Solution Items ora puntano ai due Dockerfile reali
  (`src/Server/Jigen/Dockerfile`, `src/Embedding/Jigen.Embedding/Dockerfile`).
  33 progetti finali.
- `Jigen.sln` rimosso. Aggiornati i riferimenti: workflow CI
  (`restore`/`build` su `Jigen.slnx`), README, e `TestStorageDirectory` che
  cercava `Jigen.sln` per trovare la radice del repository (ora accetta
  entrambi i formati).
- Effetto collaterale positivo: il `.sln` non si compilava per il nome di
  progetto duplicato "Jigen" (host e cartella). Con `.slnx` i progetti sono
  identificati dal percorso e `dotnet build Jigen.slnx -m:1` completa con 0
  errori (83 warning, tutti preesistenti).
- Verifica: `dotnet restore Jigen.slnx` verde, `dotnet build Jigen.slnx
  --no-restore -m:1` 0 errori, `dotnet sln Jigen.slnx list` 33 progetti,
  `dotnet test Jigen.slnx --no-build --filter SparseCollectionInfoTests` esegue
  dal file di soluzione.
- Nota: i dossier e i task plan precedenti citano `Jigen.sln` come documenti
  storici; non sono stati riscritti.
## SP-05U Management allineato all'indice sparso

Stato: completato. Attendono approvazione esplicita del codice.

- Prima: la vista di management non sapeva nulla dello sparso. `CollectionInfo`
  riportava solo i byte di contenuto e vettori piu i metrici HNSW;
  `DatabaseDetails` sommava `IndexSize` dei grafi densi, quindi una collezione
  con indice sparso risultava piu piccola di quanto occupasse davvero.
- Nuovo `SparseCollectionInfo` (segmenti, termini, blocchi, dimensione blocco,
  tombstone, byte su disco) esposto da `Store.GetSparseInfo(collection)` e
  agganciato a `CollectionInfo.Sparse`; `Store.GetSparseInfo` riusa il reader che
  serve le ricerche, quindi i numeri descrivono lo stesso stato. Il conteggio dei
  posting non c'e: sommarlo significa leggere ogni blocco.
- `SparseIndex.BlockCount` per il numero di blocchi (O(1) sull'header della lista).
- `DatabaseDetails.SparseSize` sommato ai totali del database.
- Trasporti: `CollectionInfoResponse.Sparse = 7` con
  `SparseCollectionInfoResponse`; REST lo restituisce automaticamente perche
  serializza `CollectionInfo`, e `/details` guadagna `SparseSize`.
- Client Python: `SparseCollectionInfo` e `CollectionInfo.sparse`, con due test.
- Difetto trovato scrivendo i test: una collezione con **un solo segmento** e
  tombstone non veniva mai compattata. `CompactSparseIndexes` chiamava il
  compattatore solo con piu di un segmento, e il compattatore stesso usciva
  subito con `generations.Count <= 1`: le copie ritirate restavano su disco per
  sempre e il file dei tombstone cresceva senza limite. Ora il negozio compatta
  anche con un segmento se ci sono tombstone, e il compattatore riscrive il
  segmento singolo quando deve scartare copie ritirate.
- Verifica: Store 73 su 73 (3 nuovi test sull'info sparsa), sparse 127 su 127,
  gRPC 23 su 23 (2 nuovi), API 55 su 55, client 21 piu 5 saltati, Python 30 su
  30; `mkdocs --strict` verde.
- Non incluso: campionamento dei metrici sparsi nel modulo Metrics (campiona solo
  l'indice denso), grafo sparso nella visualizzazione.
- Skill attive: `phase-development-project-conventions`, `base-be-base-rules`,
  `implementation-be-dotnet-dev`, `capabilities-dev-problem-solving-methodology`,
  `run-tests`.

## SP-05T Nomi del client allineati, vettore sparso dal motore di embedding

Stato: completato. Attendono approvazione esplicita del codice.

- Motore di embedding: `IEmbeddingGenerator.GenerateSparseEmbedding(Async)` e il
  tipo `SparseEmbedding` (termini = id di vocabolario del tokenizer, pesi =
  frequenza con damping logaritmico), con `SparseEmbeddingOptions` (MaxTerms 1024,
  Offset 1, LogBase 1) esposte da `EmbeddingGeneratorOptions.Sparse`. Funziona per
  ogni profilo, `BgeM3` compreso (mappatura XLM-R degli id gia presente nel
  generatore); i marker di sequenza vengono esclusi dal calcolo dei termini.
  Implementato in `OnnxEmbeddingGenerator`, inoltrato da
  `QueuedEmbeddingGenerator` e da `DefaultEmbeddingGenerator`.
- Modulo embedding: DTO `SparseEmbeddingDto`/`VectorPairDto` e comandi
  `CalculateSparseEmbedding` e `CalculateVectorPair` (un solo dispatch per
  entrambi i vettori), con gli handler corrispondenti.
- Trasporti: RPC `CalculateSparseEmbedding` e `CalculateSparseEmbeddingBatch` su
  entrambi i proto; `SearchDocumentRequest.SparseFromSentence = 8` (la meta
  sparsa viene ricavata dalla sentenza che il server gia embedda) e
  `Document.SparseFromSentence = 7` (la scrittura riempie entrambi gli slot). Un
  valore esplicito (`SparseQuery`/`Sparse`) vince sempre su quello derivato.
  REST: `SearchData.SparseFromSentence`, `DocumentPayload` e `BulkDocumentItem`
  idem; il controller calcola la meta sparsa con lo stesso comando.
- Core e handler: `SetDocument`/`AppendDocument` ricevono `SparseFromSentence`;
  `ResolveSparseTerms` usa i termini espliciti o li deriva dalla sentenza.
- Client .NET: nomi allineati all'in-process — `SearchDense`/`SearchDenseAsync`
  (vettore e sentenza), `SearchSparse`/`SearchSparseAsync`, e `Search`/
  `SearchAsync` ora ibrida. `SearchHybrid`/`SearchHybridAsync` rimossi.
  `Search(sentence, ..., sparseFromSentence: true)` fa derivare dal server
  entrambe le meta; `Add`/`AddAsync` hanno la variante con il flag e
  `AddRangeAsync(..., sparseFromSentence, ...)` per il lotto di sentenze.
- Client Python: stessi nomi (`search_dense`, `search_sparse`, `search` ibrida,
  `search_hybrid` come alias), `sparse_from_sentence` su `search`, `upsert` e
  `append`.
- Test: 6 nuovi test del motore (`EmbeddingSparseTests`, deterministico, id
  crescenti, pesi positivi, testo vuoto, peso maggiore con ripetizione, cap
  `MaxTerms`) eseguiti sul tokenizer nomic reale; 4 nuovi test gRPC e 3 REST per
  la meta derivata; 2 nuovi test del client .NET e 4 del client Python.
- Verifica: SemanticTools 6 su 6 (filtro), API 55, gRPC 21, client 21 + 5
  saltati, Python 28, sparse 127, Store 70; tutti i progetti toccati compilano;
  `mkdocs build --strict` verde.
- Limite noto: la generazione e lessicale (TF dal tokenizer), non la testa sparsa
  appresa del modello (SPLADE/BGE-M3); sostituirla significa implementare lo
  stesso metodo dietro `IEmbeddingGenerator`.
- Difetto trovato provando il modello BGE-M3 reale: il ramo SentencePiece usava
  l'overload `EncodeToIds(text, bool, bool)` della classe base `Tokenizer`, che
  sono `considerPreTokenization`/`considerNormalization` e non
  `addBeginningOfSentence`/`addEndOfSentence`: `<s>` e `</s>` finivano fra i
  termini (id 0 e 2). Il campo ora e tipizzato `SentencePieceTokenizer` e la
  chiamata usa i parametri nominati; aggiunto un test che rifiuta gli id 0..3.
  Verificato sul modello reale: 16 termini, id XLM-R, nessun marker, denso a 1024.
- L'export ufficiale di BGE-M3 espone solo `sentence_embedding` e
  `token_embeddings`: nessuna testa lessicale, quindi i pesi sparsi appresi non
  sono calcolabili da quei file. Documentato.
- Confronto con `oldoldold` (d2c4aa4) fatto con un worktree temporaneo e lo stesso
  modello: dopo la correzione, tokenizzazione e uscita coincidono esattamente.
  Su cinque testi gli id e i pesi sono identici; denso (somma -0.745755, sha
  CF1BA5F6...) e sparso (sha 38C62B84.../9628E545...) hanno lo stesso hash nei
  due rami. Il ramo vecchio tipizzava gia il campo `SentencePieceTokenizer` e
  usava l'overload con `addBeginningOfSentence`/`addEndOfSentence`: ero io ad
  averlo involontariamente degradato nel porting.
- Allineati due dettagli al ramo vecchio: cache di `_specialTokenIds` (una
  chiamata di tokenizzazione in meno per vettore generato) e suite di test
  adottata da li (9 test: BGE-M3 e nomic, marker esclusi per entrambi i
  tokenizzatori, termini condivisi fra testi, ripetizione pesata, cap dei
  termini), piu il controllo degli id nel range 4..250001.
- Differenza residua voluta: `TokenizeContentIds` abbassa il testo per il profilo
  `SigLip2`, come fa gia il percorso denso; il ramo vecchio non lo faceva.
- Skill attive: `phase-development-project-conventions`, `base-be-base-rules`,
  `implementation-be-dotnet-dev`, `capabilities-dev-problem-solving-methodology`,
  `run-tests`.

## SP-05S Documentazione allineata allo stato attuale

Stato: completato. Attendono approvazione.

- Nuova pagina `docs/indexes/sparse.md` (in `mkdocs.yml` e in `docs/index.md`):
  cosa indicizza, dove vivono le coppie, le tre ricerche, la fusione RRF, il
  filtro, `minimumScore`, la struttura su disco (segmenti, tombstone, manifest),
  il ciclo di vita nello Store, il modello di costo misurato e i limiti.
- `docs/benchmarks.md`: nuova sezione "Sparse and hybrid search" con le misure di
  SP-05R (corpus di riferimento, corpus realistico, Store 10k e 30k, trasporto
  gRPC) e le letture che ne derivano.
- Corrette le API cambiate in tutta la documentazione: `store.Search` ->
  `SearchDense` (in-process), `VectorEntry.Embedding` -> `Embeddings`,
  `Search(sentence)` -> `SearchDense(sentence)` negli esempi in-process.
  Negli esempi del client `Search` resta la meta densa, con `SearchSparse` e
  `SearchHybrid` accanto.
- Aggiunti i campi sparsi ai riferimenti dei trasporti: `SparseTerms`,
  `Sparse`/`SparseQuery`, `MinDenseScore`/`MinSparseScore`, `DenseScore`/
  `SparseScore` in `docs/server/grpc-api.md`; `SearchData`, `VectorPayload` e
  `DocumentPayload` in `docs/server/rest-api.md`.
- Aggiornati `docs/in-process/overview.md` (indice sparso e directory
  `{name}.sparse/`), `docs/in-process/store-options.md` (layout del record
  vettori con la sezione sparsa e la sua directory), `docs/server/overview.md`,
  `docs/client/usage.md`, `docs/client/python.md` e il README (higlights, quick
  taste).
- Sistemati due link relativi rotti in `docs/scouting/dossier.md`: `mkdocs build
  --strict` ora passa senza avvisi.
- Verifica: `mkdocs build --strict` verde (restano solo le pagine fuori nav).
- Skill attive: `phase-development-project-conventions`, `base-be-base-rules`,
  `implementation-be-dotnet-dev`, `capabilities-dev-problem-solving-methodology`.

## SP-05R Giro di benchmark: sparso, ibrido, Store e trasporti

Stato: misure raccolte in Release sulla stessa macchina delle misure
precedenti. Attendono approvazione.

Protocollo di riferimento del motore sparso (non modificato nei parametri):
3000 documenti, 1-3 vettori per documento, 60 termini per vettore su 200, blocco
64, 200 query da 20 termini generate dopo il corpus, migliore di 3 giri.
Corpus prodotto: 362.220 posting, 5.751 blocchi (lo storico riportava 359.820 e
5.727: la sequenza del generatore non e recuperabile dai log, quindi il corpus
differisce dello 0,7% e le latenze vanno lette con questo margine).

Motore sparso, corpus di riferimento (peggior caso: ogni termine compare nel 60%
dei documenti):
- build 447 ms, 5,97 MB su disco, apertura ~0 ms
- k=1 8,33 ms, k=10 8,58 ms, k=100 8,95 ms, k=3000 11,37 ms; 1.052 KiB per query
  a k=10 (storico 7,76 ms e 1.265 KiB: +10% tempo, -17% memoria)
- k=3000 con `minimumScore` che lascia ~30 documenti: 9,15 ms

Motore sparso, corpus realistico (30.000 documenti, 2 vettori, vocabolario
50.000, quindi ogni termine in ~72 documenti; 3,6 M posting):
- build 1.703 ms, 63,55 MB, 18,5 B/posting, 90.458 blocchi
- 20 termini: k=1 0,84 ms, k=10 0,89 ms (68 KiB), k=100 1,03 ms, k=30000 1,41 ms
- Lettura: il costo e dominato da termini della query x lunghezza delle liste,
  non dalla dimensione del corpus. Lo stesso numero di posting costa 335 ms con
  il vocabolario da 200 termini e 0,89 ms con quello da 50.000.

Store, 10.000 documenti con 2 vettori da 128 dimensioni e 120 termini sparsi
(HNSW M=16 efC=200 efS=80, WAL off):
- accettazione in lotti da 1000: 36 ms (277.555 doc/s); ingestione durevole
  (scrittura + indice): 13.324 ms, 751 doc/s
- sparso k=10 6,48 ms con 2 segmenti, 3,69 ms dopo Shrink, 3,54 ms dopo riapertura
- denso k=10 0,63 ms; ibrido k=10 8,08 ms prima, 4,57 ms dopo la riapertura
- Shrink 490 ms, segmenti da 2 a 1; riapertura 60 ms, prima ricerca 7,75 ms

Store, 30.000 documenti (3,6 M posting, vocabolario 200), costo per forma della
query: 5 termini 9,86 ms, 20 termini 81,63 ms, 60 termini 335,79 ms (k=10).
Indice sparso 59,25 MB, dati 161 MB, Shrink 1.314 ms.

Trasporti, gRPC su database appena creato, 2.000 documenti:
- `SetVector` una chiamata per documento: 313 doc/s (3,20 ms per documento);
  in run precedenti 486-678 doc/s, quindi 1,5-3,2 ms per chiamata
- `SetVectors` in streaming: 79 ms, 25.103 doc/s (da 14 a 80 volte piu veloce)
- `SearchSparse` k=10: 8,32 ms per query round trip incluso; una chiamata unaria
  di riferimento (`Count`) costa 1,68 ms, quindi ~6,6 ms sono il server
- `Clear` ripetuto senza compattazione degrada la ricerca: 8,32 -> 16,82 ->
  24,21 ms per query in tre giri, perche i segmenti restano e i tombstone
  crescono finche non si esegue Shrink.

Denso, benchmark della suite `tests/JigenBenchmarks` (10.000 x 128, due giri):
ingest 1.556-1.780 vettori/s, ricerca HNSW 771-875 us, recall@10 0,992-0,993,
brute force 1.546-1.577 us, riapertura 89-90 ms, vettori 5,15 MB e grafo
7,09 MB. Dimensioni e recall sono identiche ai numeri documentati; latenza e
ingest sono piu lenti dei 1.904-2.059 vettori/s e 559-602 us del documento, che
sono stati misurati su una macchina meno carica.

Letture:
- La fusione dei segmenti costa: a parita di dati, passare da 2 segmenti a 1
  dimezza quasi la latenza sparsa. La compattazione non e un'operazione di
  manutenzione opzionale, e parte del percorso di lettura.
- Un carico con molte cancellazioni o `Clear` peggiora la ricerca finche non si
  compatta: da valutare una compattazione automatica dopo un Clear.
- Il costo della ricerca sparsa dipende dal vocabolario: con termini molto
  frequenti (200 termini, corpus sintetico) servono centinaia di ms, con un
  vocabolario reale si resta sotto il millisecondo sugli stessi posting.
- Sul trasporto il lotto in streaming e l'unica scelta ragionevole per
  l'ingestione; la ricerca paga ~1,7 ms di chiamata, accettabile rispetto a
  qualunque bagaglio lessicale reale.

Harness: temporaneo, in `.temp/bench` durante la sessione, rimosso a fine giro.
Se il giro deve essere ripetibile, i modi `sparse`, `sparse-real`, `store`,
`storeq` e `grpc` vanno promossi in `tests/JigenBenchmarks`.
- Skill attive: `phase-development-project-conventions`, `base-be-base-rules`,
  `implementation-be-dotnet-dev`, `capabilities-dev-problem-solving-methodology`,
  `run-tests`.

## SP-05Q Scrittura delle coppie sparse sui trasporti e test riparati

Stato: completato. Attende approvazione esplicita del codice.

- Proto: nuovo messaggio `SparseTerms { map<int32,float> Terms }`. `Vector` porta
  `repeated SparseTerms Sparse = 7` allineato per indice con `Vectors` (una lista
  piu corta lascia gli ultimi vettori senza termini); `Document` porta
  `SparseTerms Sparse = 6`, cioe i termini del documento quando il server calcola
  l'embedding. Una lista piu lunga dei vettori o un termine negativo viene
  rifiutata: allegare termini a vettori inesistenti o a termini che nessuna query
  puo nominare sarebbe una scrittura muta.
- Core: `SetVector`, `AppendVector`, `SetRawVector` ricevono `SparseVectors`
  (per vettore); `SetDocument`, `AppendDocument` ricevono `SparseTerms`.
- Handler: `ToEntry` porta le coppie nella `VectorEntry`, quindi la scrittura
  segue il percorso gia esistente (WAL, record, indice sparso).
- REST: `VectorPayload` e `BulkVectorItem` accettano `sparse` come array di mappe
  termine/peso; `DocumentPayload` e `BulkDocumentItem` come mappa singola. Un
  payload non valido risponde 400, e nel bulk salta l'elemento invece di
  scriverlo senza i suoi termini.
- gRPC: `Service` converte le mappe in `SparseTerm[]` con le stesse due regole di
  validazione, restituendo `InvalidArgument`.
- Client .NET: `VectorEntry<T>.SparseVectors`, `BuildVector`/`BuildDocument`
  mappano i termini, e `Add`/`AddAsync` hanno le varianti con `sparse` per uno o
  piu vettori e per il documento con sentence.
- Client Python: `upsert`, `append`, `upsert_many` e `upsert_texts` accettano
  `sparse`; una mappa su un entry multivettore viene rifiutata invece di essere
  replicata su tutti i vettori.
- Test riparati: `Jigen.Grpc.Tests` non compilava (ctor di `Server` senza
  `passageOptions`, `Vector.Embeddings` rimosso) e ora passa 17 su 17;
  `Jigen.API.Tests` idem (ctor di `EmbeddingController`) e passa 52 su 52.
  Nel progetto client i test che parlano con un server vivo sono marcati
  `[ServerFact]`/`[ServerTheory]`: si saltano con il motivo quando il server, il
  database o il modulo embedding non ci sono, invece di fallire con un errore di
  trasporto. `Add(key, null)` ora lancia `ArgumentNullException` come il test si
  aspettava, e gli entry dei test di inserimento hanno un vettore (il server
  rifiuta un entry senza vettori).
- Verifica end-to-end reale: server avviato con un database provisionato, test
  `SparseRoundTripTests` eseguito sul client .NET: scrittura con coppie sparse
  via gRPC e ricerca sparsa che ritrova il documento. 27 test, 26 passati e 1
  saltato (modulo embedding non presente nella build di debug).
- Limite noto: REST non e stato provato su un server vivo. L'identity del server
  non espone un modo headless per ottenere un token utente: il flusso password
  non e fra quelli gestiti (`client_credentials`, `authorization_code`,
  `refresh_token`), e un token client non porta ruoli, quindi i criteri di
  autorizzazione dei controller non possono passare da uno script. REST e
  comunque coperto dai test dei controller, incluso il binding JSON dei payload
  sparsi.
- Nota operativa scoperta: lanciando `dotnet bin/.../Jigen.dll` dalla radice del
  repository la content root e la radice, quindi `appsettings.json` non viene
  letto (niente seed di identity) e i moduli vengono cercati nella radice. Serve
  content root sulla cartella di output, oppure avviare dalla cartella di output.
- Skill attive: `phase-development-project-conventions`, `base-be-base-rules`,
  `implementation-be-dotnet-dev`, `capabilities-dev-problem-solving-methodology`,
  `run-tests`.

## SP-05P Ricerca sparsa e ibrida su REST, gRPC e client

Stato: implementato. Manca la verifica end-to-end sui trasporti.

- Confini invariati: i messaggi esistenti sono stati estesi, non sostituiti. Una
  nuova RPC avrebbe duplicato `SearchVector` e `SearchDocument` per una
  differenza di soli campi opzionali.
- `SearchVectorRequest.SparseQuery` (7) e `SearchTuning.MinDenseScore` (4) /
  `MinSparseScore` (5) portano la meta sparsa e le soglie; `SearchDocumentRequest`
  riceve `SparseQuery` (7) perche il server calcola l'embedding del testo e la
  meta sparsa arriva dal client. `SearchVectorResult` espone `DenseScore` (4) e
  `SparseScore` (5) come campi opzionali, cosi un risultato denso non dichiara
  uno score sparso inesistente.
- REST: `SearchData` accetta `sparseQuery`, `minDenseScore` e `minSparseScore`;
  `SearchCollection` non rifiuta piu una richiesta senza sentence ed embeddings
  se c'e `sparseQuery`.
- Handler: `SearchVector` usa `Store.SearchHybrid` quando `SparseQuery` e
  presente, altrimenti il percorso denso di sempre.
- Client .NET: `SearchSparse`, `SearchSparseAsync`, `SearchHybrid`,
  `SearchHybridAsync`, `SearchOptions.MinDenseScore`/`MinSparseScore`,
  `VectorSearchResult.DenseScore`/`SparseScore`. Il costruttore della richiesta
  accetta embedding assente, sparso assente o entrambi.
- Client Python: binding rigenerati dal proto aggiornato; `search_dense` (con
  `search` mantenuto come alias), `search_sparse`, `search_hybrid`,
  `SearchOptions.min_dense_score`/`min_sparse_score`, `SearchResult.dense_score`/
  `sparse_score`. I test del client passano da 10 a 17.
- Verifica fatta: i due proto sono identici, i progetti `Jigen.Grpc`,
  `Jigen.API`, `Jigen.Handlers`, `Jigen.Client` compilano, `pytest` 17 su 17,
  `ruff` pulito.
- Verifica non fatta: giro end-to-end su un server vivo. Il server parte, ma
  l'identity non crea utente e client seminati, quindi ogni chiamata REST
  risponde 401; `Jigen.API.Tests` e `Jigen.Grpc.Tests` non compilano gia prima
  di questo incremento.
- Limite noto e non risolto: nessun trasporto sa scrivere coppie sparse. Le
  `VectorEntry.SparseVectors` arrivano solo da chiamate in-process, quindi via
  REST/gRPC e possibile interrogare l'indice sparso ma non popolarlo.
- Skill attive: `phase-development-project-conventions`, `base-be-base-rules`,
  `implementation-be-dotnet-dev`, `capabilities-dev-problem-solving-methodology`,
  `run-tests`.

## SP-05O Soglie per meta nella ricerca ibrida

Stato: completato. Attende approvazione esplicita del codice.

- `VectorCollection.Search` accetta `minDenseScore` e `minSparseScore`, entrambi
  `-infinity` per impostazione predefinita. Le soglie si applicano **prima**
  della fusione RRF, quindi un punteggio debole in una meta non puo sollevare un
  documento nell'altra.
- I gradi della fusione sono ricalcolati dopo il taglio: eliminare i punteggi
  deboli non lascia buchi nella numerazione.
- `minSparseScore` viene passato al motore sparso e ne sfrutta la potatura
  anticipata; `minDenseScore` e applicato ai risultati gia restituiti
  dall'indice HNSW.
- Semantica verificata: un documento sopravvive se passa il taglio in **almeno
  una** meta. Un documento debole nel denso ma normale nello sparso arriva
  comunque dallo sparso, e viceversa; sparisce solo quando entrambe le soglie lo
  escludono. Una soglia sparsa impossibile lascia esattamente la meta densa.
- Verifica: quattro documenti con similarita dense distinte (1,00 / 0,90 / 0,30 /
  0,20) e pesi sparsi (1,0 / 5,0 / 2,0 / 0,5). Nove controlli superati, incluse
  le soglie combinate con `k` e con il predicato.
- Regressioni: Store 70 su 70, sparse 127 su 127.
- Skill attive: `phase-development-project-conventions`, `base-be-base-rules`,
  `implementation-be-dotnet-dev`, `capabilities-dev-problem-solving-methodology`,
  `run-tests`.

## SP-05N Punteggio minimo nella ricerca sparsa

Stato: completato. Attende approvazione esplicita del codice.

- Nuovo parametro `minimumScore` (default `-infinity`) su
  `VectorCollection.SearchSparse(query, top, predicate, minimumScore, token)`,
  `Store.SearchSparse(collection, query, k, filter, minimumScore, token)`,
  `SparseCollectionIndex.Search(...)` e `SparseIndex.Search(...)`.
- Doppio effetto, come previsto: scarta i documenti sotto la soglia e **innalza
  la soglia di potatura prima che il top-k sia completo**, quindi salta piu
  intervalli di blocco. La soglia dell'heap ora vale
  `max(punteggio k-esimo, minimumScore)`.
- Ordine dei controlli per candidato: prima il punteggio minimo (confronto
  aritmetico), poi la competitivita rispetto all'heap, solo alla fine il filtro
  che legge il contenuto.
- Verifica su 30.000 documenti, 6 termini di query, taglio al 2% dei punteggi:
- senza minimo 6.398 risultati in 9,80 ms per query; con minimo 0,960 si
  ottengono 132 risultati in 4,75 ms. Il sottoinsieme conserva l'ordine
  originale; combinato con k=100 restituisce al massimo 100 risultati, tutti
  sopra la soglia. Quattro controlli superati.
- Regressioni: sparse 127 su 127, Store 70 su 70.
- Non applicato alla ricerca ibrida: la fusione RRF lavora su posizioni, quindi
  un punteggio minimo su una sola meta non avrebbe un significato definito.
- Skill attive: `phase-development-project-conventions`, `base-be-base-rules`,
  `implementation-be-dotnet-dev`, `capabilities-dev-problem-solving-methodology`,
  `run-tests`.

## SP-05M Ipotesi: filtro per chiavi ammesse

Stato: misurato, non implementato. Serve una decisione dell'utente.

- Idea: invece di leggere il contenuto di ogni candidato e valutare un
  predicato, l'applicazione fornisce l'insieme delle chiavi ammesse, che conosce
  gia dal proprio indice; il motore fa solo un test di appartenenza, senza I/O.
- Misura su 2000 documenti, 40 termini, 6 termini per documento, predicato che
  tiene 200 documenti (10%), k=10, 30 esecuzioni per variante:
  ricerca senza filtro 1,56-1,78 ms; chiavi ammesse 1,95-2,01 ms;
  predicato con lettura del contenuto 3,37-3,65 ms.
  Il filtro per chiavi costa circa 0,2-0,4 ms, quello per predicato circa
  1,6-1,9 ms: la ricerca filtrata per chiavi e circa la meta del tempo.
- Il test di appartenenza e una ricerca binaria su chiavi ordinate confrontate
  come span, quindi senza allocazioni per candidato; la variante con
  `ToArray()` per chiamata costa uguale solo perche il caso e piccolo.
- Costo a carico dell'applicazione: una query per ottenere le chiavi. Utile
  quando il filtro e un criterio indicizzato altrove; inutile quando il criterio
  richiede comunque di leggere il documento.
- Verifica: entrambe le vie restituiscono gli stessi 10 documenti.

## SP-05L Filtro con predicato sulla ricerca sparsa

Stato: completato. Attende approvazione esplicita del codice.

- `VectorCollection.SearchSparse(query, top, predicate, token)` accetta ora un
  `Expression<Func<T, bool>>`, come la ricerca densa.
- Catena: l'espressione diventa `IFilterExpression` con `ExpressionTranslator`;
  `Store.SearchSparse(collection, query, k, IFilterExpression, token)` la applica
  dentro il filtro di visibilita del motore, quindi un documento rifiutato non
  occupa un posto nel top-k. La decisione legge il contenuto serializzato del
  candidato e lo valuta senza deserializzarlo.
- `SparseCollectionIndex.Search` ha un overload con `SparseDocumentFilter`; il
  filtro si combina con quello dei tombstone, quindi le copie ritirate restano
  escluse.
- La ricerca ibrida ora applica il predicato a entrambe le meta, non solo a
  quella densa.
- Costo e ottimizzazione: il filtro viene valutato solo per un documento che
  puo ancora entrare nel top-k (soglia corrente) e la decisione e memorizzata per
  l'ultimo documento, perche i posting di un documento arrivano consecutivi.
  Un documento sotto soglia non puo vincere e il suo punteggio puo solo crescere,
  quindi viene rivalutato quando un vettore successivo lo alza.
- Misura, corpus di 400 documenti con 20 forti e il resto debolissimi, predicato
  che tiene il 10%: valutazioni del filtro 30 a k=1, 100 a k=3, 228 a k=10,
  229 a k>=50 (tutti i candidati). Il risparmio cresce al calare di k: con
  k=10 servono comunque quasi tutti i candidati, perche occorre trovarne 10 che
  superino il predicato. La potatura MaxScore aveva gia ridotto i candidati a
  229 su 400.
- Verifica: quattro documenti in due categorie. Senza predicato quattro
  risultati; con il predicato solo i due della categoria attesa; un documento
  escluso che superava il secondo classificato non ruba il posto (top=2
  restituisce comunque due documenti buoni); predicato senza corrispondenze
  restituisce lista vuota; la ibrida rispetta il predicato; la densa mantiene il
  proprio comportamento. Sei controlli superati.
- Regressioni: Store 70 su 70, sparse 127 su 127.
- Skill attive: `phase-development-project-conventions`, `base-be-base-rules`,
  `implementation-be-dotnet-dev`, `capabilities-dev-problem-solving-methodology`,
  `run-tests`.

## SP-05K Ricerca densa, sparsa e ibrida

Stato: completato. Attende approvazione esplicita del codice.

- `VectorCollection.SearchDense(float[] embeddings, ...)` e
  `SearchDense(string sentence, ...)`: ricerca solo densa. Prima si chiamavano
  `Search`; il nome ora dice cosa cercano.
- `VectorCollection.SearchSparse(query, top, token)`: invariata, solo sparsa.
- `VectorCollection.Search(embeddings, sparseQuery, top, predicate, efSearch,
  token)`: ibrida, entrambe le query opzionali, ma fornirle entrambe nulle e un
  errore di argomento.
- Fusione RRF scelta dall'utente: ogni documento vale 1/(60 + posizione) in
  ciascuna lista e i valori si sommano; nessuna normalizzazione e nessun peso da
  tarare. `VectorSearchResult.Score` porta il valore fuso, mentre `DenseScore` e
  `SparseScore` restano disponibili come punteggi parziali.
- `SearchDense` popola anche `DenseScore`, per coerenza del tipo di risultato.
- Il predicato filtra solo la meta densa: viene valutato sul contenuto
  serializzato senza leggerlo, cosa che la meta sparsa non puo fare.
- Verifica: tre documenti in entrambe le liste piu uno solo denso e uno solo
  sparso. `SearchDense` restituisce solo punteggi densi e ordina per similarita;
  `SearchSparse` invariata; la ricerca ibrida riporta i due punteggi parziali
  secondo la provenienza, il punteggio fuso coincide con la somma dei ranghi
  reciproci, funziona con una sola delle due query e rifiuta l'assenza di
  entrambe. Dieci controlli superati.
- Regressioni: Store 70 su 70, sparse 127 su 127. Aggiornati i soli test che
  chiamavano la vecchia `Search` densa (`VectorCollectionTests`).
- Ordine delle coppie in ingresso non piu richiesto: `SparseIndexBuilder.Add`
  ordina una copia di ogni vettore e rifiuta solo un termine ripetuto, perche due
  pesi per lo stesso termine non hanno significato. Prima un vettore non ordinato
  produceva un errore solo al salvataggio, lontano dalla chiamata che lo causava.
- Allineamento completato su richiesta dell'utente: l'estensione
  `Store.Search(collection, queryVector, ...)` e diventata `Store.SearchDense`
  in entrambi gli overload con vettore, e l'overload senza vettore, che elenca
  per filtro, e diventato `Store.SearchByFilter`. Aggiornati handler del server,
  `DocumentCollection`, `VectorCollection` e i test che li usavano.
- Skill attive: `phase-development-project-conventions`, `base-be-base-rules`,
  `implementation-be-dotnet-dev`, `capabilities-dev-problem-solving-methodology`,
  `run-tests`.

## SP-05J WAL e indice sparso

Stato: completato. Attende approvazione esplicita del codice.

- Conferma: le coppie termine/peso fanno parte del record WAL di inserimento e
  il replay le ricostruisce, perche gli insert replicati passano dallo stesso
  thread di scrittura che indicizza il lotto.
- Lacuna trovata: la cancellazione non era nel percorso di recupero. `ApplyWalDelete`
  e `ApplyWalClearCollection` rimuovevano la voce densa ma non ritiravano la
  copia sparsa, quindi dopo un crash un documento cancellato ricompariva nella
  ricerca sparsa. Ora entrambi chiamano `DeleteSparseEntry`.
- Seconda lacuna, emersa dalla prova: il replay riapplica anche gli insert
  precedenti alla cancellazione, e il documento tornava comunque. Il tombstone
  registrato durante il replay copriva solo i segmenti gia su disco, non quello
  che il lotto replicato avrebbe scritto dopo. Ora il replay registra l'ultima
  operazione per chiave: un insert replicato di una chiave cancellata piu avanti
  nel WAL non viene indicizzato.
- Terza correzione: un insert replicato puo esistere gia in un segmento scritto
  prima del crash senza essere nel log delle posizioni; viene quindi ritirato
  prima di essere reindicizzato, cosi non compaiono due copie.
- Verifica con processo separato che termina senza chiusura: inserimento di 1 e
  2 con salvataggio, inserimento di 3 solo nel WAL, cancellazione di 1, poi
  `Environment.Exit`. Alla riapertura: documento 2 presente, documento 3
  recuperato dal WAL, documento 1 resta cancellato, nessun duplicato, Shrink
  finale coerente. Cinque controlli superati.
- Regressioni: Store 70 su 70, sparse 127 su 127.
- Skill attive: `phase-development-project-conventions`, `base-be-base-rules`,
  `implementation-be-dotnet-dev`, `capabilities-dev-problem-solving-methodology`,
  `run-tests`.

## SP-05I Igiene dei test dello Store

Stato: completato. Solo test, nessuna modifica al codice di produzione.

- Causa 1, lock condiviso: `ReadWriteTest` apriva `/data/jigendb` con il nome
  fisso `readwritetest` e lo Store prende un lock esclusivo per database. Con le
  classi in parallelo la seconda apertura falliva con "already open in another
  Store instance or process".
- Causa 2, dominio ONNX: le classi che caricano un modello registrano
  `ai.onnx.contrib`, che il runtime accetta una sola volta per processo; due
  inizializzazioni sovrapposte facevano fallire una delle due.
- Correzioni: `ReadWriteTest` usa una directory unica sotto `.temp` per test,
  con pulizia in `Dispose`; le tre classi che caricano un modello
  (`ReadWriteTest`, `NomicHnswScoreTests`, `BgeM3HnswScoreTests`) condividono la
  collezione xUnit `onnx-models` con parallelizzazione disattivata
  (`OnnxModelCollection`).
- Verifica: quattro esecuzioni consecutive della suite Store, tutte 70 su 70.
  Prima della correzione la prima esecuzione falliva 6 casi su 70 e la
  successiva passava, senza modifiche al codice.
- Nessuna modifica al codice di produzione; suite sparse 127 su 127 e primitive
  14 su 14 invariate.
- Skill attive: `phase-development-project-conventions`, `run-tests`,
  `capabilities-dev-problem-solving-methodology`.

## SP-05H Collegamento allo Store

Stato: code-ready. Attende approvazione esplicita del codice.

- `Store` gestisce gli indici sparsi in `{database}.sparse/<collezione>/`, con
  uno scrittore e un lettore per collezione. Il thread di scrittura indicizza il
  lotto appena persistito, quindi la regola di un solo scrittore resta valida.
- `Jigen.Store` ora riferisce `Jigen.Index.Sparse`.
- Sovrascrittura: `IndexSparseEntries` ritira le copie precedenti con un
  tombstone quando la chiave esisteva gia o compare due volte nel lotto. Senza
  questo il documento veniva servito due volte, una con i pesi vecchi: la
  verifica lo ha mostrato come 175 risultati invece di 174.
- Visibilita dei tombstone: scrittore e lettore aprono lo stesso file con handle
  diversi, quindi i record non scaricati non erano visibili al lettore e la
  cancellazione restava intermittente. Lo scrittore ora espone la lista viva e
  il lettore la usa quando esiste.
- Cancellazione e svuotamento ritirano le copie sparse. Lo Shrink dello Store
  compatta i segmenti di ogni collezione con piu di un segmento, come richiesto
  dall'utente.
- API: `Store.SearchSparse(collection, query, k, token)` e
  `VectorCollection.SearchSparse(query, top, token)`.
- Verifica: 300 documenti con 1-4 termini per vettore, 18 ricerche attraverso lo
  Store concordi con l'oracolo; aggiornamento del documento 10 servito con i
  pesi nuovi; cancellazione del 20 assente; ricerca corretta dopo riapertura;
  Shrink che lascia un solo segmento e risultati ancora concordi.
  Sei controlli superati.
- Regressioni: Store 70 su 70, primitive 14 su 14, sparse 127 su 127.
  La suite Store resta instabile per cause preesistenti (lock condiviso su
  /data/jigendb e dominio ONNX): la prima esecuzione ha fallito i sei casi di
  ReadWriteTest, la seconda e passata.
- Non incluso: REST, gRPC, client; compattazione fuori dallo Shrink.
- Skill attive: `phase-development-project-conventions`, `base-be-base-rules`,
  `implementation-be-dotnet-dev`, `capabilities-dev-problem-solving-methodology`,
  `run-tests`.

## SP-05G Compattazione dei segmenti

Stato: code-ready. Attende approvazione esplicita del codice.

- `SparseCollectionCompactor.Compact()` fonde tutti i segmenti vivi in uno solo
  con la generazione successiva, scarta le copie ritirate dai tombstone,
  riscrive il manifest con la sola nuova generazione, azzera i tombstone e
  rimuove le directory dei vecchi segmenti.
- Memoria: fusione termine per termine con un cursore per segmento; il costo in
  memoria e la lista piu lunga, non il corpus. Il merge emette blocchi di
  `BlockSize` posting senza accumulare l'intero termine.
- Sicurezza: il segmento nuovo e scritto e svuotato su disco prima che il
  manifest lo nomini; il manifest e sostituito con una rinomina; le directory
  non elencate vengono ignorate dai lettori e rimosse alla compattazione
  successiva. Un'interruzione lascia quindi uno stato valido.
- Errore corretto durante la verifica: i cursori non venivano avvanzati di un
  posting prima della fusione, quindi il segmento risultante aveva zero termini.
  La verifica lo ha colto come "expected 1, actual 0".
- Verifica: cinque lotti da 60 documenti, aggiornamento di 5, 100 e 250,
  cancellazione di 30, 200 e 299. Prima: 8 segmenti e 6 tombstone, 32 ricerche
  concordi con l'oracolo. Dopo la compattazione: segmento g9 unico, manifest con
  una sola voce, una directory, 15 termini, zero tombstone, 32 ricerche ancora
  concordi, cancellati assenti e aggiornati con i pesi nuovi; una seconda
  compattazione non fa nulla; un nuovo segmento dopo la fusione resta
  ricercabile. Dieci controlli superati. Suite sparse 127 su 127.
- Non incluso: politica automatica (soglia per numero di segmenti o rapporto di
  copie ritirate), collegamento allo Store.
- Skill attive: `phase-development-project-conventions`, `base-be-base-rules`,
  `implementation-be-dotnet-dev`, `capabilities-dev-problem-solving-methodology`,
  `run-tests`.

## SP-05F Scrittore per lotti e persistenza della collezione

Stato: code-ready. Attende approvazione esplicita del codice.

- `SparseCollectionWriter` accumula i documenti e `Commit` scrive un segmento
  nuovo con la generazione successiva, poi lo pubblica nel manifest. L'ordine di
  arrivo non conta piu: conta solo l'ordine interno al lotto, che il builder
  stabilisce in memoria.
- `Delete` registra un tombstone che copre i segmenti scritti fino a quel
  momento; una versione aggiunta dopo resta visibile. Se ci sono documenti in
  attesa li scrive prima, cosi il tombstone non nasconde un documento appena
  accodato. Il prezzo: un aggiornamento produce un segmento in piu.
- Tombstone generalizzato: `UpToGeneration` copre ogni copia scritta fino a
  quella generazione. Lo scrittore non deve sapere in quale segmento si trova la
  chiave, quindi non serve una mappa chiave-segmento all'apertura.
- Manifest: elenco delle generazioni, scritto su file temporaneo e sostituito
  con una rinomina, quindi un lettore non vede mai un elenco parziale.
- Struttura per collezione: `manifest`, `tombstones` con il suo `.index`,
  `segments/g<generazione>/{terms,terms.index,blocks,blocks.index}`.
  Quattro file per segmento piu due per collezione.
- `SparseCollectionStorage.Open` apre i segmenti elencati dal manifest e i
  tombstone, e restituisce `SparseCollectionIndex`. Una directory di segmento
  non elencata viene ignorata, quindi un segmento incompleto non entra mai.
- Verifica: 2 lotti da 150 documenti con arrivo casuale, aggiornamento dei
  documenti 20 e 170, cancellazione del 45. Quattro segmenti scritti, manifest
  coerente, file presenti. Dopo la riapertura: 32 ricerche a k 1, 5, 50 e 500
  concordi con l'oracolo; cancellato assente; aggiornati con i pesi nuovi;
  scrittore riaperto che continua dalla generazione 4; quinto segmento visibile
  e partecipe della ricerca fusa. Nove controlli superati.
  Suite sparse 127 su 127.
- Non incluso: compattazione dei segmenti, collegamento allo Store.
- Skill attive: `phase-development-project-conventions`, `base-be-base-rules`,
  `implementation-be-dotnet-dev`, `capabilities-dev-problem-solving-methodology`,
  `run-tests`.

## SP-05E Indice a segmenti con tombstone

Stato: code-ready. Attende approvazione esplicita del codice.

- Requisito dell'utente: le scritture possono arrivare in ordine arbitrario e un
  documento puo essere aggiornato dopo la scrittura. Un unico indice ordinato per
  termine lo impediva.
- Struttura: un segmento e un `SparseIndex` immutabile con una generazione.
  `SparseCollectionIndex` interroga ogni segmento e unisce i risultati per
  punteggio decrescente e chiave crescente. `SparseTombstone` ritira una singola
  copia indicando la generazione del segmento che la contiene: un aggiornamento
  nasconde la versione precedente e lascia viva la nuova.
- Motore: filtro di visibilita applicato dentro il top-k. Un documento ritirato
  non occupa un posto, altrimenti i risultati si accorcerebbero.
- Esattezza: un documento vive in un solo segmento vivo, quindi i migliori k
  della collezione sono tra i migliori k di ogni segmento; la fusione non perde
  candidati.
- Errore corretto durante la verifica: la prima versione del tombstone indicava
  la generazione dell'operazione e nascondeva anche la versione nuova dello
  stesso documento. Ora indica la copia da ritirare.
- Verifica: tre segmenti da 200 documenti con aggiornamenti dei documenti 50 e
  120 e cancellazione del 300, arrivo non ordinato nei lotti.
  32 ricerche fuse a k 1, 5, 50 e 500 concordi con l'oracolo su chiavi e
  punteggi; cancellato assente; aggiornato presente solo con i pesi nuovi;
  copia ritirata sostituita dal documento successivo; 237 documenti vivi
  restituiti oltre il k. Cinque controlli superati. Suite sparse 127 su 127.
- SP-05D e superato: non serve piu che le chiavi crescano.
- Non incluso: scrittore per lotti che produce un segmento per ciascun lotto,
  manifest su disco, compattazione dei segmenti.
- Skill attive: `phase-development-project-conventions`, `base-be-base-rules`,
  `implementation-be-dotnet-dev`, `capabilities-dev-problem-solving-methodology`,
  `run-tests`.

## SP-05D Indicizzare durante l'invio

Stato: code-ready. Attende approvazione esplicita del codice.

- Requisito dell'utente: la ricerca non puo restare ferma fino alla fine
  dell'invio.
- Implementazione: `SparseIndexWriter` riceve i documenti man mano. I posting
  restano in memoria finche non si svuota il buffer; `Flush` ordina le coppie
  del termine, riempie l'ultimo blocco aperto, crea i blocchi successivi e
  riscrive la voce di directory del termine. `PendingLimit` forza il flush da
  solo. Il chiamante puo ricercare sui documenti gia scritti.
- Regola di ordinamento: la chiave del documento deve crescere, perche i posting
  di un termine sono ordinati per chiave. Le chiavi generate dallo Store
  (`Guid.CreateVersion7`) crescono nel tempo, quindi il percorso normale resta
  incrementale. Una chiave che non avanza produce `NotSupportedException` con
  l'indicazione di ricostruire: meglio un errore esplicito che un ordine rotto.
- Verifica: 500 documenti, 1-3 vettori, 20 termini, blocchi da 9 e limite di 40
  posting in attesa. Sei ricerche eseguite durante l'invio, 180 confronti con
  l'oracolo esaustivo, tutti concordi su chiavi e punteggi; classifica finale
  concorde; ogni documento corrispondente raggiungibile dopo molti flush;
  contenuto conservato alla riapertura; chiave fuori ordine rifiutata.
  Cinque controlli superati. Suite sparse: 127 su 127.
- Limiti dichiarati: una chiave fuori ordine richiede la ricostruzione; non
  esistono ancora tombstone per cancellazioni e aggiornamenti; ogni flush
  riscrive l'intera voce di directory dei termini toccati, costo che cresce con
  il numero di blocchi del termine; l'`SparseIndex` va ricreato dopo un flush
  per vedere i termini nuovi.
- Skill attive: `phase-development-project-conventions`, `base-be-base-rules`,
  `implementation-be-dotnet-dev`, `capabilities-dev-problem-solving-methodology`,
  `run-tests`.

## SP-05C Costruire l'indice di una collezione

Stato: code-ready. Attende approvazione esplicita del codice.

- Vincolo del formato: `SparsePostingBlock` esige posting strettamente ordinati
  per chiave e indice vettore e `SparseBlockReference.Follows` esige intervalli
  ordinati. Le scritture arrivano in ordine arbitrario, quindi un indice non si
  puo accodare man mano: si costruisce in blocco. Aggiornamento e cancellazione
  richiedono un nuovo indice, non una modifica in loco.
- Implementazione: `SparseDocument`, `SparseIndexBuildSummary` (termini,
  posting, blocchi) e `SparseIndexBuilder` con `Add` e `Write`. `Write` crea la
  directory, rifiuta un indice gia presente e scrive directory e blocchi in
  ordine di termine. Ogni termine e ordinato per chiave e indice vettore;
  identita ripetute sono un errore.
- Sicurezza dell'operazione: la scrittura avviene in una directory nuova, cosi
  un indice in uso non viene mai toccato e la sostituzione resta al chiamante.
- Verifica: corpus generato di 400 documenti con 1-3 vettori, 24 termini,
  blocchi da 7 posting e ordine di arrivo mescolato. Costruzione:
  24 termini, 2.320 posting, 340 blocchi. 36 confronti a k 1, 5 e 20 con la
  ricerca esaustiva, tutti concordi; riapertura con lo stesso ranking;
  rifiuto di sovrascrittura e di identita ripetuta. 5 controlli superati.
  Suite sparse: 127 su 127.
- Limite dichiarato: il builder tiene in memoria tutti i posting del corpus.
  Con milioni di coppie servono partizioni per intervalli di termini, ancora
  da implementare e da misurare.
- Skill attive: `phase-development-project-conventions`, `base-be-base-rules`,
  `implementation-be-dotnet-dev`, `capabilities-dev-problem-solving-methodology`,
  `run-tests`.

## SP-05B Persistere le coppie nel record del documento

Stato: code-ready. Attende approvazione esplicita del codice.

- Formato: dal numero di versione 2, il file dei vettori chiude ogni record con
  la sezione sparsa `[numero vettori][conteggio coppie per vettore][coppie
  termine/peso]`. Il numero di versione occupa lo slot riservato
  dell'intestazione (offset 8), che negli archivi precedenti vale zero.
- Scelta di layout: la sezione sta nello stesso record dei vettori densi, cosi
  la coppia resta una sola unita di scrittura e di lettura e non serve un
  secondo file, un secondo indice delle posizioni o una nuova mappa in memoria.
- Archivi esistenti: versione zero significa record senza sezione sparsa.
  Si aprono e restano densi; una scrittura con vettori sparsi viene rifiutata
  con `NotSupportedException` invece di perdere dati in silenzio. Non esiste
  conversione in loco: i record vecchi non hanno spazio per la sezione.
- Percorsi collegati: scrittura batch e record singolo, WAL (sezione scritta
  solo quando ci sono coppie, quindi i record densi restano identici),
  replay, compattazione con copia dell'intera sezione, validazione del log
  delle posizioni che ora ammette un record con soli vettori sparsi,
  `Store.GetSparseVectors`, `VectorCollection.GetSparseVectors` e passaggio
  delle coppie da `VectorEntry<T>` allo Store.
- Verifica funzionale, 15 controlli superati: round trip multi-vettore con
  ordine dei termini conservato, voce solo densa con sezione vuota, voce solo
  sparsa senza vettori densi, sovrascrittura, cancellazione, persistenza dopo
  riapertura con replay del WAL, sopravvivenza alla compattazione, archivio
  legacy leggibile e scrittura sparsa rifiutata.
- Regressioni: Store 70 su 70, primitive 14 su 14, sparse 127 su 127.
  La suite Store resta instabile per cause preesistenti: `ReadWriteTest`
  condividono `/data/jigendb` e il registro del dominio ONNX
  `ai.onnx.contrib`; in due esecuzioni consecutive la prima ha fallito i sei
  casi di `ReadWriteTest` per lock e dominio duplicato, la seconda e passata
  70 su 70, e in isolamento i sei casi passano.
  I test API non compilano per l'errore preesistente del costruttore di
  `EmbeddingController`; non dipende da questa modifica.
- Nota di lavorazione: una invocazione di `dotnet format` senza `.editorconfig`
  nel repository ha raddoppiato l'indentazione di `WalRecord.cs`. Il file e
  stato ricostruito con l'indentazione originale e le sole modifiche
  semantiche: il diff finale e di 82 righe aggiunte e 6 rimosse, identiche
  con e senza confronto degli spazi.
- Limiti dichiarati: i byte morti dei record sparsi non entrano in
  `DeadBytes` ne in `FragmentationRatio`, quindi la compattazione non viene
  richiesta per soli dati sparsi; `CollectionInfo.VectorSize` non conta le
  coppie. Mancano il comando di ricostruzione dell'indice dai documenti e
  l'indice per termine.
- Skill attive: `phase-development-project-conventions`,
  `phase-development-configuration-options`, `base-be-base-rules`,
  `implementation-be-dotnet-dev`, `capabilities-dev-problem-solving-methodology`,
  `run-tests`.

## SP-05A Modello sparso nel documento

Stato: code-ready. Attende approvazione esplicita del codice.

- Decisione dell'utente del 24 ottobre 2026: le coppie termine/peso stanno
  **nel documento**, unica fonte autorevole; le liste per termine sono un
  indice derivato ricostruibile. Le coppie costano circa 472 MB per milione
  di documenti con 64 termini e chiavi di 4 byte, in aggiunta all'indice
  (circa 1,06 GB). Il recupero da indice corrotto e disco -> disco e in
  streaming, a partizioni di termini; l'apertura non ricostruisce nulla.
- Forma scelta dopo misura: coppie ordinate. `SparseTerm` e un
  `readonly struct` con `[MessagePackObject]` e chiavi numeriche, che occupa
  gli stessi byte di una tupla (227/472/1.027 byte per 32/64/128 termini),
  mentre senza attributi verrebbe salvato come mappa (611/1.240/2.562 byte).
  Rispetto al dizionario le coppie costano il 7-15% in piu ma si scorrono
  3-6 volte piu veloci (206 ns contro 1.028 ns a 64 termini).
- Implementazione: `SparseTerm` in `Jigen.Primitives/DataStructures` e campo
  `SparseVectors` su `VectorEntry` e `VectorEntry<T>`, con il vettore i
  corrispondente a `Embeddings[i]`.
- Verifica: round trip sparso, ordine conservato, densi invariati, documento
  scritto senza il campo ancora leggibile, voce tipizzata, valore predefinito
  vuoto: 6 controlli superati. `PrimitiveTests` 14 superati; suite sparse 127
  superati; `Jigen.Store` compilato; test Store su serializzazione, ricerca,
  cancellazione e filtri: 27 superati.
- La suite Store completa riporta 8-9 fallimenti preesistenti in
  `IngestionTests`, `ReadWriteTest`, `NomicHnswScoreTests` e
  `SeedDemoDatabaseTest`: il costruttore di `IngestionTests` apre
  `/data/jigendb` e non dispone lo `Store`, quindi i casi successivi trovano il
  lock `tests.lock.jigen` ancora aperto nel processo; `SeedDemoDatabaseTest`
  richiede un server attivo su `localhost:13223`; `ReadWriteTest` e
  `NomicHnswScoreTests` falliscono a intermittenza per il registro del dominio
  ONNX `ai.onnx.contrib` e per lo stesso lock condiviso. Nessuno di questi
  percorsi tocca la serializzazione dei vettori sparsi.
- Su richiesta dell'utente del 24 ottobre 2026 sono stati rimossi i due file
  che fallivano in modo deterministico: `IngestionTests.cs` (7 casi, `Store`
  mai disposto) e `SeedDemoDatabaseTest.cs` (1 caso, richiede un server vivo).
  `ReadWriteTest` e `NomicHnswScoreTests` restano perche dispongono lo `Store`
  e passano quando non collidono con il lock condiviso. Dopo la rimozione la
  suite Store riporta 70 superati, 0 falliti.
- Non ancora collegati a questo campo: WAL, record dello storage,
  `VectorCollection.Set` e indici. Sono il passo successivo, non questo.
- Skill attive: `phase-development-project-conventions`,
  `phase-development-configuration-options`, `base-be-base-rules`,
  `implementation-be-dotnet-dev`, `capabilities-dev-problem-solving-methodology`,
  `analyzing-dotnet-performance`, `run-tests`.

## SP-07C: top-k e ordinamento degli intervalli

Stato: annullato su richiesta dell'utente il 24 ottobre 2026 alle 10:46.
Ripristinato il codice precedente a SP-07C; conservati SP-07B e il benchmark
originale. Le misure sotto documentano il tentativo scartato, non il codice attuale.
Scope: SparseMaxScoreSearch, SparseSearchResult, SparseTermDirectoryEntry,
SparseIndex. Benchmark originale invariato.

- Il top-k scarta i documenti non competitivi prima di allocare il risultato;
  trasferisce al risultato il buffer posseduto del documento pendente senza
  ricopiarlo. Punteggi e pareggi conservano il confronto precedente.
- Ordinamento dei limiti una sola volta per intervallo effettivamente visitato.
  Le selezioni successive riutilizzano l'ordine; accumulo e arrotondamento invariati.
- Capacita iniziale dei due HashSet della directory e della lista dei termini.
- 127 test superati in Debug e in Release; formatter verificato.
  Nessun test modificato; warning NU1902 preesistente.
- Baseline prima delle modifiche, protocollo originale: k=1 8,35 ms,
  k=10 8,70 ms, k=100 9,19 ms, k=max 9,91 ms; 1.265,3 KiB/query top-10.
- Dopo: k=1 8,81 ms, k=10 9,14 ms, k=100 9,51 ms, k=max 10,77 ms.
  Ripetizione senza modifiche: 8,70 / 9,05 / 9,39 / 10,49 ms.
  Allocazioni top-10 in entrambe le esecuzioni: 1.079,5 KiB/query (-14,7%).
- Latenza top-10 osservata peggiorata del 4-5% sulla baseline immediata.
  Il vecchio 7,76 ms resta riportato come dato storico, non sostituito dalla
  nuova baseline. Non si attribuisce il peggioramento con certezza al codice:
  sono esecuzioni sequenziali in ambiente condiviso, non un esperimento isolato.
  Le modifiche restano da valutare prima di dichiarare un miglioramento prestazionale.
- Skill attive: capabilities-dev-problem-solving-methodology,
  analyzing-dotnet-performance, base-be-base-rules, implementation-be-dotnet-dev,
  phase-development-project-conventions, run-tests. Capacita usate per analisi
  delle allocazioni e confronto misurato, senza modificare il protocollo.

## SP-07B Sparse: ulteriori riduzioni di copie e allocazioni

Stato: code-ready. Ottimizzazioni richieste dall'utente il 24 ottobre 2026.
Le proprieta pubbliche `Postings` e `Blocks` restano invariate per sua
indicazione esplicita; nessuna modifica al formato persistente o allo Store.

- Scope: `SparsePostingBlock`, `SparseTermDirectoryEntry`,
  `SparseBlockReference`, `SparseMaxScoreSearch`.
- La deserializzazione calcola i limiti durante la lettura e usa costruttori
  privati per prendere possesso degli array gia verificati. Eliminati la
  seconda scansione dei posting e il secondo HashSet della directory.
  I costruttori pubblici mantengono copia degli input e controlli esistenti.
- Prima della copia di una chiave viene verificata anche la capacita residua
  del buffer condiviso; una lunghezza incompatibile causa InvalidDataException.
- MaxScore alloca un array di indici e un comparatore per query, riutilizzati
  a ogni cambio soglia. L'ordine a parita di limite resta quello dei termini;
  l'accumulo dei punteggi non cambia.
- Il confine di intervallo condivide la chiave posseduta dal riferimento
  del blocco, senza allocare o copiare un nuovo identificatore.
- Verifica: 127 test sparse superati sia in Release sia in Debug.
  Nessun test aggiunto o modificato. Rimane NU1902 per MessagePack 3.1.7.
- Nessuna nuova misura di latenza: la riduzione del lavoro e delle allocazioni
  deriva dal codice; il guadagno end-to-end resta da misurare.
- Skill attive: `phase-development-project-conventions`, `base-be-base-rules`,
  `implementation-be-dotnet-dev`, `capabilities-dev-problem-solving-methodology`,
  `run-tests`. La metodologia governa le modifiche e la verifica delle
  regressioni; `run-tests` governa l'esecuzione della suite esistente.

### SP-07B: misura delle prestazioni

Esecuzione Release del 24 ottobre 2026, .NET 10.0.13, Arch Linux, 22 CPU
logiche visibili. Harness ripetibile:
`.ai-generated-operations/sparse-performance/SparsePerformance.csproj`.
Comando dalla radice: `dotnet run --project
.ai-generated-operations/sparse-performance/SparsePerformance.csproj -c Release
-- "<percorso assoluto repository>"`.
I file dati vengono creati in una sottocartella univoca di `.temp` e rimossi
alla fine, dopo la chiusura delle liste.

- Corpus sintetico con seme 42: 3.000 documenti, due vettori per documento,
  200 termini, probabilita di presenza 0,3 e pesi positivi uniformi.
  359.825 posting, 5.723 blocchi da massimo 64 posting, 6.222.320 byte
  complessivi nei file dati e indice.
- Query con seme 73: 200 query da 20 termini distinti con pesi positivi.
  Warm-up di 50 query per k, poi cinque round per k, ordine dei k ruotato.
  Un solo thread; 1.000 campioni per k. Allocazioni gestite misurate sul
  thread corrente, non memoria residente. La raccolta dei tempi aggiunge
  un piccolo overhead di allocazione comune alle misure.
- k=1: media 9,631 ms, P50 9,192 ms, P95 13,440 ms,
  1.311,6 KiB allocati per query.
- k=10: media 9,514 ms, P50 9,180 ms, P95 13,068 ms,
  1.316,5 KiB allocati per query.
- k=100: media 10,417 ms, P50 9,945 ms, P95 14,284 ms,
  1.339,2 KiB allocati per query.
- k=int.MaxValue: media 11,549 ms, P50 11,133 ms, P95 15,354 ms,
  1.465,0 KiB allocati per query. Rimane lo stesso motore, non un
  riferimento brute-force indipendente.
- Generazione, scrittura, flush e chiusura: 507,01 ms. Riapertura delle
  StoredList: 2,43 ms; costruzione SparseIndex: 8,43 ms.
  Prima ricerca k=10: 110,26 ms, inclusi effetti di primo utilizzo/JIT.
  Cache del sistema operativo non svuotata: non e una misura a disco freddo.
- Le medie dei round k=10 variano da 8,922 a 9,803 ms. Il precedente
  valore di circa 8,2 ms non e un baseline direttamente confrontabile:
  corpus ricreato e protocollo diverso, ora media di tutti i round
  anziche migliore round. Non viene attribuito un guadagno percentuale.
- Nessun codice di produzione o test funzionale modificato da questa misura.
  Skill: `capabilities-dev-problem-solving-methodology`, per protocollo,
  separazione delle fasi e limiti di interpretazione.

### SP-07B: ripristino del protocollo originale

Su richiesta dell'utente e stato recuperato il sorgente originale del benchmark
dalla cronologia della sessione, non ricostruito per approssimazione.
L'harness precedente in `.ai-generated-operations/sparse-performance/Program.cs`
e stato sostituito con quel protocollo. Cambiano solo la gestione sicura dei
temporanei e la stampa aggiuntiva dei tre tempi, eseguita fuori dalle misure.

- Seme 12345; 3.000 documenti, da uno a tre vettori ciascuno, 60 termini per
  vettore su 200, chiavi BitConverter, blocchi fisicamente in ordine inverso.
  Confermati esattamente 359.820 posting e 5.727 blocchi da massimo 64.
- Le 200 query usano lo stesso generatore dopo la generazione del corpus:
  20 estrazioni ciascuna, con eventuale sostituzione dei termini ripetuti.
  Warm-up: tutte le query a k=10. Poi k=1,10,100,int.MaxValue, tre round
  per k, migliore media di round. Allocazioni tramite
  GC.GetTotalAllocatedBytes(true), su una successiva passata a k=10.
- Release, risultati ripristinati: k=1 7,49 ms (precedente 7,83);
  k=10 7,76 ms (precedente 8,17); k=100 8,29 ms (precedente 8,81);
  k=max 9,24 ms (precedente 9,58).
- Round top-10: 7,866 / 7,817 / 7,762 ms. Allocazioni top-10:
  1.265,3 KiB/query rispetto ai precedenti circa 1.712 KiB/query.
  Build con flush 507 ms; costruzione SparseIndex su liste gia aperte 1 ms.
- Con lo stesso protocollo il valore osservato top-10 e circa il 5% inferiore,
  con circa il 26% in meno di allocazioni. Il confronto e con una misura
  storica, non con esecuzioni A/B contemporanee: non isola la variabilita
  della macchina tra le sessioni.
- I 9,51 ms della prova con protocollo diverso non sono usati per valutare
  le ottimizzazioni. Questo harness originale resta il riferimento per
  le prossime misure. Dati temporanei rimossi a fine esecuzione.

## Actor task planning

Piano dei vettori sparsi confermato dall'utente il 23 ottobre 2026:
[Piano di sviluppo dei vettori sparsi](task-plan.md).
Un attore, questa sessione, con codice, integrazione e verifica in sequenza.
L'utente approva esplicitamente il codice di SP-01 e autorizza i test SP-02.
Lo stesso gate resta previsto per gli incrementi successivi.

## Technology resolution

SP-01 usa il backend-platform .NET 10, risolto con `implementation-be-dotnet-dev`,
e la baseline `base-be-base-rules`. I progetti `Jigen.Index.Sparse` e `Jigen.Primitives`
usano C# 14. Lo stack xUnit riguarda le regressioni esistenti.
Embedding, trasporti e database relazionali non sono coinvolti.
Si mantiene la struttura della libreria embedded, senza moduli CQRS.
Il nuovo progetto `Jigen.Index.Sparse` dipende da `Jigen.Primitives`;
nessun nuovo pacchetto NuGet.

## Configuration options

SP-01 non introduce configurazione. `ValueTuple` è il parametro senza opzioni
richiesto dal contratto generico `IStorableItem`; i file e il flush restano
responsabilità delle opzioni esistenti di `StoredList`.

## Task execution

### SP-03B Cursore dei posting

Stato: closed. Verifiche di compilazione e regressione superate;
codice approvato esplicitamente dall'utente prima del task SP-03BT.

- Contratto confermato dall'utente: cursore in avanti su directory del termine
  e StoredList dei blocchi della stessa collezione, senza gestione dei file.
- Implementato `SparsePostingCursor`: `MoveNext`, `Seek` inclusivo e
  `SkipBlock`. Quest'ultimo seleziona il riferimento successivo senza leggerlo.
  Prima dell'avanzamento e dopo il salto `Current` non ha valore; a fine lista
  anche `CurrentBlock` è assente e le operazioni restituiscono false.
- Seek cerca per chiave binaria e indice vettore, prima nei confini della
  directory, poi nei posting del solo blocco scelto. Non arretra.
- Il caricamento confronta conteggio, limiti ed estremi con il riferimento.
  Riferimenti fuori lista o discordanti causano `InvalidDataException`;
  gli errori I/O non sono intercettati. Un caricamento fallito non pubblica
  un posting corrente e può essere ritentato.
- Il chiamante garantisce durata e stabilità della StoredList. Nessuna
  configurazione, dipendenza o modifica al formato persistente.
- Verifica: build riuscita, formatter verificato, nessun errore nell'editor;
  tutti i 72 test esistenti superati. Questi test non esercitano ancora il cursore.
  Rimane il warning preesistente NU1902 per MessagePack 3.1.7.
- File: `src/Jigen/Jigen.Index.Sparse/SparsePostingCursor.cs`, piano e dossier.
  Nessun test modificato. Ricerca top-k, lookup e Store esclusi.
- Skill: `workflow-development`, `phase-development-technology-resolution`,
  `phase-development-project-conventions`, `phase-development-configuration-options`,
  `phase-development-task-execution`, `phase-development-development-handoff`,
  `base-be-base-rules`, `implementation-be-dotnet-dev`,
  `capabilities-dev-problem-solving-methodology`, `run-tests`.
  La metodologia serve alla verifica del seek e delle transizioni di stato;
  `run-tests` governa le regressioni esistenti. Stack e piano attori invariati.
- Handoff: approvazione esplicita del codice, poi SP-03BT Verificare il cursore.

### SP-01 Blocco persistente di posting

Stato: closed. Codice compilato e approvato esplicitamente dall'utente.

- `SparsePosting` conserva una copia della chiave binaria, l'indice del vettore
  sparso nel documento e un peso float finito. Non usa posizioni dello Store.
- `SparsePostingBlock` conserva posting immutabili, ordinati per chiave binaria
  lessicografica e indice del vettore. La stessa coppia non può comparire due volte.
- Minimo e massimo includono zero per i contributi assenti. Il formato ammette
  pesi negativi; l'algoritmo successivo dovrà usare limiti compatibili.
- Serializzazione little-endian, versione 1. Intestazione da 16 byte:
  versione int32, conteggio int32, minimo float32, massimo float32.
  Ogni posting contiene lunghezza chiave int32, byte della chiave, indice vettore
  int32 e peso float32. Dimensione: 16 + somma di (12 + lunghezza chiave).
- Blocco vuoto ammesso, con limiti zero. La lettura rifiuta versioni sconosciute,
  troncamenti, lunghezze invalide, pesi non finiti, indici negativi, duplicati,
  ordinamento errato, byte residui e limiti incoerenti tramite `InvalidDataException`.
- `StoredList` resta invariata. La lettura attuale deserializza tutto il blocco;
  la directory per saltare payload senza leggerli appartiene a SP-03.
- Verifica: build riuscita, formatter applicato ai due file nuovi, nessun errore
  nell'editor, 11 regressioni esistenti superate. Esclusi tre test che modificano
  percorsi fissi condivisi sotto `/data`. Directory temporanea confinata a `.temp/sp-01`.
- I test del nuovo tipo appartengono a SP-02, riportato sotto.
- Warning preesistenti: NU1902 per MessagePack 3.1.7 e suggerimenti xUnit2017.

File: `src/Jigen/Jigen.Index.Sparse/SparsePosting.cs`,
`src/Jigen/Jigen.Index.Sparse/SparsePostingBlock.cs`,
`src/Jigen/Jigen.Index.Sparse/Jigen.Index.Sparse.csproj`, `Jigen.sln`,
questo dossier, il piano e i documenti di scouting che registrano la scelta.

La correzione richiesta dall'utente separa il codice sparse dalle primitive
generiche. I tipi hanno namespace `Jigen.Index.Sparse`; il progetto è registrato
nella cartella di soluzione `src/Store`, come gli altri indici.
La build del nuovo progetto e della dipendenza riesce. Il ciclo di vita
per collezione appartiene agli incrementi SP-03 e SP-05, non al singolo blocco.

Skill attivate: `workflow-development`, `phase-development-actor-task-planning`,
`phase-development-technology-resolution`, `phase-development-project-conventions`,
`phase-development-configuration-options`, `phase-development-task-execution`,
`phase-development-deviation-routing`, `phase-development-development-handoff`,
`base-be-base-rules`, `implementation-be-dotnet-dev`, `base-project-decision-log`,
`capabilities-dev-problem-solving-methodology`, `capabilities-italian-text-humanization`,
`run-tests`. La metodologia serve alla verifica del formato e dell'immutabilità;
la revisione italiana riguarda i documenti di progetto; `run-tests` riguarda solo
l'esecuzione delle regressioni esistenti.

### SP-02 Verificare il blocco persistente

Stato: closed. L'approvazione esplicita del codice precede l'inizio dei test.

- Creato il progetto xUnit `tests/Jigen.Index.Sparse.Tests`, registrato nella
  cartella `tests` della soluzione, con riferimento a `Jigen.Index.Sparse`.
  Versioni di SDK e runner allineate ai test esistenti.
- Flush e riapertura di una vera `StoredList` preservano documenti, indici dei
  vettori, limiti e bit dei pesi, inclusi valori estremi, subnormali e zero negativo.
- Verificati blocco vuoto, contributo zero nei limiti, indipendenza dagli array
  del chiamante e dal buffer di deserializzazione, ordinamento e identità duplicate.
- Un payload atteso indipendente verifica il layout little-endian versione 1.
- Ogni prefisso troncato di un blocco popolato viene rifiutato.
  I casi di corruzione includono versione, conteggio, lunghezze, pesi, indici,
  limiti, duplicati, ordinamento e byte residui.
- Verifica: tutti i 35 test del progetto superati; formatter e controllo degli
  spazi superati; nessun errore nel file di test. Rimane solo il warning NU1902
  della dipendenza MessagePack già presente.
- I test di persistenza creano directory uniche sotto `.temp` e le eliminano
  dopo la chiusura dei file; nessun dato scritto sotto `/data`.
- File: `tests/Jigen.Index.Sparse.Tests/Jigen.Index.Sparse.Tests.csproj`,
  `tests/Jigen.Index.Sparse.Tests/SparsePostingBlockTests.cs`, `Jigen.sln`,
  piano e questo dossier. Nessuna modifica al codice di produzione né allo snapshot allegato.
- Skill: `workflow-development`, `phase-development-actor-task-planning`,
  `phase-development-technology-resolution`, `phase-development-project-conventions`,
  `phase-development-configuration-options`, `phase-development-task-execution`,
  `phase-development-deviation-routing`, `phase-development-development-handoff`,
  `base-be-base-rules`, `implementation-be-dotnet-dev`, `base-project-decision-log`,
  `capabilities-dev-problem-solving-methodology`, `capabilities-italian-text-humanization`,
  `run-tests`. La metodologia orienta i casi ai difetti del formato; la revisione
  italiana riguarda la documentazione; `run-tests` governa il runner VSTest/xUnit.

Handoff: SP-03A Directory persistente dei termini, primo incremento di SP-03.

### SP-03A Directory persistente dei termini

Stato: closed. Build e regressioni superate; codice approvato esplicitamente
dall'utente prima del task di test SP-03AT.

- Aggiunti `SparseBlockReference` e `SparseTermDirectoryEntry` in
  `src/Jigen/Jigen.Index.Sparse`.
- Il riferimento deriva dal blocco esistente e conserva posizione nella
  `StoredList` dei blocchi, conteggio, estremi documento/vettore e limiti.
  Non duplica i pesi dei posting.
- La directory conserva una voce per termine, con riferimenti immutabili
  ordinati per intervallo. Rifiuta duplicati e sovrapposizioni.
- I metadati serializzati sono separati dai payload: la futura lettura della voce
  non richiede di aprire o deserializzare i blocchi.
- Formato little-endian versione 1: intestazione con versione, termine e conteggio,
  12 byte. Ogni riferimento usa 32 byte fissi più le due chiavi di confine.
  Il riferimento contiene posizione, conteggio, minimo, massimo, indici dei vettori,
  lunghezze delle chiavi e chiavi.
- Directory vuota ammessa con limiti zero; blocchi vuoti non referenziabili.
  La deserializzazione rifiuta versioni, conteggi, lunghezze, intervalli,
  posizioni, limiti e indici invalidi, riferimenti ripetuti e byte residui.
- Non sono implementati indirizzamento termine → posizione, gestione dei file
  per collezione, cursori o ricerca. Gli indici di blocco richiedono una lista
  append-only senza `RemoveAt`; la coerenza fra file richiede il writer futuro.
- Intervalli non sovrapposti descrivono una lista ordinata, non risolvono ancora
  inserimenti fuori ordine o aggiornamenti: merge e versioni restano in SP-05.
- Verifica di produzione: build riuscita, 35 regressioni superate, formatter
  applicato e nessun errore nell'editor. I test specifici appartengono a SP-03AT.
- Skill: `workflow-development`, `phase-development-actor-task-planning`,
  `phase-development-technology-resolution`, `phase-development-project-conventions`,
  `phase-development-configuration-options`, `phase-development-task-execution`,
  `phase-development-deviation-routing`, `phase-development-development-handoff`,
  `base-be-base-rules`, `implementation-be-dotnet-dev`, `base-project-decision-log`,
  `capabilities-dev-problem-solving-methodology`, `capabilities-italian-text-humanization`,
  `run-tests`. Metodologia applicata alle invarianti del formato; revisione italiana
  alla documentazione; `run-tests` alle regressioni esistenti.

File modificati: i due nuovi tipi, questo dossier, il piano e il dossier di
scouting che possiede il contratto di ranking confermato.

### SP-03AT Verificare la directory

Stato: closed. Test avviati dopo approvazione esplicita del codice.

- Aggiunti i test della directory in `SparseTermDirectoryTests.cs`.
- Flush e riapertura di una vera `StoredList` conservano termine, posizioni,
  conteggi, estremi documento/vettore e limiti. La prova non crea né apre file
  di posting: i riferimenti sono leggibili indipendentemente dal payload.
- Verificati directory vuote, riferimenti con un solo posting, intervalli
  adiacenti dello stesso documento e chiavi binarie con prefissi comuni.
  Le posizioni fisiche dei blocchi non devono seguire l'ordine delle chiavi.
- Verificati immutabilità, formato little-endian atteso, ogni prefisso troncato
  e rifiuto di metadati corrotti, riferimenti duplicati e intervalli sovrapposti.
- Estratta la gestione della directory temporanea in `TestStorageDirectory.cs`,
  condivisa dai test del blocco e della directory; nessun helper in produzione.
- Verifica: 72 test superati nella suite completa, formatter e controllo degli
  spazi superati, nessun errore nell'editor. Resta il warning NU1902 preesistente.
- I test eliminano le directory uniche sotto `.temp` alla chiusura dei file.
  Nessun dato scritto sotto `/data`; nessuna modifica ai tipi di produzione.
- Perimetro: i tre file del progetto di test, piano e dossier.
- Skill: `workflow-development`, `phase-development-actor-task-planning`,
  `phase-development-technology-resolution`, `phase-development-project-conventions`,
  `phase-development-configuration-options`, `phase-development-task-execution`,
  `phase-development-deviation-routing`, `phase-development-development-handoff`,
  `base-be-base-rules`, `implementation-be-dotnet-dev`, `base-project-decision-log`,
  `capabilities-dev-problem-solving-methodology`, `capabilities-italian-text-humanization`,
  `run-tests`. Metodologia applicata ai difetti di persistenza e formato;
  revisione italiana alla documentazione; `run-tests` al runner VSTest/xUnit.

Handoff: cursore SP-03B e relativi test SP-03BT, ora chiusi.

## Deviation routing

Il passaggio diretto dallo scouting al primo incremento è confermato dall'utente.
Analisi e architettura formali non sono concluse: le scelte ancora aperte restano
prerequisiti dei task successivi. Nessuna modifica al formato denso, a `AGENTS.md`,
allo stack o ai contratti di rete.

### SP-03BT Verificare il cursore

Stato: closed. L'utente ha approvato esplicitamente il codice del cursore.

- Aggiunti 24 casi in `tests/Jigen.Index.Sparse.Tests/SparsePostingCursorTests.cs`.
  Nessuna modifica al codice di produzione.
- Verificati attraversamento dopo flush e riapertura, pesi e identità multivettore,
  seek inclusivo, chiavi con prefissi, destinazioni intermedie e seek retrogrado.
  Le posizioni fisiche dei blocchi sono invertite rispetto agli intervalli logici.
- Verificati directory vuota, fine lista stabile e salto dopo lettura parziale.
  Riferimenti non leggibili nei blocchi saltati dimostrano che seek e salto non
  ne caricano il payload; anche il seek oltre l'ultimo estremo evita la lettura.
- Verificate discordanza di conteggio, minimo, massimo, documenti e indici vettore
  agli estremi, oltre ai riferimenti fuori lista. Il fallimento non pubblica
  posting e resta ripetibile; il salto consente di esaurire il cursore.
- Questi casi intercettano posting persi o ripetuti, arretramento, letture dei
  blocchi saltati, confusione tra vettori e accettazione di metadati discordanti.
- Verifica: intera suite sparse, 96 test superati, zero fallimenti o test saltati.
  Formatter verificato; nessun errore rilevato nell'editor. Temporanei unici
  sotto `.temp` rimossi dal helper dopo la chiusura dei file.
  Rimane il warning NU1902 preesistente per MessagePack 3.1.7.
- Skill: `workflow-development`, `phase-development-technology-resolution`,
  `phase-development-project-conventions`, `phase-development-configuration-options`,
  `phase-development-task-execution`, `phase-development-development-handoff`,
  `base-be-base-rules`, `implementation-be-dotnet-dev`,
  `capabilities-dev-problem-solving-methodology`, `run-tests`.
  La metodologia orienta i casi ai difetti del seek e dello stato;
  `run-tests` governa l'esecuzione VSTest/xUnit.
- File: nuovo file dei test, piano e dossier. Handoff: SP-03, ancora bloccato
  sul lookup dei termini della collezione.

### SP-03 Implementare Block-Max MaxScore

Stato: closed. Codice implementato, compilato e approvato esplicitamente
dall'utente; verifica funzionale eseguita nel task separato SP-04.

- L'utente ha richiesto il completamento del motore, escludendo lo Store.
  `SparseIndex` riceve directory e blocchi della singola collezione. Il chiamante
  li apre, li mantiene immutati e li chiude; il motore non scrive e non dispone
  file. Per modificare il contenuto occorre ricostruire l'istanza a dati stabili.
- Il costruttore legge solo le voci della directory e costruisce un dizionario
  termine-posizione. Identificatori duplicati causano `InvalidDataException`.
  `TryGetTerm` rappresenta l'assenza con false. Nessun lookup statico o globale.
  L'apertura resta proporzionale ai metadati letti e non e a costo costante.
- `Search` riceve una mappa termine-peso e k. Termini sconosciuti non generano
  candidati; query senza termini presenti e k zero restituiscono una lista vuota.
  Termini di peso zero partecipano alla corrispondenza. Query con identificatori
  negativi o pesi non finiti e k negativo sono errori di argomento.
- `SparseMaxScoreSearch` usa cursori per query e intervalli delimitati dal minimo
  degli estremi finali correnti. Calcola limiti locali dal massimo del blocco
  per pesi query positivi e dal minimo per quelli negativi, includendo zero.
  Salta gli intervalli con limite strettamente inferiore alla soglia top-k,
  avanzando i soli riferimenti, senza leggere i relativi payload.
- MaxScore ordina i limiti locali e divide le liste in essenziali e non essenziali.
  Solo le essenziali generano candidati; le altre completano il prodotto scalare.
  La divisione viene aggiornata quando sale la soglia, anche dentro l'intervallo.
  I limiti sono sommati con arrotondamento verso l'alto; il prefisso non essenziale
  viene ricontrollato nell'ordine di accumulo del punteggio. I pareggi non vengono
  potati. Non sono introdotti quantizzazione o limiti al numero dei candidati.
- I pesi persistenti restano float. Prodotti e somme usano double e i termini
  sono sommati per identificatore crescente. `SparseSearchResult.Score` e double:
  evita l'overflow float dei prodotti di pesi finiti. La verifica differenziale
  deve usare lo stesso ordine e la stessa aritmetica.
- Il top-k conserva al massimo k documenti finalizzati in una coda di priorita
  e un documento in aggregazione. Le identita visitate sono ordinate; per ciascun
  documento si conserva il massimo dei soli vettori corrispondenti. I risultati
  sono ordinati per punteggio decrescente e chiave binaria crescente.
- La cancellazione viene propagata; nessun errore I/O e nascosto. Query simultanee
  non condividono cursori, punteggi o heap. Il motore non verifica l'appartenenza
  delle liste a una collezione: questa e una responsabilita del proprietario
  dei file, come gia nel contratto del cursore.
- File: `SparseIndex.cs`, `SparseMaxScoreSearch.cs`, `SparseSearchResult.cs`
  in `src/Jigen/Jigen.Index.Sparse`, piano e dossier. Formati e tipi precedenti
  invariati; nessuna configurazione o dipendenza aggiunta, nessun test modificato.
- Verifica: build riuscita con C# 14; formatter verificato; nessun errore
  nell'editor; 96 regressioni esistenti superate. Rimane NU1902 preesistente.
  La nuova ricerca e verificata dal task separato SP-04 riportato sotto.
  Misura locale in Release riportata nella verifica indipendente del motore.
- Skill: `workflow-development`, `phase-development-technology-resolution`,
  `phase-development-project-conventions`, `phase-development-configuration-options`,
  `phase-development-task-execution`, `phase-development-development-handoff`,
  `base-be-base-rules`, `implementation-be-dotnet-dev`,
  `capabilities-dev-problem-solving-methodology`, `run-tests`.
  La metodologia serve alla sicurezza dei limiti e alla verifica numerica;
  `run-tests` riguarda le regressioni esistenti. Nessun agente aggiuntivo.

Uso, con le due liste gia aperte della stessa collezione:

```csharp
var index = new SparseIndex(termDirectory, postingBlocks);
IReadOnlyDictionary<int, float> query = new Dictionary<int, float>
{
    [17] = 1.5f,
    [91] = -0.25f
};
var hits = index.Search(query, k: 10);
```

### SP-04 Verificare la ricerca

Stato: closed. L'utente ha approvato esplicitamente il motore e autorizzato
tutti i test del motore, mantenendo lo Store escluso.

- Aggiunti 31 casi in `tests/Jigen.Index.Sparse.Tests/SparseIndexTests.cs`.
  Il codice di produzione e rimasto invariato.
- L'oracolo esaustivo scorre i posting originali dei termini della query,
  accumula per coppia documento/vettore in ordine di termine, prende il massimo
  per documento e ordina il risultato. Non usa cursori, metadati o potatura.
  Si confrontano sia tutte le chiavi sia i punteggi double esatti, senza tolleranza.
- Eseguiti 3.150 confronti generati: cinque semi per tre profili di peso
  (non negativo, firmato, estremo), 80 documenti con fino a quattro vettori,
  12 termini indicizzati, 35 query per corpus e sei valori di k.
  Sono inclusi termini sconosciuti, chiavi con prefissi, blocchi disallineati
  di dimensione variabile e ordine fisico inverso rispetto agli intervalli.
- I casi deterministici verificano massimo per vettore anziche somma dei massimi
  per termine, esclusione dei vettori senza corrispondenze anche con punteggi
  negativi, contributi delle liste non essenziali, pareggi, zeri e valori estremi.
  Prevengono fusione di vettori, inserimento di zeri da documenti non candidati,
  perdita di contributi e overflow float nel punteggio.
- Due prove usano riferimenti non leggibili per gli intervalli potati: una
  verifica il salto dell'intero intervallo, l'altra i candidati presenti solo
  nelle liste non essenziali con limite totale ancora competitivo.
  Con k maggiore, la lettura degli stessi riferimenti fallisce: il controllo
  negativo distingue la potatura dalla mancata partecipazione del termine.
  Un limite uguale alla soglia non viene potato.
- Verificati flush e riapertura delle due StoredList, lookup di termini presenti,
  vuoti e assenti, rifiuto dei duplicati, due collezioni con stessi identificatori
  e dati differenti, e venti query lanciate tramite task sulle due istanze.
  La chiusura di una collezione non altera l'altra.
- Verificati query e indice vuoti, termini assenti, k zero e maggiore dei
  candidati, argomenti invalidi, pesi non finiti, token gia cancellato e ricerca
  successiva riuscita. I casi vuoti non leggono i posting non pertinenti.
- Verifica: 127 test totali superati in Debug e Release, zero fallimenti o
  test saltati. Formatter verificato e nessun errore nell'editor.
  Temporanei sotto `.temp`, rimossi dopo la chiusura dei file.
  Rimane NU1902 per la dipendenza MessagePack 3.1.7 preesistente.
- File: nuovo file di test, piano e questo dossier. Nessuno snapshot allegato
  modificato. Nessuna dipendenza o configurazione aggiunta.
- Skill: `workflow-development`, `phase-development-technology-resolution`,
  `phase-development-project-conventions`, `phase-development-configuration-options`,
  `phase-development-task-execution`, `phase-development-development-handoff`,
  `base-be-base-rules`, `implementation-be-dotnet-dev`,
  `capabilities-dev-problem-solving-methodology`, `run-tests`.
  La metodologia guida l'oracolo indipendente e i controlli negativi;
  `run-tests` governa la suite VSTest/xUnit.
- Handoff: motore consegnato nel perimetro confermato. Integrazione Store esclusa.

### Verifica indipendente del motore sparse

Stato: eseguita su richiesta dell'utente, senza modifiche alla produzione.

- Prova differenziale piu ampia di quella dei test permanenti, eseguita con un
  harness temporaneo poi rimosso: 40 corpus casuali, 40 query per corpus e sette
  valori di k, per 11.200 confronti. I profili di peso includono negativi, zero,
  estremi a 1e30, uno e subnormali; i blocchi hanno dimensione 1-4, quindi ogni
  termine occupa piu blocchi e le identita di un documento sono divise fra blocchi.
  Nessuna discrepanza su chiavi e punteggi double esatti.
- Misura locale, configurazione Release, corpus sintetico con 3.000 documenti,
  200 termini, 359.820 posting, 5.727 blocchi da 64 e query di 20 termini.
  Costruzione dell'indice su `StoredList`: circa 350 ms. Apertura di `SparseIndex`:
  1-7 ms, proporzionale ai termini della directory. Ricerca: 17,7 ms con k=1,
  18,1 ms con k=10, 21,2 ms con k=100, 27,8 ms senza potatura.
- La potatura riduce il tempo di circa un terzo, ma 18 ms per query resta lontano
  dai tempi dichiarati dalle implementazioni pubblicate, che operano su pesi
  quantizzati e strutture specializzate. Il confronto non e omogeneo e non
  autorizza a stimare altri corpus.
- Cause individuate, non ancora ottimizzate: da due a tre operazioni di cursore
  per termine per candidato, con ricerca binaria ripetuta anche quando il cursore
  e gia posizionato; `SelectEssentialTerms` riordina e alloca a ogni cambiamento
  della soglia; confronto scalare delle chiavi binarie; deserializzazione dei
  blocchi attraverso la cache di 512 voci di `StoredList`.
- Limiti dichiarati: il motore non verifica che directory e blocchi appartengano
  alla stessa collezione, non controlla scritture concorrenti e non comprime
  i posting. I punteggi sono double, quindi il tipo differisce da quello denso.
- Nel corso della verifica un merge ha spostato i test del motore e le modifiche
  SP-04 in `stash@{0}`; su indicazione dell'utente lo stash e stato applicato
  senza rimuoverlo e la suite e tornata a 127 test superati in Release.

## Ottimizzazione del motore sparse

Stato: codice pronto, verificato con le regressioni; attende approvazione.

- Baseline Release, corpus sintetico con 3.000 documenti, 200 termini, 359.820
  posting, 5.727 blocchi da 64, query di 20 termini: 16,6 ms e 2.872 KB allocati
  per query con k=10. Lettura sequenziale dell'intero corpus: 22,7 MB allocati.
- Intervento 1, percorsi rapidi nel cursore: `Seek` non ripete la ricerca sui
  blocchi quando la destinazione cade nel blocco gia caricato o non supera il
  posting corrente. 16,1 -> 9,4 ms.
- Intervento 2, copie rimosse: i costruttori interni prendono possesso degli
  array gia deserializzati, evitando la seconda copia che `ToArray` faceva
  sull'intero blocco e sull'intera voce di directory. 2.872 -> 1.724 KB.
- Intervento 3, accesso diretto: i tipi conservano l'array e lo espongono come
  `IReadOnlyList`, senza il wrapper `ReadOnlyCollection` allocato per ogni blocco,
  e i percorsi caldi usano l'array tramite accessore interno. 9,0 -> 8,2 ms.
- Risultato finale con k=10: 8,2 ms e 1.712 KB per query, quindi tempo dimezzato
  e allocazioni ridotte del 40%. Con k=1 7,8 ms, con k=100 8,8 ms, senza potatura
  9,6 ms. La potatura da sola vale circa il 15% in questo corpus.
- Scartata dopo misura la riduzione della dimensione dei blocchi: da 8 a 256
  posting per blocco il tempo passa da 17,3 a 8,1 ms e le allocazioni da 5.645 a
  1.940 KB. Blocchi piccoli moltiplicano metadati e costi per blocco e peggiorano
  la potatura effettiva, quindi la dimensione resta una scelta di ingestione.
- Le copie dei dati restano obbligatorie: `StoredList` legge in buffer presi da
  `ArrayPool` e li restituisce subito dopo `Deserialize`, quindi trattenere il
  buffer di lettura non e sicuro. Si copia una volta sola per blocco.
- Effetto collaterale dichiarato: `Postings` e `Blocks` restituiscono
  `IReadOnlyList` appoggiata a un array. Un chiamante che forzasse il cast all'array
  potrebbe mutarlo. Il tipo resta immutabile nell'uso previsto e i test lo verificano.
- Formato persistente, firme pubbliche e comportamento di ranking invariati.
- Verifica: 127 test superati in Debug e Release; formatter verificato.
  Rimane NU1902 della dipendenza MessagePack preesistente.
- Skill: `workflow-development`, `phase-development-technology-resolution`,
  `phase-development-project-conventions`, `phase-development-configuration-options`,
  `phase-development-task-execution`, `base-be-base-rules`,
  `implementation-be-dotnet-dev`, `capabilities-dev-problem-solving-methodology`,
  `run-tests`. La metodologia ha guidato baseline, ipotesi singole e misure.
- Proposte non implementate, da decidere: cache dei blocchi deserializzati per
  query ripetute; formato a colonne per sostituire le copie per posting con poche
  copie in blocco, con cambio di versione del formato; struttura dei cursori
  tipo WAND per ridurre il lavoro per candidato.

## Development handoff

Motore sparse consegnato nel perimetro confermato, con 127 test superati
in Debug e Release. Blocchi, directory, cursori, lookup per collezione e ricerca
Block-Max MaxScore sono implementati, approvati e verificati.
SP-01, SP-02, SP-03A, SP-03AT, SP-03B, SP-03BT, SP-03 e SP-04 sono chiusi.
Il confronto esaustivo conferma risultati e punteggi nei corpus verificati;
le prove di potatura verificano il mancato caricamento dei payload saltati.
Misura locale in Release riportata nella verifica indipendente: apertura
proporzionale ai termini della directory e ricerca nell'ordine di 18 ms su
360.000 posting. Non si dichiarano prestazioni su altri corpus ne apertura
indipendente dal volume dei dati.
SP-05 e SP-06, integrazione Store e relativi test, sono differiti per scelta
esplicita dell'utente. Per riprenderli occorre definire nella fase di analisi
il contratto pubblico dei vettori sparsi e in architettura aggiornamenti,
WAL, compattazione e recupero. Nessun prototipo o codice di interfaccia coinvolto.

## DEV-GRANITE-R2-CONFIGURATION

Status: Completed.

- Added standalone CPU configurations for Granite Embedding 311M and 97M Multilingual R2.
- Added a dedicated `Granite` processing profile with right padding, CLS pooling and L2 normalization, without the BGE-M3-specific token-id mapping.
- Set passage defaults to 512 tokens with 64-token overlap for the accuracy-oriented 311M model and 384 tokens with 64-token overlap for the latency-oriented 97M model.
- Added direct Hugging Face `tokenizer.json` loading through the official Rust tokenizer bindings for .NET. Both configurations now use the tokenizer asset published by IBM.
- Verification: both files parsed as JSON and satisfied all `PassageSplittingOptions` startup constraints. `Jigen.SemanticTools` and `Jigen.Embedding.Handlers` build successfully.
- Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, `implementation-be-dotnet-dev`, and `capabilities-dev-problem-solving-methodology`.

## DEV-GRANITE-R2-MODEL-VALIDATION

Status: Completed.

- Deployed the official 97M and 311M tokenizer JSON and quantized AVX2 ONNX assets under `/data/onnx`.
- Verified all four assets against the published Hugging Face LFS SHA-256 hashes.
- Added real-model tests for tokenization, output dimensions, finite values, L2 normalization, Italian semantic ranking, and batch generation.
- Verification: 8 Granite tests passed. The complete `Jigen.SemanticTools.Tests` suite passed 32 tests.
- Activated skills: `workflow-development`, `phase-development-project-conventions`, `phase-development-task-execution`, `base-be-base-rules`, `base-dossier-writing`, `implementation-be-dotnet-dev`, `capabilities-dev-problem-solving-methodology`, and `run-tests`.

## DEV-GRANITE-97M-INDEX-BENCHMARK

Status: Completed.

- Indexed 1,000 texts with Granite 97M and Jigen HNSW.
- Executed 2,000 generated searches against the persisted index.
- Expected-document top-1 accuracy was 57.40%. First-query accuracy was 50.50%. Second-query accuracy was 66.00%.
- Cosine score minimum, average, and maximum were 0.757785, 0.865203, and 0.965269.
- Generated `/data/score.txt` and persisted the index under `/data/granite-97m-test-index`.
- Activated skills: `workflow-development`, `phase-development-project-conventions`, `phase-development-task-execution`, `base-be-base-rules`, `base-dossier-writing`, `implementation-be-dotnet-dev`, and `capabilities-dev-problem-solving-methodology`.

## DEV-GRANITE-PARAMETER-EVALUATION

Status: Completed.

- Compared Granite 97M at 384 dimensions with Granite 311M at 128, 256, 384, 512, and 768 dimensions over 2,000 searches.
- Granite 311M at 768 dimensions reached 69.30% exact top-1. Granite 97M reached 58.70%.
- HNSW with `M=32`, `EfConstruction=400`, and `EfSearch=128` reached 68.95% top-1 and 98.75% exact-result agreement.
- Granite 311M at 512 dimensions reached 69.05% exact top-1.
- A 512-token passage size splits 9 of 1,000 documents. No document exceeds 1,024 tokens.
- Generated `/data/granite-parameter-results.txt`.
- Activated skills: `workflow-development`, `phase-development-project-conventions`, `phase-development-task-execution`, `base-be-base-rules`, `base-dossier-writing`, `implementation-be-dotnet-dev`, and `capabilities-dev-problem-solving-methodology`.

## DEV-EMBEDDING-MODEL-COMPARISON-FIRST100

Status: Completed.

- Compared exact retrieval over the first 100 documents and 200 generated queries.
- BGE-M3 reached 96.50% top-1. Nomic v1.5 INT8 reached 96.00%.
- Granite 311M reached 85.00% at both 512 and 768 dimensions. Granite 97M reached 81.00%.
- Nomic encoded the sample in 12.170 seconds. BGE-M3 required 50.526 seconds.
- Generated `/data/model-comparison-first100.txt`.
- Activated skills: `workflow-development`, `phase-development-project-conventions`, `phase-development-task-execution`, `base-be-base-rules`, `base-dossier-writing`, `implementation-be-dotnet-dev`, and `capabilities-dev-problem-solving-methodology`.

## DEV-HNSW-BALANCED-PRODUCTION-PROFILE

Status: Completed.

- Configured `M=32`, `EfConstruction=400`, and `EfSearch=128` in the server appsettings.
- Kept graph quantization disabled and exact reranking enabled.
- The measured profile reached 98.75% exact-result agreement on 2,000 searches.
- Existing HNSW graphs require rebuilding before `M` and `EfConstruction` take full effect.
- Verification: server configuration validation and server build succeeded.
- Activated skills: `workflow-development`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, `base-dossier-writing`, `implementation-be-dotnet-dev`, and `capabilities-dev-problem-solving-methodology`.

## DEV-NOMIC-OPTIMIZED-CONFIGURATION

Status: Completed.

- Added a standalone Nomic INT8 CPU configuration with explicit `Nomic` profile and 768-dimensional output.
- Allocated two embedding workers with 10 ONNX intra-op threads each on the 22-logical-CPU host.
- Set 512-token passages with 64-token overlap for the short-report corpus.
- Set `search_document` as the default task and exposed the four Nomic task prefixes.
- Verification: JSON constraints, embedding worker build, and the real-model Nomic HNSW score test succeeded.
- Activated skills: `workflow-development`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, `base-dossier-writing`, `implementation-be-dotnet-dev`, `capabilities-dev-problem-solving-methodology`, and `run-tests`.

## DEV-NOMIC-SERVER-CONFIGURATION

Status: Completed.

- Added a standalone all-in-one server profile for the Nomic INT8 text model.
- Combined the optimized Nomic worker settings with the balanced HNSW `32/400/128` profile.
- Preserved server identity, storage, and RabbitMQ settings. Image embeddings remain disabled.
- Verification: JSON constraints and server build succeeded. The environment file was copied to the runtime output.
- Activated skills: `workflow-development`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, `base-dossier-writing`, `implementation-be-dotnet-dev`, and `capabilities-dev-problem-solving-methodology`.

## DEV-NOMIC-TASK-ROUTING

Status: Completed.

- Confirmed that document ingestion and sentence search previously omitted the embedding task and both inherited `search_document` from `DefaultTask`.
- Document ingestion now requests `search_document`. REST and gRPC sentence searches request `search_query`.
- Server build succeeded. The focused API test was added but execution is blocked by a pre-existing constructor error in `EmbeddingControllerTests`.
- Activated skills: `workflow-development`, `phase-development-project-conventions`, `phase-development-task-execution`, `base-be-base-rules`, `base-dossier-writing`, `implementation-be-dotnet-dev`, and `capabilities-dev-problem-solving-methodology`.

## Technology resolution

| Slot | Technology | Implementation skill |
| --- | --- | --- |
| Backend platform | .NET 10 / C# | `implementation-be-dotnet-dev` |
| Embedding runtime | ONNX Runtime 1.27 | No dedicated implementation skill; existing project implementation applies |

Task `DEV-BGE-M3-PROFILE` changes the local ONNX embedding profile contract and documentation. Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `base-be-base-rules`, `implementation-be-dotnet-dev`, and `capabilities-dev-problem-solving-methodology`. No capability skill beyond problem-solving is required.

## DEV-BGE-M3-PROFILE

Status: Completed.

- Added the `BgeM3` processing profile with right padding, CLS pooling, and L2 normalization.
- Documented the official Hugging Face ONNX file layout and configuration.
- Scope is dense embeddings only; sparse and ColBERT outputs remain excluded.
- Verification: `dotnet build src/Jigen/Jigen.SemanticTools/Jigen.SemanticTools.csproj --no-restore` succeeded with no errors. The existing `NETSDK1206` warning concerns macOS runtime identifiers in `Microsoft.ML.OnnxRuntime.Extensions`.
- Editor diagnostics reported no errors in the modified C# files.
- Tests were not modified or added because production code and tests are separate tasks under the backend baseline.

## DEV-BGE-M3-HNSW-SCORE

Status: Failed as expected; the regression is reproduced.

- Added a focused integration test for BGE-M3 embeddings stored and searched through `Jigen.Store` with `SmallWorldIndexer`.
- The test proves that HNSW returns the direct cosine unchanged: both values are `0.6735` for ticket `00020294` and query `prostituzuione`.
- The expected reference similarity is about `0.3355`, so the inflation occurs during embedding generation, before HNSW search.
- A focused tokenizer test confirms the cause: for `prostituzuione`, the official tokenizer emits `[0, 44794, 1667, 20493, 2]`, while the current Microsoft SentencePiece construction emits `[1, 44793, 1666, 20492, 2]`. The BOS id is wrong and every lexical token id is shifted by `-1`.
- Verification: the focused test built successfully and failed on the reference-score assertion with direct cosine `0.6735` and HNSW score `0.6735`.
- Verification: the tokenizer test built successfully and failed with the exact divergent token sequences above.
- Existing analyzer warnings in `VectorCollectionTests.cs` and the existing ONNX Runtime RID warning remain outside this task.
- Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.

## DEV-BGE-M3-TOKENIZER-FIX

Status: Completed.

- Corrected the BGE-M3 JSON-tokenizer path by mapping raw SentencePiece ids to the XLM-RoBERTa vocabulary expected by the ONNX model.
- The mapping is restricted to the `BgeM3` profile; other profiles and ONNX tokenizers are unchanged.
- Verification: `Jigen.SemanticTools` built with 0 warnings and 0 errors.
- Verification: the focused BGE-M3 Store/HNSW regression test passed, proving the corrected end-to-end similarity.
- Verification: the focused BGE-M3 public-generator test passed with similarity in the expected `0.33`–`0.34` interval.
- Verification: the Nomic Store/HNSW test passed. The unrelated query scored `0.530176`, the relevant query scored `0.634335`, and both HNSW scores matched their direct cosine values.
- Existing xUnit analyzer warnings in `VectorCollectionTests.cs` remain outside this task.
- Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.

## DEV-BGE-M3-IGNORE-TASK

Status: Production code verified; awaiting code-quality approval before the separate test update.

- BGE-M3 now ignores task text during input preparation, including explicit client values and stale non-empty defaults.
- Nomic and Qwen task formatting remains unchanged; SigLIP2 continues to ignore tasks.
- Verification: `Jigen.Embedding.Handlers` built with 0 errors. Two existing ONNX Runtime RID warnings remain.
- Verification: the BGE-M3 Store/HNSW regression test passed.
- Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.

## DEV-MULTIVECTOR-VECTOR-FILE

Status: Completed.

- The embeddings file record is now `[id length][id][vector count][vector count × dimensions × float]`, so one entry can store several vectors.
- `VectorEntry.Embeddings` carries the vectors; `Embedding` remains the single-vector shorthand, and `Vectors` resolves whichever is set.
- The position index carries the vector count (`{name}.index.jigen` record and in-memory tuple), so delete accounting, shrink, collection metrics and the embeddings record size stay exact.
- The WAL insert record carries `[vector count][dimensions][vectors]`; replay restores every vector.
- Exact (brute-force) scoring ranks a multi-vector entry by its closest vector. The HNSW index keeps one node per document and indexes the first vector.
- `GetEmbeddings` reads all vectors at store, gRPC (`Vector.Vectors`, `EmbeddingResponse.Vectors`), REST (`VectorPayload.Vectors`, `BulkVectorItem.Vectors`, transaction payloads) and client level; `GetEmbedding` keeps returning the first vector.
- Existing databases written with the previous layout are not readable by this version.
- Verification: `Jigen.Primitives`, `Jigen.Store`, `Jigen.Indexer.HNSW`, `Jigen.API`, `Jigen.Grpc`-hosted server, `Jigen-AllInOne` and `Jigen.Client` built with 0 errors.
- Verification: `MultiVectorTests` (5 tests) passed: round trip, reopen, WAL replay, max-similarity scoring, mixed-vector rejection.
- Verification: `JigenStoreTests` passed 70 of 78; the 8 failures are the pre-existing database-lock collisions of `IngestionTests` and `SeedDemoDatabaseTest`, which share `/data/jigendb`.
- Verification: `PrimitiveTests` (14) and the HNSW split-storage tests (4) passed. `Jigen.API.Tests` still fails to build on the pre-existing `EmbeddingControllerTests` constructor error; `Jigen.Grpc.Tests` fails on the pre-existing `Server.Server` constructor signature.
- Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.

## DEV-MULTIVECTOR-BENCHMARK

Status: Completed.

- Extended the macro-benchmark with `mv:K` (K vectors per document) and with the vectors/index file sizes, plus a tail-recall metric measured on a non-first vector.
- Single-vector path shows no regression against the pre-change code (HEAD in a temporary worktree): ingest 1683–2091 vs 1843–2091 docs/s, HNSW search 595–671 vs 575–630 µs, recall 0.986–0.995 vs 0.992–0.996 — all within run-to-run noise.
- Multi-vector storage is exact at every K: the vectors file equals documents × K × dimensions × 4 B plus 24 B of header per document.
- Ingest cost per document is flat (~1.5–1.7 k docs/s at 10k) while stored vectors scale linearly.
- HNSW keeps one node per document, so recall against exact max-similarity search falls to chance as K grows; a query on a non-first vector is not reachable through the graph (0.18 at K=4, 0.03 at K=32).
- Exact search cost grows linearly with K (1.8 ms at K=1 to 6.6 ms at K=32 on 10k documents).
- Verification: results recorded in `docs/benchmarks.md` under "Multi-vector entries".
- Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.

## DEV-MULTIVECTOR-HNSW-NODES

Status: Completed.

- Corrected the previous behavior: the graph never required one node per document. It accepts several nodes sharing a key (the delete lookup already kept a position list per key, and search already merged same-key nodes by best score), so indexing every vector needed no graph-format change at all.
- `WriterThread` now enqueues one index entry per stored vector instead of only the first.
- `ReconcileAsync` re-indexes every vector of a missing key, so a partial graph is rebuilt with the same shape as a normal insert.
- SQ8 exact reranking now rescores a candidate against the document's best stored vector, matching the exact index rule; it previously read only the first vector.
- Added `HnswCollection_FindsDocumentThroughAnyOfItsVectors` (round trip through three vectors of one document).
- Measured (10k docs, 128 dim): recall@10 0.902 and 0.935 on a non-first vector at K=4, versus 0.331 and 0.181 with first-vector-only indexing; graph grows to 28.35 MB (from 7.09 MB) and ingest drops to 372 docs/s, as expected from K× nodes. `mv:1` is unchanged.
- Verification: `Jigen.Store` and `Jigen.Indexer.HNSW` built with 0 errors.
- Verification: `JigenStoreTests` 64 passed and `HnswTest` 20 passed, excluding the classes that collide on `/data/jigendb`.
- Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.

## DEV-MULTIVECTOR-QDRANT-BENCHMARK

Status: Completed.

- Ran the comparative macro-benchmark (`jigendb-hnsw-w8` vs `qdrant`, `random-10k`) against a local Qdrant 1.19.2 container: Jigen post-index P50 0.22 ms vs 3.12 ms, both recall 1.000, ingest 4,066 vs 33,425 vec/s.
- Verified Qdrant's multi-vector statistics empirically: 40,000 stored vectors over 10,000 points report `points_count: 10000` and `indexed_vectors_count: 10000`. Both counters are per point; the API exposes no vector count.
- Verified Qdrant de-duplicates multi-vector hits per point (10 results, 10 unique ids), the same pooling Jigen applies when several graph nodes share a document key.
- Measured the like-for-like multi-vector workload (10k documents × 4 vectors × 128 dimensions): Qdrant ingest 1,989 docs/s plus 3.0 s indexing, search P50 4.94 ms over REST; Jigen `mv:4` 372 docs/s including graph build, search 910 µs in-process.
- Kept the probe reproducible in `.ai-generated-operations/qdrant-multivector-probe.py`.
- Verification: `MultiVectorTests` (6 tests, including collection statistics) passed.
- Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.

## DEV-LAZY-INDEXING-VERIFICATION

Status: Completed.

- Confirmed the lazy indexing parameters: `LazyIndexer(Func<IIndexer>, int threshold = 100001)` in the store layer, `jigendb-lazy-wN` (threshold 9,999, or `-t50000`) in the comparative suite, and a new `lazy` / `lt:N` mode in the macro-benchmark.
- Measured lazy vs eager (10k docs, 128 dim): lazy ingest 77,560 docs/s at K=1 and 43,685 docs/s at K=4 (versus 1629 and 331), but the deferred build is one serial reconciliation pass — 19.64 s and 119.68 s — so time to a searchable graph is 3.2× and 4.0× worse than eager, which builds concurrently across the index workers.
- Recall is unchanged by lazy indexing, including through a non-first vector (0.932 at K=4), because reconciliation now re-indexes every stored vector.
- `LazyIndexer` compares the collection's position-index size, so the threshold counts documents, not vectors.
- Found a measurement artifact in the comparative suite: its pre-index phase issues 5 searches and the first one triggers the lazy switch, so the build cost is charged to the pre-index sample and the `Index (s)` column reports only a second no-op pass (0.02 s). Documented in `docs/benchmarks.md`; the suite itself is unchanged.
- Verification: macro-benchmark runs for eager/lazy at K=1 and K=4, and comparative macro for `jigendb-lazy-w8`, `jigendb-hnsw-w8` and `qdrant` on `random-10k`.
- Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.

## DEV-MULTIVECTOR-CLIENT-CONTRACT

Status: Completed.

- Found the Python client bindings stale: `_proto/Jigen_pb2.py` was generated from the pre-change proto, so `EmbeddingVector`, `Vector.Vectors` and `EmbeddingResponse.Vectors` did not exist and the client could not send or read multi-vector entries.
- Regenerated the Python bindings from `Jigen.Client/Protos/Jigen.proto` with `grpc_tools.protoc` and restored the package-relative import in `Jigen_pb2_grpc.py`.
- Extended the Python client: `embedding=` now accepts one vector or a list of vectors (single goes to the compatible `Embeddings` field, several to `Vectors`), `upsert_many` accepts both shapes per entry, and `get_embeddings` returns every vector while `get_embedding` keeps returning the first.
- Documented the multi-vector usage in the Python README.
- Verified end to end against a live .NET gRPC server: multi-vector upsert, read back, search by each of the three vectors, single-vector fallback, bulk streaming path, and the async client.
- Recorded the reproduction in `.ai-generated-operations/check-python-multivector.py`.
- Note: the all-in-one host (`Jigen-AllInOne.csproj`) has the `Jigen.Grpc` reference commented out, so it serves no gRPC endpoints and every call falls through to the SPA fallback; the gRPC endpoint lives in `Jigen.csproj`.
- Verification: 10 Python tests passed (`pytest src/Client/Jigen.Python/tests`), up from 5, covering multi-vector mapping, mixed-shape rejection and `get_embeddings`.
- Verification: `check-python-multivector.py` reported OK against the live server, and the async path returned all vectors.
- Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.

## DEV-EMBEDDINGS-ONLY

Status: Completed.

- Removed the single-vector ambiguity end to end: `VectorEntry.Embedding`, `VectorEntry<T>.Embedding`, `IStore.GetEmbedding`, `VectorCollection.GetEmbedding`, `Store.GetEmbedding`, the REST route `/embedding`, the CQRS query `GetEmbedding` and the gRPC RPC `GetEmbedding` no longer exist.
- `VectorEntry.Embeddings` is now the only vector payload at every layer; a single-vector entry is a list of one, and the store, WAL, transaction, shrink and indexers read that list.
- `SmallWorldIndexer.AddToIndex` expands one entry into one node per vector, so callers no longer fan out per vector; `WriterThread` and `ReconcileAsync` pass whole entries.
- Wire contract: `Vector` keeps only `Vectors` (field 5 reserved), the stored-vector reply is the new `StoredEmbeddingsResponse { repeated EmbeddingVector Vectors }`, and `EmbeddingResponse` keeps its single `Embeddings` because it belongs to the embedding service (one vector per sentence).
- REST: `VectorPayload`, `BulkVectorItem` and the transaction payloads carry only `Vectors`; a payload without vectors is rejected with 400; the read route is `.../documents/{key}/embeddings`.
- Clients: .NET `Add(key, content, float[])` wraps the vector into a one-element list, `GetEmbeddings/GetEmbeddingsAsync` are the only readers; Python regenerated bindings, `embedding=` always fills `Vectors`, `get_embeddings` is the only reader.
- Verification: `Jigen.Primitives`, `Jigen.Store`, `Jigen.Indexer.HNSW`, `Jigen.API`, `Jigen.Grpc`, both server hosts, `Jigen.Client` and all benchmark/test projects built with 0 errors (only the pre-existing `EmbeddingControllerTests` constructor error remains).
- Verification: `JigenStoreTests` 64 passed, `HnswTest` 20 passed, Python 10 passed.
- Verification: live server — the Python contract script reported OK, the REST upsert returned 2 vectors from `.../embeddings`, a payload without vectors got 400, and the .NET client read back `[[1, 0]]` and `[[1, 0], [0, 1]]`.
- Verification: macro-benchmark smoke run at 5k×64 reproduced recall 0.998.
- Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.

## DEV-EMBEDDINGS-ONLY-GUARD

Status: Completed.

- The vector RPCs (`SetVector`, `SetVectors`, `AppendVector`, `SetRawVector`) now reject a request whose `Vectors` list is empty with `InvalidArgument`, so a client still sending the removed single-vector field fails loudly instead of storing an entry without vectors.
- REST already rejected the same case with 400.
- Verification: `Jigen.csproj` built with 0 errors; documented in `docs/server/grpc-api.md`.
- Activated skills: `workflow-development`, `phase-development-project-conventions`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.

## DEV-EMBEDDINGS-ONLY-BENCHMARK

Status: Completed.

- Re-ran the macro-benchmark after the embeddings-only refactor to confirm no runtime cost: single-vector ingest 1904–2059 docs/s (was 1838), HNSW search 559–602 µs (was 745), recall 0.993 (was 0.994), graph and vectors file sizes unchanged.
- Multi-vector unchanged within run-to-run variance: K=4 ingest 366–388 docs/s (was 372), recall 0.895–0.900 / 0.926–0.932 on a non-first vector (was 0.902 / 0.935); K=8 ingest 171 docs/s, recall 0.815 / 0.831.
- Lazy indexing unchanged in shape: K=1 ingest 104,644 docs/s then a 14.54 s build; K=4 91,237 docs/s then 102.22 s.
- 100k single-vector run: ingest 1186 docs/s, search 1153 µs, recall 0.807, reopen 343 ms (previous run 1048, 1349 µs, 0.798, 444 ms).
- Comparative suite against Qdrant 1.19.2 at `random-10k` still runs: Jigen HNSW w8 4,033 vec/s and 0.18 ms post-index P50 versus Qdrant 28,066 vec/s and 2.73 ms, both recall 1.000.
- `docs/benchmarks.md` updated with the re-measured multi-vector and lazy tables.
- Activated skills: `workflow-development`, `phase-development-project-conventions`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.
