# Piano di sviluppo dei vettori sparsi

## Coordinamento confermato

Un attore: questa sessione, responsabile di codice, integrazione e verifica.
Esecuzione sequenziale nel checkout corrente, senza agenti aggiuntivi.
Piano confermato dall'utente il 23 ottobre 2026.
Baseline: `8638cc9`; i documenti di scouting sono modifiche locali da preservare.

Fonte delle attività: richiesta diretta di implementare Block-Max MaxScore e
approvazione del primo incremento. Analisi e architettura formali non completate.
Stack: .NET 10, C# 14, xUnit. Nessun nuovo pacchetto.
Il progetto dedicato e l'ambito per collezione sono fissati in
[Recommendation and handoff](../scouting/dossier.md#recommendation-and-handoff).
Decisioni e fonti: [Paper su prestazioni e struttura dei dati](../scouting/dossier.md#paper-su-prestazioni-e-struttura-dei-dati).

## Cronologia

| Ordine | Questa sessione | Gate |
| --- | --- | --- |
| 1 | [SP-01 Blocco persistente di posting](#sp-01-blocco-persistente-di-posting), closed | codice approvato esplicitamente dall'utente |
| 2 | [SP-02 Verificare il blocco persistente](#sp-02-verificare-il-blocco-persistente), closed | 35 test superati |
| 3a | [SP-03A Directory persistente dei termini](#sp-03a-directory-persistente-dei-termini), closed | codice approvato esplicitamente dall'utente |
| 3a-test | [SP-03AT Verificare la directory](#sp-03at-verificare-la-directory), closed | suite completa: 72 test superati |
| 3b | [SP-03B Cursore dei posting](#sp-03b-cursore-dei-posting), closed | codice approvato esplicitamente |
| 3b-test | SP-03BT Verificare il cursore, closed | suite completa: 96 test superati |
| 3c | [SP-03 Implementare Block-Max MaxScore](#sp-03-implementare-block-max-maxscore), closed | codice approvato esplicitamente |
| 4 | [SP-04 Verificare la ricerca](#sp-04-verificare-la-ricerca), closed | 127 test superati in Debug e Release |
| 4-perf | [SP-07 Ottimizzare il motore sparse](#sp-07-ottimizzare-il-motore-sparse), code-ready | dimezzato il tempo; attende approvazione |
| 4a | [SP-05A Modello sparso nel documento](#sp-05a-modello-sparso-nel-documento), code-ready | attende approvazione esplicita del codice |
| 4b | [SP-05B Persistere le coppie nel record del documento](#sp-05b-persistere-le-coppie-nel-record-del-documento), code-ready | attende approvazione esplicita del codice |
| 4c | [SP-05C Costruire l'indice di una collezione](#sp-05c-costruire-lindice-di-una-collezione), code-ready | attende approvazione esplicita del codice |
| 4d | SP-05D Indicizzare durante l'invio, superseded | sostituito dall'indice a segmenti |
| 4e | [SP-05E Indice a segmenti con tombstone](#sp-05e-indice-a-segmenti-con-tombstone), code-ready | attende approvazione esplicita del codice |
| 4f | [SP-05F Scrittore per lotti e persistenza della collezione](#sp-05f-scrittore-per-lotti-e-persistenza-della-collezione), code-ready | attende approvazione esplicita del codice |
| 4g | [SP-05G Compattazione dei segmenti](#sp-05g-compattazione-dei-segmenti), code-ready | attende approvazione esplicita del codice |
| 4h | [SP-05H Collegamento allo Store](#sp-05h-collegamento-allo-store), code-ready | attende approvazione esplicita del codice |
| 5 | [SP-05 Integrare lo Store multivettore](#sp-05-integrare-lo-store-multivettore), deferred | escluso dal completamento corrente |
| 6 | [SP-06 Verificare il ciclo persistente](#sp-06-verificare-il-ciclo-persistente), deferred | dipende dall'integrazione Store esclusa |

## SP-01 Blocco persistente di posting

- Attore: questa sessione. Stato: closed. Ordine: 1. Prerequisiti: piano confermato.
- Obiettivo: record immutabile serializzabile con `IStorableItem` in `StoredList`.
- Perimetro: `src/Jigen/Jigen.Index.Sparse/`, registrazione in `Jigen.sln`,
  documentazione. Dipendenza dal solo progetto `Jigen.Primitives`.
- Identità del posting: chiave binaria del documento e indice del vettore sparso;
  nessun riferimento alla posizione densa, che cambia con la compattazione.
- Ordinamento: chiave binaria lessicografica, poi indice del vettore; niente duplicati.
- Pesi float non quantizzati. Minimo e massimo includono zero, contributo dei posting assenti.
- Passi: introdurre posting e blocco; serializzare in little-endian con versione;
  verificare integrità del payload in lettura; compilare e rieseguire regressioni esistenti.
- Verifica: build di `Jigen.Index.Sparse` e della dipendenza `Jigen.Primitives`;
  regressioni esistenti `PrimitiveTests`
  con esclusione di `CreateList`, `SizeTest` e `ShrinkDbTest`, che scrivono o cancellano
  percorsi fissi condivisi sotto `/data`;
  formatter limitato ai nuovi file; ispezione del formato e dell'immutabilità.
- Esclusi: indice per termine, algoritmo top-k, modifica a `StoredList`, integrazione
  nello Store, WAL, API e client. Il record non dimostra un'apertura a costo costante.
- Skill: quelle dichiarate nel [Task execution](dossier.md#task-execution).
- Consegna: codice e formato documentato; destinatario utente per approvazione,
  poi SP-02 Verificare il blocco persistente.
- Evidenze: [Task execution](dossier.md#task-execution). Il round trip del nuovo
  blocco è verificato dal task separato SP-02.

## SP-02 Verificare il blocco persistente

- Attore: questa sessione. Stato: closed. Ordine: 2.
- Dipendenza: SP-01 Blocco persistente di posting e approvazione esplicita del codice.
- Consegna: test xUnit di round trip tramite `StoredList`, flush e riapertura,
  pesi firmati, ordinamento, multivettore, immutabilità e payload corrotto.
- Perimetro: `tests/Jigen.Index.Sparse.Tests`, progetto xUnit dedicato con riferimento
  a `Jigen.Index.Sparse`, registrato in `Jigen.sln`. Versioni dei pacchetti allineate
  ai test esistenti. Non spostare tipi specifici in `Jigen.Primitives`.
- Passi: creare il progetto; scrivere test attraverso costruttori, serializzazione
  e `StoredList`; eseguire l'intero progetto con `dotnet test`; formattare i file nuovi.
- Difetti da intercettare: perdita di posting o pesi al riavvio, fusione di vettori
  dello stesso documento, limiti non sicuri per contributi assenti, alias di memoria
  mutabile, accettazione di record corrotti o non ordinati.
- Verifica: `dotnet test tests/Jigen.Index.Sparse.Tests/Jigen.Index.Sparse.Tests.csproj`;
  tutti i casi devono passare. Temporanei sotto `.temp`, con pulizia dopo la riapertura.
- Skill: quelle di SP-01, con `run-tests` applicata ai test nuovi.
- Nessuna modifica al codice di produzione. Evidenze nel dossier.
- Esito: 35 test superati, formatter verificato, temporanei rimossi dai test.
  Evidenze in [SP-02 Verificare il blocco persistente](dossier.md#sp-02-verificare-il-blocco-persistente).
- Handoff: SP-03 Implementare Block-Max MaxScore dopo esito positivo.

## SP-03 Implementare Block-Max MaxScore

- Attore: questa sessione. Stato: closed. Ordine: 3c.
- Dipendenza: SP-03BT Verificare il cursore, chiuso.
- Perimetro confermato dall'utente: completare il motore in `Jigen.Index.Sparse`,
  con lookup dei termini per collezione e ricerca Block-Max MaxScore.
  Store, scrittura dei documenti, aggiornamenti, WAL e API restano esclusi.
- Ranking confermato dall'utente: massimo dei prodotti scalari dei vettori di
  ciascun documento; un risultato per documento; pareggi per chiave lessicografica.
- Selezione dei candidati: solo vettori con termini comuni, come fissato nel
  [ranking confermato](../scouting/dossier.md#recommendation-and-handoff).
- Il motore riceve le due StoredList della collezione, di proprieta del chiamante,
  e costruisce un lookup termine-posizione dalla sola directory. Nessuno stato
  statico o condiviso fra collezioni; file aperti e stabili durante l'uso.
- Passi: lookup su directory esistente, query con pesi float, cursori locali alla
  query, limiti superiori locali e liste essenziali/non essenziali, top-k di
  documenti distinti con il massimo sui vettori corrispondenti.
- Punteggi calcolati in double dai pesi float, sommati per termine crescente;
  limiti arrotondati verso l'alto e potatura stretta per preservare i pareggi.
- Verifica prevista: build, formatter e 96 regressioni esistenti; nuovi test
  di confronto esaustivo e isolamento nel task separato SP-04 dopo approvazione.
- Skill: quelle di SP-03B, piu run-tests per le regressioni esistenti.
- Esito: motore implementato, build e formatter superati, 96 regressioni
  superate. Codice approvato esplicitamente; verifica della nuova ricerca in SP-04.

## SP-03B Cursore dei posting

- Attore: questa sessione. Stato: closed. Ordine: 3b.
- Dipendenza: SP-03AT chiuso; contratto confermato dall'utente.
- Perimetro: `SparsePostingCursor.cs`, piano e dossier; nessuna modifica ai test.
- Input: directory del termine e StoredList dei blocchi della stessa collezione.
  Il chiamante mantiene file aperti e contenuto stabile durante l'uso.
- Operazioni: avanzamento, seek in avanti inclusivo per documento/vettore,
  salto del blocco corrente senza caricare il successivo.
- Il cursore parte sul primo riferimento, senza posting corrente e senza I/O.
  Fine lista e assenza di posting sono esiti espliciti, non eccezioni.
- Seek usa ricerca binaria sui confini e poi sui posting del solo blocco scelto.
  I blocchi saltati non vengono letti. I metadati del payload letto devono
  corrispondere al riferimento, altrimenti la lettura segnala corruzione.
- Esclusi: apertura e gestione file, lookup termini, top-k, mutazioni e Store.
- Nessuna nuova configurazione o dipendenza. Skill: workflow-development,
  phase-development-technology-resolution, phase-development-project-conventions,
  phase-development-configuration-options, phase-development-task-execution,
  base-be-base-rules, implementation-be-dotnet-dev,
  capabilities-dev-problem-solving-methodology (verifica del seek e dello stato).
- Verifica: build, regressioni sparse esistenti e formatter.
- Esito: build e formatter superati; 72 regressioni superate. Codice approvato
  esplicitamente dall'utente; test specifici nel task separato SP-03BT.
- Handoff: approvazione esplicita del codice, poi SP-03BT Verificare il cursore.

## SP-03BT Verificare il cursore

- Attore: questa sessione. Stato: closed. Ordine: 3b-test.
- Dipendenza: approvazione esplicita del codice SP-03B.
- Perimetro: test sparse e documentazione; nessuna modifica alla produzione.
- Verificare avanzamento e seek inclusivo, multivettore, chiavi con prefissi,
  directory vuota e fine lista, seek retrogrado senza arretramento, salto senza
  caricamento dei payload e rifiuto della discordanza fra directory e blocco.
- I casi devono intercettare letture di blocchi saltati, posting persi o ripetuti,
  confusione tra vettori, stati correnti non validi e accettazione di riferimenti
  incoerenti. Usare StoredList reale e temporanei unici sotto `.temp`.
- Verifica: intera suite sparse; formatter dei test modificati.
- Esito: 24 nuovi casi del cursore e 72 precedenti superati, nessuno saltato;
  formatter verificato. Nessuna modifica alla produzione.
- Handoff: proseguire SP-03 dopo esito positivo.

## SP-03A Directory persistente dei termini

- Sorgente: SP-03 Implementare Block-Max MaxScore, suddiviso per consegnare
  prima la struttura che i cursori devono leggere.
- Attore: questa sessione. Stato: closed. Ordine: 3a.
- Dipendenza: SP-02 Verificare il blocco persistente, chiuso.
- Obiettivo: una voce serializzabile per termine in una `StoredList` di directory
  della collezione, separata dalla `StoredList` dei blocchi.
- Perimetro: `SparseBlockReference.cs` e `SparseTermDirectoryEntry.cs` nel
  progetto `Jigen.Index.Sparse`; piano e dossier.
- Il riferimento di blocco deriva da un blocco già costruito: posizione nella
  lista dei blocchi, conteggio, primo e ultimo identificatore di vettore, minimo
  e massimo. Nessuna copia dei pesi dei posting nella directory.
- La voce conserva l'identificatore del termine e i riferimenti ordinati per
  intervallo documento/vettore, senza sovrapposizioni o riferimenti ripetuti.
- Passi: implementare metadati immutabili; serializzare la directory con versione;
  rifiutare metadati incoerenti; compilare e rieseguire i 35 test esistenti.
- Esclusi: apertura dei file per collezione, WAL, aggiornamenti dei documenti,
  cursori, ricerca top-k e integrazione nello Store.
- Verifica: build di `Jigen.Index.Sparse`, test esistenti di
  `Jigen.Index.Sparse.Tests`, formatter e revisione dei limiti e delle lunghezze.
- Skill: quelle di SP-01; nessuna nuova capability.
- Handoff: utente per approvazione, poi SP-03AT Verificare la directory.
  Nessuna modifica ai test durante questa attività.
- Evidenze: [SP-03A Directory persistente dei termini](dossier.md#sp-03a-directory-persistente-dei-termini).

## SP-03AT Verificare la directory

- Sorgente: SP-03 Implementare Block-Max MaxScore.
- Attore: questa sessione. Stato: closed. Ordine: 3a-test.
- Dipendenza: SP-03A Directory persistente dei termini e approvazione del codice.
- Perimetro: `tests/Jigen.Index.Sparse.Tests`; nessuna modifica alla produzione.
- Verificare round trip su `StoredList`, limiti e confini leggibili senza aprire
  i blocchi, identificatori con prefissi comuni, intervalli adiacenti nello stesso
  documento, directory vuote, duplicati, sovrapposizioni e payload corrotti.
- Difetti: limiti persi alla riapertura, fusione di intervalli, alias di memoria,
  accesso anticipato ai posting e metadati invalidi accettati.
- Comando: `dotnet test tests/Jigen.Index.Sparse.Tests/Jigen.Index.Sparse.Tests.csproj`.
- Skill: quelle di SP-01; temporanei sotto `.temp`, da eliminare dopo il test.
- Evidenze: [SP-03AT Verificare la directory](dossier.md#sp-03at-verificare-la-directory).
  Tutti i 72 test del progetto passano; nessuna modifica alla produzione.
- Handoff: SP-03 Implementare Block-Max MaxScore quando tutti i test passano.

## SP-04 Verificare la ricerca

- Attore: questa sessione. Stato: closed. Ordine: 4.
- Dipendenza: SP-03 Implementare Block-Max MaxScore e approvazione del codice.
- Incremento: confronto con ricerca esaustiva sugli stessi pesi float e
  accumulo double per termine crescente; stesso ranking e insieme dei candidati.
- Perimetro: test sparse e dossier, senza modifiche alla produzione.
- Casi: corpus deterministici e generati con pesi firmati, zeri e valori estremi;
  blocchi disallineati e posizioni fisiche non ordinate; vettori multipli dello
  stesso documento anche su blocchi diversi; pareggi per chiave e k maggiore dei
  candidati; query vuota, termini assenti, k zero e cancellazione.
- Verificare lookup dopo riapertura, termini duplicati rifiutati e due collezioni
  con stessi identificatori ma dati separati. Dimostrare i salti usando riferimenti
  non leggibili in intervalli sotto soglia, senza introdurre hook di produzione.
- Verifica: suite sparse completa e formatter; nessuna soglia di copertura.
- Esito: 31 nuovi casi, 127 test totali superati in Debug e Release;
  3.150 confronti generati contro scansione esaustiva, oltre ai casi deterministici.
  Formatter verificato; nessuna modifica al codice di produzione.
- Handoff: utente per chiusura del motore. Store escluso dal perimetro corrente.

## SP-07 Ottimizzare il motore sparse

- Attore: questa sessione. Stato: code-ready. Ordine: 4-perf.
- Dipendenza: SP-04 chiuso. Richiesta diretta dell'utente di migliorare le prestazioni.
- Perimetro: `SparsePostingCursor.cs` e `SparseMaxScoreSearch.cs`; nessuna modifica
  al formato persistente, alle firme pubbliche o ai test.
- Baseline Release misurata: 3.000 documenti, 200 termini, 359.820 posting,
  5.727 blocchi da 64, query di 20 termini. Ricerca k=10: 16,6 ms e 2,87 MB
  allocati per query. Lettura sequenziale dell'intero corpus: 17,5 ms e 22,7 MB.
  Una query tocca circa un decimo dei blocchi: il costo e quindi dominato dalla
  deserializzazione dei blocchi e dalle ricerche binarie ripetute.
- Ipotesi da verificare una alla volta: percorsi rapidi nel cursore quando la
  destinazione cade nel blocco gia caricato o non supera il posting corrente;
  assenza di ricerca ripetuta a parita di posizione; riuso degli array nella
  selezione delle liste essenziali.
- Metodo: una modifica per volta, misura con lo stesso harness, regressioni.
- Esclusi: compressione o quantizzazione dei pesi, modifiche a `StoredList`,
  cache aggiuntive, modifiche al formato o all'API pubblica.
- Verifica: 127 regressioni esistenti, misure prima e dopo, formatter.
- Esito: query k=10 da 16,6 a 8,2 ms, allocazioni da 2.872 a 1.712 KB.
  Tre interventi misurati separatamente: percorsi rapidi nel cursore (16,1 -> 9,4),
  eliminazione delle copie ridondanti (2.872 -> 1.724 KB), accesso diretto agli
  array (9,0 -> 8,2). Il formato persistente e l'API pubblica restano invariati.
- Handoff: approvazione esplicita del codice prima di nuovi test.
  Ottimizzazioni ulteriori richiedono una decisione: cache dei blocchi,
  formato a colonne con copie in blocco, struttura dei cursori tipo WAND.

## SP-05A Modello sparso nel documento

- Attore: questa sessione. Stato: code-ready. Ordine: 4a.
- Dipendenza: decisione dell'utente del 24 ottobre 2026 sulla forma del dato.
- Decisione registrata: coppie termine/peso ordinate per termine crescente,
  conservate **nel documento**, che e la sola fonte autorevole. Le liste per
  termine restano un indice derivato, ricostruibile dai documenti.
- Incremento: `SparseTerm` come `readonly struct` con `[MessagePackObject]` e
  chiavi 0/1, e campo `SparseVectors` su `VectorEntry` e `VectorEntry<T>`.
  Il vettore in posizione i corrisponde a `Embeddings` in posizione i.
- Misura che ha guidato la forma: una struct con chiavi numeriche occupa gli
  stessi byte delle tuple (227/472/1.027 byte per 32/64/128 termini), mentre
  una struct senza attributi verrebbe salvata come mappa con nomi di campo
  (611/1.240/2.562 byte). Le coppie costano il 7-15% piu del dizionario ma
  scorrono 3-6 volte piu veloci per costruire l'indice.
- Verifica prevista: round trip e compatibilita con documenti privi del campo;
  regressioni esistenti di `Jigen.Primitives` e sparse.

## SP-05B Persistere le coppie nel record del documento

- Attore: questa sessione. Stato: code-ready. Ordine: 4b.
- Dipendenza: SP-05A e decisione dell'utente sul trattamento degli archivi
  esistenti: si aprono e restano solo densi.
- Incremento: la sezione sparsa `[numero vettori][conteggi][coppie]` chiude
  ogni record dei vettori dal formato 2; numero di versione nell'intestazione
  del file dei vettori; scrittura batch e singola, WAL con sezione opzionale,
  replay, compattazione, lettura `GetSparseVectors` e passaggio dalla
  `VectorCollection`. Un archivio con versione 0 resta denso e rifiuta
  scritture sparse con `NotSupportedException`.
- Non incluso: contabilita dei byte morti sparsi, comando di ricostruzione,
  indice per termine, REST, gRPC, client.
- Verifica prevista: 15 controlli funzionali su scrittura, rilettura,
  sovrascrittura, cancellazione, riapertura, compattazione e archivio legacy;
  regressioni Store, primitive e sparse.

## SP-05C Costruire l'indice di una collezione

- Attore: questa sessione. Stato: code-ready. Ordine: 4c.
- Dipendenza: SP-05B e il formato dei blocchi.
- Decisione tecnica emersa dal formato: i posting di un termine sono ordinati
  per chiave e indice vettore, quindi le scritture non possono essere accodate
  nell'ordine di arrivo. La costruzione avviene in un solo passaggio, dopo aver
  raccolto tutte le coppie; un aggiornamento o una cancellazione producono un
  nuovo indice invece di modificare quello esistente.
- Incremento: `SparseDocument`, `SparseIndexBuildSummary` e
  `SparseIndexBuilder` in `Jigen.Index.Sparse`. Scrittura in una directory
  nuova, rifiuto di sovrascrivere un indice esistente e rifiuto di una coppia
  documento/vettore/termine ripetuta.
- Non incluso: collegamento allo Store, comando di ricostruzione, gestione
  incrementale con segmenti e tombstone.
- Verifica prevista: confronto con la ricerca esaustiva su un corpus generato,
  riapertura dell'indice e casi di rifiuto.

## SP-05D Indicizzare durante l'invio

- Attore: questa sessione. Stato: code-ready. Ordine: 4d.
- Motivo: l'utente ha rifiutato il vincolo di indicizzare solo a invio concluso.
- Incremento: `SparseIndexWriter` accoda i posting dei documenti gia inviati,
  riempie l'ultimo blocco di ogni termine e ne aggiorna la voce di directory,
  con flush espliciti o automatici oltre `PendingLimit`. La ricerca funziona
  sui documenti gia scritti.
- Regola: i posting di un termine devono restare ordinati, quindi la chiave del
  documento deve crescere. Le chiavi generate dallo Store crescono nel tempo.
  Una chiave che non avanza produce `NotSupportedException`: la collezione va
  ricostruita con `SparseIndexBuilder` invece di corrompere l'ordine.
- Non incluso: gestione di chiavi fuori ordine senza ricostruzione, tombstone,
  compattazione dei segmenti, collegamento allo Store.
- Verifica prevista: ricerca durante l'invio confrontata con l'oracolo in piu
  punti, classifica finale, riapertura e rifiuto della chiave fuori ordine.

## SP-05E Indice a segmenti con tombstone

- Attore: questa sessione. Stato: code-ready. Ordine: 4e.
- Motivo: l'utente ha stabilito che le scritture possono arrivare in ordine
  arbitrario e che un documento puo essere aggiornato dopo essere stato scritto.
  Un unico indice ordinato per termine non lo consente.
- Struttura: `SparseSegment` (generazione + `SparseIndex` immutabile) e
  `SparseCollectionIndex` che interroga ogni segmento e unisce i risultati.
  `SparseTombstone` ritira una singola copia indicando la generazione del
  segmento che la contiene, quindi un aggiornamento nasconde la versione
  precedente senza toccare la nuova.
- Motore: `SparseIndex.Search` accetta un filtro di visibilita applicato prima
  di occupare un posto nel top-k, altrimenti un documento ritirato
  accorcerebbe i risultati.
- Esattezza: un documento vive in un solo segmento vivo, quindi i migliori k
  della collezione sono tra i migliori k di ogni segmento.
- SP-05D e superato: il vincolo di chiavi crescenti non esiste piu. Lo scrittore
  per lotti e il prossimo incremento.
- Non incluso: scrittore per lotti, manifest dei segmenti su disco,
  compattazione.
- Verifica prevista: tre segmenti con aggiornamenti e cancellazioni confrontati
  con l'oracolo.

## SP-05F Scrittore per lotti e persistenza della collezione

- Attore: questa sessione. Stato: code-ready. Ordine: 4f.
- Incremento: `SparseCollectionWriter` accumula i documenti e scrive un
  segmento per lotto; `Delete` registra un tombstone che copre i segmenti gia
  scritti e forza il commit di quanto in attesa, cosi non nasconde un documento
  appena accodato; `SparseCollectionManifest` elenca i segmenti e viene
  sostituito in modo atomico; `SparseCollectionStorage` riapre segmenti e
  tombstone e restituisce l'indice di collezione.
- Struttura su disco per collezione: `manifest`, `tombstones` con il suo
  `.index`, e `segments/g<generazione>/{terms,terms.index,blocks,blocks.index}`.
- Non incluso: compattazione dei segmenti, collegamento allo Store.
- Verifica prevista: lotti in ordine casuale, aggiornamenti, cancellazione,
  riapertura, nuovo segmento dopo la riapertura, confronto con l'oracolo.

## SP-05G Compattazione dei segmenti

- Attore: questa sessione. Stato: code-ready. Ordine: 4g.
- Incremento: `SparseCollectionCompactor` fonde tutti i segmenti vivi in uno
  nuovo, scartando le copie ritirate e azzerando i tombstone; il manifest viene
  riscritto con la sola nuova generazione e le directory dei vecchi segmenti
  vengono rimosse.
- Memoria: la fusione avviene termine per termine con cursori multipli, quindi
  proporzionale alla lista piu lunga, non all'intero corpus.
- Sicurezza: il segmento nuovo e completo prima che il manifest lo nomini; una
  directory non elencata viene ignorata e poi rimossa, quindi un'interruzione
  lascia uno stato valido.
- Non incluso: politica automatica di compattazione, collegamento allo Store.
- Verifica prevista: otto segmenti con aggiornamenti e cancellazioni, confronto
  con l'oracolo prima e dopo, manifest e directory coerenti, nuovo segmento
  dopo la compattazione.

## SP-05H Collegamento allo Store

- Attore: questa sessione. Stato: code-ready. Ordine: 4h.
- Incremento: `Store` mantiene uno scrittore e un lettore sparse per collezione
  sotto `{database}.sparse/`; il thread di scrittura indicizza il lotto appena
  persistito; cancellazione e svuotamento ritirano le copie sparse; lo Shrink
  dello Store compatta i segmenti; `Store.SearchSparse` e
  `VectorCollection.SearchSparse` espongono la ricerca.
- Sovrascrittura: prima di accodare la versione nuova, le copie precedenti
  vengono ritirate con un tombstone, altrimenti il documento comparirebbe due
  volte con pesi vecchi.
- Tombstone in memoria: lo scrittore espone la lista viva, perche un secondo
  handle sullo stesso file non vede i record non ancora scaricati.
- Non incluso: REST, gRPC, client; politica di compattazione fuori dallo Shrink.
- Verifica prevista: ricerca attraverso lo Store prima e dopo aggiornamento,
  cancellazione, riapertura e Shrink.

## SP-05 Integrare lo Store multivettore

- Attore: questa sessione. Stato: deferred. Ordine: 5.
- Motivo: l'utente ha limitato il completamento corrente al motore sparse.
- Dipendenza: SP-04 Verificare la ricerca.
- Incremento: scrittura, ricerca, riapertura, aggiornamento e cancellazione,
  con istanze, file e directory dell'indice separati per collezione.
- Da risolvere prima di ready: versioni dei documenti, WAL, directory persistenti,
  compattazione, lettura dei vettori e misure di apertura a cache fredda.
- REST, gRPC e client esclusi da questo giro.
- Verifica prevista: build e regressioni Store; nuovi test in SP-06.

## SP-06 Verificare il ciclo persistente

- Attore: questa sessione. Stato: deferred. Ordine: 6.
- Motivo: integrazione Store esclusa dal completamento corrente.
- Dipendenza: SP-05 Integrare lo Store multivettore e approvazione del codice.
- Incremento: test comportamentali di persistenza e recupero, misure di apertura,
  memoria e ricerca. Corpus e soglie da confermare prima di ready.
- Handoff finale: utente; evidenze nel dossier.
