# Development Dossier

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
