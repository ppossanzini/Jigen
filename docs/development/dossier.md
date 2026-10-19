# Multivector development

## Scope

- Piu embedding dense indipendenti condividono un metadato nella stessa collezione.
- Sparse vectors esclusi da questa implementazione.
- Primitive esistenti invariate: tipi, interfacce e formati. Approvata l'implementazione delle nuove primitive su disco con basso memory footprint.
- HNSW invariato per nodi, file del grafo e navigazione.
- Una modifica HNSW e ammessa soltanto nella lettura del metadato, se necessaria.
- La persistenza delle associazioni resta responsabilita di Jigen.Primitives.
- Nessuna nuova gestione di file, formato storage, writer o mapping in Jigen.Store.
- I dati delle associazioni non sono caricati integralmente in un dizionario managed.
- StoredList mantiene il proprio indice delle posizioni e la propria cache secondo l'implementazione esistente.

## Architecture gate

Composizione minima: SharedMetadataStore in Jigen.Store.

- StoredList<T,TOptions> gestisce i file tramite StoreListOptions.
- IStorableItem<T,TOptions> definisce Serialize e Deserialize per i record applicativi, senza modificare Jigen.Primitives.
- Le due strutture risolvono embedding -> metadato e metadato -> contenuto.
- StoredList usa RandomAccess.Read, un indice delle posizioni in RAM e una cache limitata. Non usa memory mapping nella versione esaminata.
- Il formato delle liste e il lifecycle dei file restano quelli delle primitive esistenti.
- L'integrazione deve rendere il metadato disponibile durante il filtro senza cambiare IStore.
- SharedMetadataContent conserva il contenuto. SharedMetadataAssociation conserva collezione, ID embedding e posizione del metadato.
- Il lookup della composizione usa B+ tree per l'associazione e array per la versione corrente. Store.GetContent legge dalla composizione; nessun fallback al payload legacy.
- Le posizioni metadato restano stabili: questo incremento non rimuove elementi dalle liste.
- I commit della composizione seguono Commit and recovery specification. Nessuna transazione atomica tra file.
- Mutazioni approvate: append-only, cancellazioni logiche, metadati senza embedding conservati.
- UpdateMetadata aggiunge un contenuto e un record MetadataUpdated dopo il Flush dei contenuti. L'identita metadato resta stabile.
- DeleteEmbedding aggiunge un record EmbeddingDeleted. Una nuova associazione successiva puo riattivare l'ID.
- La riapertura ricostruisce gli indici derivati e lascia invariate le sorgenti recuperate.
- Il lock superiore serializza le operazioni di una sola istanza. Aperture concorrenti da altre istanze o processi non sono supportate da questa composizione.
- Le mutazioni persistono le sorgenti prima di pubblicare gli indici e ritornare. Il Flush periodico delle primitive non passa dal lock superiore.

## Disk lookup design

- Requisito approvato: indice completo su disco, senza copia completa managed di chiavi, hash o posizioni.
- Scelta approvata dall'utente: B+ tree e array persistente ad accesso diretto, entrambi su disco con accesso MMF file-backed.
- Specifica di formato, commit e recovery approvata per implementazione. Garanzie delle nuove primitive subordinate alla verifica.
- B+ tree generico: chiave binaria -> riferimento scalare. Uso Store: collezione/embedding -> ultima associazione.
- Array persistente generico: ID numerico stabile -> riferimento scalare. Uso Store: metadato -> ultima versione del contenuto. Gli ID devono restare sufficientemente densi e non essere rinumerati alla cancellazione.
- Nessuna conoscenza di embedding o metadati nelle primitive; nessuna costruzione che richieda tutte le chiavi in RAM.
- Indirizzi delle pagine calcolati dal numero pagina; nessuna lista managed delle posizioni. Viste MMF e buffer di scrittura con budget configurabile.
- Le pagine mappate possono aumentare RSS e page cache. MMF non garantisce un limite alla RAM fisica totale.
- Il B+ tree confronta chiavi complete senza dipendere dall'unicita di un hash.
- Recovery B+ tree approvato come meccanismo: pagine copy-on-write, pubblicazione della radice dopo persistenza delle nuove pagine, due record radice con generazione e checksum.
- Recovery array approvato come meccanismo: due copie per slot con generazione e checksum. L'ultima copia confermata resta disponibile fino al completamento dell'aggiornamento.
- Il protocollo deve impedire che aggiornamenti consecutivi sovrascrivano entrambe le copie prima della conferma. Checksum e Flush MMF non costituiscono una transazione.
- Concorrenza approvata: un writer, lettori concorrenti nella stessa istanza. Crescita e remapping devono rispettare la durata delle letture attive.
- Nessun riuso delle pagine obsolete nella prima versione proposta.
- L'indice e derivato dai record sorgente. Ordine di persistenza, identificazione della sorgente, watermark e replay devono impedire riferimenti a record non recuperati.
- Parametri di formato e memoria in Primitive format. Nessun riuso delle pagine obsolete nella prima versione.
- Il recovery separato delle primitive non garantisce atomicita tra file o coerenza con i record sorgente.
- Verifiche richieste: lookup contro scansione di riferimento, split e aggiornamenti, cancellazione logica, riapertura, fault injection sul commit, crescita con lettori e misura heap/RSS a dataset crescente.
- Limite della composizione corrente: gli indici managed delle StoredList restano proporzionali ai record. Il nuovo lookup da solo non rende limitata la RAM dell'intero store; anche la lettura posizionale delle liste richiede una valutazione separata.

## Commit and recovery specification

- D1: protocollo logico approvato, inclusa ricostruzione completa alla riapertura. Ambito: nuove primitive e coordinamento logico della composizione. Skill: capabilities-dev-problem-solving-methodology e base-dossier-writing.
- I record sorgente restano autoritativi. Gli indici sono derivati e ricostruibili. Nessun commit distribuito tra liste, B+ tree e array.
- Ordine di mutazione: append contenuto se necessario, Flush contenuti, append associazione o mutazione, Flush associazioni, aggiornamento degli indici. Nessun indice pubblica un riferimento prima della persistenza della sorgente.
- AddMetadata richiede il Flush del contenuto prima della pubblicazione nello slot. I metadati senza embedding restano recuperabili.
- B+ tree: scrivere nuove pagine e checksum senza modificare le pagine raggiungibili dalla radice confermata. Persistire le pagine prima di scrivere il record radice inattivo. Persistire il record radice prima di confermare il commit e riutilizzare il record precedente.
- Array: scrivere valore, stato, generazione e checksum nella copia inattiva dello slot. Persistire la copia prima di confermare l'aggiornamento. Ogni aggiornamento dello stesso slot completa questa sequenza prima del successivo; nessun batch sovrascrive entrambe le copie.
- Il checksum include identita del record e generazione. Un valore zero non indica assenza; lo stato dello slot distingue assenza, valore e cancellazione logica.
- Alla riapertura delle primitive si sceglie la generazione valida piu recente. Una scrittura non confermata puo essere recuperata se completa e valida. Due copie invalide di un record inizializzato segnalano corruzione, non assenza.
- Le copie devono avere collocazione e ordine di scrittura che non espongano entrambe allo stesso aggiornamento parziale. La doppia copia da sola non dimostra resistenza a perdita di alimentazione o danni del dispositivo.
- Prima versione della composizione: ricostruire entrambi gli indici prima di servire letture, usando solo i record recuperati dalle primitive sorgente. Non fidarsi di indici conservati da una precedente apertura.
- Il replay e sequenziale e usa buffer limitati. Applica le associazioni e gli aggiornamenti in ordine di append. Una cancellazione seguita da associazione riattiva l'embedding.
- Ogni riferimento a contenuto deve essere entro i record recuperati. Un riferimento invalido interrompe l'apertura con errore; nessuna correzione silenziosa della sorgente.
- Un errore di commit impedisce ulteriori mutazioni nella stessa istanza. La riapertura recupera le sorgenti e ricostruisce gli indici. La ricostruzione interrotta riparte; nessun lookup usa un indice parziale.
- I lettori osservano uno stato coerente tramite sincronizzazione nella stessa istanza. Nessun lettore conserva una vista che il writer chiude durante crescita o remapping.
- Costo approvato: scansione completa alla riapertura e commit sincrono per aggiornamento dello slot. Watermark e replay incrementale restano fuori dalla prima versione.
- Verifica discriminante futura: interrompere dopo ogni fase di persistenza, riaprire e confrontare il lookup con la scansione dei record recuperati. Ripetere con due aggiornamenti consecutivi dello stesso slot, split della radice e cancellazione/riassociazione.
- Stato di verifica: protocollo delle nuove primitive implementato; evidenze e limiti in L1-T e L2-T. Coordinamento degli indici nello Store non implementato.

## Primitive format

- D2: parametri approvati dall'utente per implementazione. Primitive generiche in Jigen.Primitives; primitive e formati esistenti invariati.
- Formato little-endian, versione 1. Valori scalari long a 64 bit. ID array long non negativi; aritmetica degli offset checked.
- B+ tree: pagine di 4096 byte, header di 64 byte, chiavi binarie da 1 a 512 byte con confronto lessicografico dei byte. Chiavi piu lunghe rifiutate prima di modificare il file.
- Pagine con directory degli slot, lunghezza delle chiavi e riferimenti espliciti. Split determinato dai byte occupati, non dal solo numero di chiavi. Nessun overflow delle chiavi nella prima versione.
- Due record radice in pagine distinte di 4096 byte. Record con versione, generazione, radice, limite delle pagine confermate e checksum. Pagine dati append-only; nessun riuso nella prima versione.
- Array: copia di 32 byte con ID (8), generazione (8), valore (8), stato (4) e checksum (4). Blocchi di 128 slot; le due copie occupano due pagine distinte di 4096 byte.
- Array con header duplicato in due pagine distinte. La capacita confermata viene pubblicata solo dopo l'inizializzazione e persistenza dei nuovi blocchi. Un blocco incompleto fuori capacita confermata non e leggibile.
- Checksum CRC32 per rilevare scritture parziali e corruzione accidentale, non per autenticazione o garanzia assoluta contro corruzione.
- Budget per istanza proposto: massimo 4 viste MMF da 1 MiB e massimo 1 MiB di buffer managed operativi. Il budget esclude oggetti del runtime, dati restituiti al chiamante e page cache del sistema operativo.
- Le opzioni centralizzano percorso, budget viste e buffer. Page size, slot size e limite chiavi appartengono al formato; una riapertura verifica la versione invece di reinterpretare il file con nuove opzioni.
- Crescita sotto esclusione del writer e delle letture attive durante il remapping. Nessun riferimento a memoria mappata esce dalla primitiva.
- Contratto minimo proposto: TryGet, Set, Delete logico e Dispose. Set/Delete confermano il proprio commit prima del ritorno; nessun buffer di mutazioni proporzionale al dataset.
- Store codifica la chiave composta con lunghezze esplicite e byte della collezione e dell'ID embedding. La codifica deve rispettare il limite complessivo di 512 byte; nessun hash sostituisce la chiave completa.
- Verifica richiesta prima del rilascio: formato e offset, chiavi al limite, split con chiavi variabili, crescita, riapertura, fault injection e budget a dataset crescente. Nessuna garanzia ancora verificata su queste primitive.

## Lookup experiment

- L0: closed come esperimento esplorativo autorizzato dall'utente. Non dimostra le garanzie delle primitive definitive.
- File: tests/JigenBenchmarks/DiskLookupExperiment.cs e ingresso disk-lookup in tests/JigenBenchmarks/Program.cs. Produzione invariata.
- Skill: phase-development-task-execution, base-be-base-rules, implementation-be-dotnet-dev, capabilities-dev-problem-solving-methodology, base-dossier-writing. La capability guida isolamento dei processi, warm-up e verifiche discriminanti.
- Strutture sperimentali MMF: B+ tree bulk-built, hash a capacita fissa con linear probing, array per ID numerici densi. Chiavi e valori di 8 byte; nessuna crescita dinamica o transazione.
- Verifica passata: ogni chiave iniziale, assenza, aggiornamenti e riapertura con parametri letti dall'header persistito.
- Misure: 10000, 100000 e 1000000 record; tre processi per struttura/dimensione; 20000 campioni per carico. Lookup uniforme, assenza, 80% delle letture sull'1% delle chiavi e aggiornamenti.
- Cache file calda dopo verifica completa; singolo thread; warm-up esplicito. Probes logici non equivalgono a I/O fisico. Mediane tra ripetizioni, non intervalli di confidenza.
- A 1000000 record: p95 lookup presente B+ tree 862 ns, hash 289 ns, array 206 ns. File rispettivamente 15.39, 32.00 e 15.26 MiB. RSS del processo rispettivamente 61.4, 77.4 e 60.4 MiB.
- Heap incrementale misurato circa 4 KiB, incluso il supporto di misura e dopo GC; non rappresenta memoria totale o picco di costruzione. Costruzione B+ tree con ordinamento in RAM; allocazioni di costruzione registrate separatamente.
- Risultati: .temp/disk-lookup-experiment/01a130756a18728ba810604049d9c461/results.json. File dati dei worker rimossi; misure grezze conservate.
- Per la verifica delle strutture scelte restano crescita/split, chiavi composite reali, carico concorrente, memoria limitata/cache fredda e garanzie di durabilita/recovery.

## Compatibility

- Nuova versione come unico percorso operativo dello Store, senza opt-in o fallback legacy.
- Migrazione automatica dei database legacy al primo avvio, prima di servire letture o scritture.
- La prima conversione conserva gli ID: un metadato per vecchia entry.
- Embedding, collezioni e contenuti conservati nella conversione.
- Nessun raggruppamento automatico di contenuti uguali.
- File v2 separati dai nomi legacy. Vecchi eseguibili non supportati sulla destinazione v2.
- Sorgente legacy conservata fino a conversione completa e verifica dei dati. Attivazione v2 solo dopo verifica riuscita.
- Rimozione automatica dei soli file sorgente legacy dopo attivazione verificata. Nessuna copia legacy permanente o doppio writer.
- Migrazione interrotta riprendibile o riavviabile senza perdita della sorgente. Dopo attivazione v2 nessun ritorno automatico al legacy.
- Una rimozione interrotta non riavvia la migrazione e non cancella dati v2. Protocollo nelle nuove primitive: Generation activation. Integrazione e verifiche richieste prima del rilascio.
- Un export verso v1 duplica il contenuto per embedding e perde l'identita condivisa.
- Il formato del grafo HNSW resta invariato. Il suo riuso richiede verifica delle identita e dei dati di origine.

## Task breakdown

| ID | Task | Dipendenze | Criterio di verifica | Esclusioni |
| --- | --- | --- | --- | --- |
| M0 | Fissa la composizione delle primitive esistenti | Nessuna | Due strutture di lettura definite; nessuna modifica Primitives; nessuna nuova persistenza Store | Nuovo formato storage |
| M1 | Implementa un percorso persistente minimo | M0 | Un metadato con due embedding si legge dopo chiusura e riapertura; build Store riuscita | Mutazioni concorrenti, conversione legacy |
| M1-T | Verifica il round-trip delle liste | Implementazione M1, approvazione esplicita della qualita del codice | Test di riapertura, assenza e isolamento delle collezioni passati | Altre funzionalita |
| M2 | Implementa mutazioni append-only | M1-T | Build; regressioni round-trip passate | Ricerca raggruppata, atomicita dopo crash |
| M2-T | Verifica mutazioni e limiti del recovery | Implementazione M2, approvazione codice | Versioni condivise, cancellazioni, riassociazione, concorrenza nella stessa istanza, terminazioni prima/dopo Flush e code incomplete simulate verificate | Prestazioni, riparazione delle primitive, terminazione osservata dentro Flush |
| L1 | Implementa array scalare persistente | D1 e D2 approvati | Build Primitives | B+ tree, integrazione Store, nuovi test |
| L1-T | Verifica array e recovery | L1, autorizzazione al task test | Lookup, riapertura, crescita, concorrenza, code danneggiate, process kill e memoria verificate | Terminazione osservata in ogni fase interna, perdita di alimentazione |
| L2 | Implementa B+ tree persistente | D1, D2 e L1-T | Build Primitives e regressioni array | Integrazione Store, nuovi test |
| L2-T | Verifica B+ tree | L2, approvazione codice | Confronto con riferimento, chiavi variabili, split multilivello, aggiornamenti, cancellazioni, riapertura, recovery e memoria | Integrazione HNSW |
| L3 | Implementa apertura per ricostruzione | Autorizzazione utente, L1-T e L2-T | Build e regressioni array/B+ tree | Integrazione Store, primitive esistenti, nuovi test |
| L3-T | Verifica apertura per ricostruzione | L3, approvazione codice | Ricreazione vuota di file validi e corrotti; nuovi commit riapribili; apertura normale conservativa; opzioni invalide senza troncamento | Replay Store, perdita di alimentazione |
| L4 | Implementa attivazione delle generazioni | Autorizzazione utente | Build Primitives; primitive esistenti invariate | Integrazione Store, nuovi test |
| L4-T | Verifica attivazione e cleanup | L4, approvazione codice | Verifica fallita conserva sorgente; pubblicazione riapribile; cleanup interrotto riprendibile; destinazione invalida impedisce cleanup | Perdita di alimentazione |
| M3 | Integra la lettura del contenuto condiviso | M2-T, L3-T, lookup su disco progettato e verificato | Test HNSW selettivo esistente passato; primitive esistenti invariate | Algoritmo e formato HNSW |
| M3-T | Verifica filtri sui metadati condivisi | M3, approvazione codice | Filtri corretti dopo aggiornamento e riapertura | Aggregazione |
| M4 | Implementa risultati per metadato nello Store | M3-T | Build; regressioni ricerca passate | Modifiche alla navigazione HNSW |
| M4-T | Verifica ranking raggruppato | M4, approvazione codice | Confronto brute force; gruppi sbilanciati; budget esaurito | Promessa di top-k esatto ANN |
| M5 | Integra la compattazione tramite primitive | M2-T, L4-T | Identita delle associazioni conservata; nessun nuovo writer Store | Conversione legacy |
| M5-T | Verifica compattazione e backup | M5, approvazione codice | Associazioni conservate; backup riapribile | Migrazione |
| M6 | Implementa migrazione automatica al primo avvio | M3-T, L4-T | Build; conversione e verifica precedono attivazione; sorgente conservata fino alla verifica e poi rimossa | Deduplicazione implicita, doppio percorso operativo |
| M6-T | Verifica compatibilita e lifecycle della migrazione | M6, approvazione codice | Fixture precedente: ID, collezioni, contenuti ed embedding conservati; interruzioni di conversione, attivazione e cleanup; riapertura v2 senza nuova conversione | Perdita di alimentazione |

M2-M6 richiedono la definizione dei contratti mancanti senza nuovi file writer, WAL o motori di compattazione in Jigen.Store.
Le regole di migrazione sono requisiti da verificare sulla composizione, non autorizzano un nuovo motore storage.
Le nuove API server/client richiedono un task separato con contratti approvati.
M4: punteggio massimo osservato degli embedding per metadato e ampliamento candidati fino ai gruppi richiesti o al budget esplicito approvati. API separata SearchByMetadata con candidateBudget obbligatorio per query; Search invariata. Nessuna promessa di top-k esatto ANN.
Le nuove verifiche sono task separati dalle modifiche di produzione.
La nuova versione dello Store non e rilasciabile prima di M6-T; M3-T da solo non verifica l'apertura di database legacy.

## NuGet impact

| Pacchetto pubblicato dalla CI | Impatto |
| --- | --- |
| Jigen.Store | Eventuale logica di integrazione e aggregazione; nessuna nuova persistenza |
| Jigen.Indexer.HNSW | Solo lettura metadato, se GetContent non basta |
| Jigen.Primitives | Array e B+ tree su disco implementati; primitive esistenti invariate |
| Jigen.Client | Differito al task dei contratti remoti |
| Jigen.SemanticTools | Nessuna modifica funzionale |
| Jigen.Calibration | Nessuna modifica funzionale |

La CI pubblica tutti e sei con la versione del tag. Pubblicazione non significa modifica funzionale.

## Task execution

- L3: closed. Qualita del codice approvata dall'utente. Apertura esplicita per ricostruzione implementata tramite DiskIndexOptions.Recreate. Nessuna modifica a Store, HNSW, primitive esistenti o test nel task di produzione.
- Verifica L3: compilazione riuscita e ventidue regressioni array/B+ tree passate. Cinque warning xUnit2017 preesistenti in TestStoredListLookup; nessun nuovo warning. Comportamento della modalita Recreate da verificare in L3-T.
- File L3: Jigen.Primitives/Persistance/DiskIndexOptions.cs, Jigen.Primitives/Persistance/MappedIndexFile.cs e questo dossier. Formati delle nuove primitive invariati.
- Skill L3: phase-development-task-execution, phase-development-configuration-options, base-be-base-rules, implementation-be-dotnet-dev, capabilities-dev-problem-solving-methodology, run-tests, base-dossier-writing. Le capability guidano il controllo dell'apertura prima del mapping e le regressioni mirate.
- L3-T: closed. Dieci nuovi casi xUnit mirati passati sulle due primitive. Nessuna modifica alla produzione.
- Verificati L3-T: file validi conservati nell'apertura normale; file corrotti troncati a 19 byte rifiutati senza modifica dei byte. Recreate elimina le vecchie entry e ripristina la dimensione iniziale; nuovo commit leggibile dopo apertura normale.
- Verificati L3-T: ViewSize nullo, non multiplo e oltre il massimo rifiutati prima del troncamento; confronto completo dei byte e valore precedente leggibile dopo riapertura.
- File L3-T: tests/PrimitiveTests/TestPersistentScalarArray.cs, tests/PrimitiveTests/TestPersistentBTree.cs e questo dossier. Riutilizzate le directory temporanee dei singoli casi; sottoalberi rimossi.
- Skill L3-T: phase-development-task-execution, base-be-base-rules, implementation-be-dotnet-dev, capabilities-dev-problem-solving-methodology, run-tests, base-dossier-writing. Le capability guidano il confronto prima/dopo ricreazione e il runner xUnit filtrato.
- Warning L3-T: cinque xUnit2017 preesistenti in TestStoredListLookup; nessun nuovo warning. Replay coordinato e interruzioni della ricostruzione nello Store fuori dal task.
- L1: passaggio al task test autorizzato dal comando diretto dell'utente. PersistentScalarArray implementato con TryGet, Set, Delete e Dispose. B+ tree e integrazione Store fuori dal task.
- Verifica L1: build Primitives riuscita senza warning o errori. Verifiche comportamentali in L1-T.
- File L1: Persistance/DiskIndexOptions.cs, Persistance/MappedIndexFile.cs, Persistance/PersistentScalarArray.cs in Jigen.Primitives e questo dossier. Primitive esistenti e test invariati.
- Skill L1: phase-development-task-execution, phase-development-configuration-options, base-be-base-rules, implementation-be-dotnet-dev, capabilities-dev-problem-solving-methodology, base-dossier-writing. La capability guida il controllo mirato di MMF, span e checksum tramite compilazione.
- Routing backend: td-backend-dev indicata dall'istruzione locale non disponibile. Applicato il routing .NET dichiarato dalla governance di progetto, come per M1-M2.
- L1-T: closed. Dodici casi passati; criterio alternativo di recovery approvato dall'utente. Nessuna modifica alla produzione.
- Verificati L1-T: assenza distinta da valore zero, estremi long, aggiornamenti, cancellazione e riattivazione dopo riapertura, confini dei blocchi e crescita oltre viste da 64 KiB.
- Verificati L1-T: lettori concorrenti durante crescita, input negativi e overflow senza modifica della lunghezza, Dispose ripetuto e rifiuto delle operazioni dopo Dispose.
- Recovery deterministico L1-T: copia slot piu recente danneggiata dopo due aggiornamenti; precedente leggibile e nuovo commit riapribile. Entrambe le copie danneggiate segnalano corruzione; mutazione fallita rende l'istanza inutilizzabile.
- Recovery crescita L1-T: header piu recente danneggiato o ultimo blocco troncato; recupero capacita precedente e crescita successiva riapribile. Entrambi gli header danneggiati impediscono l'apertura.
- File L1-T: tests/PrimitiveTests/TestPersistentScalarArray.cs e questo dossier. File temporanei in .temp/persistent-array-tests; sottoalbero del singolo caso rimosso.
- Skill L1-T: phase-development-task-execution, base-be-base-rules, implementation-be-dotnet-dev, capabilities-dev-problem-solving-methodology, run-tests, base-dossier-writing. Le capability guidano simulazioni discriminanti e runner xUnit filtrato.
- Warning L1-T: cinque xUnit2017 in TestStoredListLookup preesistente. Nessun warning nel nuovo file.
- Memoria L1-T: capacita di 10000, 100000 e 1000000 slot, vista da 65536 byte, inizializzazione dei blocchi e 1000 letture distribuite. Due slot valorizzati per dataset; nessun milione di commit individuali misurato.
- Heap trattenuto dopo GC: delta -24, 32 e 289040 byte nella verifica mirata. RSS del processo xUnit circa 84, 86 e 89 MiB. Misure nello stesso processo, includono runtime e runner; nessuna misura della page cache o prova sotto pressione di memoria.
- Terminazioni reali L1-T: tre processi figli dopo un commit confermato, durante aggiornamenti ripetuti dello stesso slot. Versione recuperata non precedente alla confermata, slot vicino conservato e nuova mutazione riuscita.
- Il punto interno raggiunto al momento della terminazione non e osservato. Simulazioni deterministiche e terminazioni reali soddisfano il criterio approvato per L1-T.
- Le verifiche non dimostrano atomicita generale o resistenza a perdita di alimentazione.
- L2: closed. Qualita del codice approvata dall'utente. PersistentBTree implementato con TryGet, Set, Delete logico e Dispose; chiavi variabili, split per byte e copy-on-write del percorso aggiornato.
- Verifica L2: build Primitives senza warning o errori; dodici regressioni array passate. Verifiche comportamentali in L2-T.
- File L2: Jigen.Primitives/Persistance/PersistentBTree.cs e questo dossier. MappedIndexFile riutilizzato senza modifiche; primitive esistenti, Store, HNSW e test invariati.
- Skill L2: phase-development-task-execution, phase-development-configuration-options, base-be-base-rules, implementation-be-dotnet-dev, capabilities-dev-problem-solving-methodology, run-tests, base-dossier-writing. Le capability guidano compilazione mirata e regressione xUnit.
- La riapertura verifica il record radice e la pagina radice. Le pagine discendenti vengono verificate durante il percorso di lettura; nessuna scansione completa del grafo alla riapertura.
- L2-T: closed. Dieci casi xUnit mirati passati. Nessuna modifica alla produzione.
- Verificati L2-T: chiavi binarie, prefissi, valore zero, lunghezze da 1 a 512 byte e copia del buffer chiamante. Confronto con riferimento su 1200 chiavi inserite fuori ordine, aggiornate, cancellate e riattivate; split multilivello e riapertura verificati.
- Recovery L2-T: record radice danneggiato, pagina radice danneggiata e coda troncata recuperano la versione precedente; nuovo commit leggibile dopo riapertura. Entrambi i record radice danneggiati impediscono l'apertura senza modificare il file.
- Corruzione discendenti L2-T: foglia danneggiata segnalata durante lettura; ramo sano leggibile. Mutazione fallita richiede riapertura dell'istanza.
- Verificati L2-T: due lettori durante crescita e split, chiavi fuori limite senza crescita del file, Dispose ripetuto e operazioni rifiutate dopo Dispose.
- Memoria L2-T: 128, 1024 e 4096 chiavi effettivamente inserite con vista da 65536 byte. Delta heap trattenuto dopo GC rispettivamente -104, 21808 e -85984 byte; RSS del processo xUnit circa 109, 114 e 114 MiB. Misure nello stesso processo, includono runtime e runner; delta negativi includono oggetti preesistenti liberati.
- File dati L2-T: 782336, 12304384 e 64364544 byte ai tre conteggi. La crescita comprende pagine obsolete copy-on-write non riutilizzate; nessuna misura di compattazione o throughput.
- Limiti L2-T: recovery deterministico, nessuna terminazione reale del processo B+ tree, perdita di alimentazione, cache fredda o pressione di memoria. Nessuna misura della page cache o garanzia sulla RAM totale.
- File L2-T: tests/PrimitiveTests/TestPersistentBTree.cs e questo dossier. Sottoalberi temporanei dei singoli casi in .temp/persistent-tree-tests rimossi.
- Skill L2-T: phase-development-task-execution, base-be-base-rules, implementation-be-dotnet-dev, capabilities-dev-problem-solving-methodology, run-tests, base-dossier-writing. Le capability guidano confronto discriminante, corruzione controllata e runner xUnit filtrato.
- Warning L2-T: cinque xUnit2017 nel TestStoredListLookup preesistente. Nessun warning nei nuovi test.
- M0: closed per la composizione minima. Proprietario scelto dall'utente: Jigen.Store.
- M1: closed. Composizione implementata; build Store riuscita senza warning. Passaggio ai test autorizzato dall'utente.
- File di produzione: SharedMetadataStore.cs, DataStructures/SharedMetadataContent.cs, DataStructures/SharedMetadataAssociation.cs.
- Operazioni: AddMetadata, AssociateEmbedding, GetContent, TryGetContent, Flush, DisposeAsync.
- Letture assenti restituiscono null o false. Nessun dizionario delle associazioni e caricato integralmente.
- Nessun cambiamento a Primitives, HNSW, dati legacy, contratti server o client.
- Skill attivate per il piano: analysis-piano-task, base-dossier-writing, phase-development-task-execution, capabilities-dev-problem-solving-methodology.
- Gate produzione: base-be-base-rules e implementation-be-dotnet-dev. La skill implementation-be-dotnet-jigen-store descrive consumatori di Jigen con dati relazionali autorevoli; non determina il formato interno di questa libreria.
- Skill produzione attivate: phase-development-task-execution, base-be-base-rules, implementation-be-dotnet-dev, capabilities-dev-problem-solving-methodology. La capability guida la verifica del nuovo contratto di serializzazione con la build mirata.
- Evidenza disponibile: il test HNSW sul filtro selettivo e passato nella verifica precedente. Non prova la nuova composizione.
- Verifica corrente: letti StoredList, IStorableItem e StoreListOptions. Disponibili operazioni di persistenza senza estendere le primitive.
- M1-T: closed. Cinque casi passati: riapertura con e senza Flush esplicito, isolamento delle collezioni, assenza e contenuto vuoto, isolamento dei buffer del chiamante.
- File del task M1-T: tests/JigenStoreTests/SharedMetadataStoreTests.cs e questo dossier. Produzione invariata.
- Skill M1-T: phase-development-task-execution, base-be-base-rules, implementation-be-dotnet-dev, capabilities-dev-problem-solving-methodology, run-tests, base-dossier-writing. Le capability guidano i casi discriminanti e l'esecuzione xUnit filtrata.
- Dati temporanei in .temp/shared-metadata-tests; directory di ogni caso rimossa alla fine; .temp esclusa da Git.
- Warning della verifica: quattro in dipendenze e test preesistenti. Nessun warning nei nuovi test.
- M2: closed. Codice approvato per il passaggio ai test; UpdateMetadata e DeleteEmbedding implementati; regressioni round-trip passate.
- File M2: SharedMetadataStore.cs, DataStructures/SharedMetadataAssociation.cs, DataStructures/SharedMetadataMutationKind.cs e questo dossier. Nessun test modificato.
- Skill M2: phase-development-task-execution, base-be-base-rules, implementation-be-dotnet-dev, capabilities-dev-problem-solving-methodology, run-tests, base-dossier-writing. Le capability guidano la verifica mirata del cambiamento dei record.
- M2-T: closed. Dodici test mirati passati, incluse le cinque regressioni M1-T. Criterio delle simulazioni deterministiche approvato dall'utente.
- Verificati: aggiornamenti condivisi e isolamento degli altri metadati; cancellazione, assenza e riassociazione dopo riapertura; 24 writer concorrenti nella stessa istanza.
- Terminazioni reali di processi figli prima e dopo il Flush finale passate. Prima del Flush resta leggibile la versione precedente; dopo il Flush e leggibile la versione aggiornata.
- Code incomplete simulate: ultimo record dati troncato e ultima entry indice troncata. Recupero della versione precedente e successivo aggiornamento dopo una seconda riapertura passati.
- StoredList.ReadIndex esclude riferimenti fuori dal file dati e limita il conteggio alle entry indice complete. Primitives invariato.
- File M2-T: tests/JigenStoreTests/SharedMetadataStoreTests.cs e questo dossier. Nessuna modifica alla produzione.
- Skill M2-T: phase-development-task-execution, base-be-base-rules, implementation-be-dotnet-dev, capabilities-dev-problem-solving-methodology, run-tests, base-dossier-writing. Le capability guidano i casi di mutazione, il processo isolato e l'esecuzione filtrata.
- Warning: tre in test preesistenti. Nessun nuovo warning.
- Limiti M2-T: terminazione nell'esatto punto di scrittura interno non osservata; nessuna atomicita generale dopo crash dimostrata.
- M3: closed. Qualita del codice completo approvata esplicitamente dall'utente. Store.GetContent collegato a SharedMetadataStore; lifecycle gestito dallo Store, senza opt-in. StoreOptions, HNSW, primitive e test invariati nel task di produzione.
- Verifica M3: compilazione riuscita; diciannove casi SharedMetadataStoreTests, VectorCollectionTests e DocumentCollectionSearchTests passati. Regressione HNSW Search_WithSelectiveFilter_FindsMatchesOutsideTheNearestEfWindow passata sul nuovo percorso dei contenuti.
- Implementato M3: ricostruzione completa all'apertura, lookup delle associazioni e delle versioni senza scansioni, commit sorgenti prima degli indici e rifiuto delle operazioni dopo un commit fallito. Gli ID dei record di versione non sono accettati come metadati dopo replay.
- Implementato M3: writer batch e singolo pubblicano il contenuto nella composizione senza scriverne una seconda copia nel vecchio file. Indice Store con riferimento metadato positivo pari a ID piu uno; formato degli embedding e HNSW invariati. Cancellazioni e clear pubblicano tombstone delle associazioni.
- File M3: SharedMetadataStore.cs, Store.cs, StoreRead.cs, StoreShrink.cs e Extensions/StoreWritingExtensions.cs in src/Jigen/Jigen.Store, piu questo dossier. Tre warning xUnit2013 preesistenti nei test Store; due MSB4011 e due xUnit1031 preesistenti nel progetto HNSW. Nessun nuovo warning.
- Limiti M3: compatibilita legacy prosegue in M6; nuova versione non rilasciabile prima dei gate successivi.
- Skill M3: phase-development-task-execution, phase-development-configuration-options, phase-development-deviation-routing, base-be-base-rules, implementation-be-dotnet-dev, capabilities-dev-problem-solving-methodology, run-tests, base-dossier-writing. Le capability guidano la regressione immediata e la distinzione fra aspettativa precedente e commit approvato.
- M3-R-T: closed. Aggiornamento del test di crash autorizzato dall'utente dopo approvazione della composizione. ProcessTermination_RecoversCompletedMutation_WithAndWithoutExplicitFlush recupera l'aggiornamento completato in entrambi i processi terminati, con e senza Flush aggiuntivo. Dodici casi della suite passati.
- File M3-R-T: tests/JigenStoreTests/SharedMetadataStoreTests.cs e questo dossier. Produzione invariata. Tre warning xUnit2013 preesistenti; nessun nuovo warning. Directory temporanee dei singoli casi rimosse.
- Skill M3-R-T: phase-development-task-execution, base-be-base-rules, implementation-be-dotnet-dev, capabilities-dev-problem-solving-methodology, run-tests, base-dossier-writing. Le capability guidano il confronto del contratto di commit e l'esecuzione filtrata con processi figli.
- Limiti M3-R-T: terminazione successiva al ritorno della mutazione, non durante il commit. Nessuna atomicita tra file o resistenza a perdita di alimentazione dimostrata.
- M3-T: closed. Cinque casi FilteredSearchTest passati: quattro nuovi casi e la regressione selettiva esistente. Produzione invariata.
- Verificati M3-T: due embedding condivisi leggono l'aggiornamento pubblicato dal writer Store; il metadato indipendente resta invariato. Filtri e contenuti corretti dopo riapertura con entrambi gli indici derivati danneggiati e ricostruiti.
- Verificati M3-T: cancellazione ripetuta restituisce false; riassociazione ripristina la ricerca. Clear rimuove le associazioni della sola collezione richiesta; stessa identita e metadato nell'altra collezione restano leggibili e ricercabili dopo riapertura.
- Verificati M3-T: contenuto vuoto presente dopo scrittura singola e bulk, anche dopo riapertura; identita assente restituisce null e false.
- File M3-T: tests/HnswTest/FilteredSearchTest.cs e questo dossier. Directory dei singoli casi in .temp/shared-filter-tests rimosse dal fixture. Due MSB4011 e due xUnit1031 preesistenti; nessun nuovo warning.
- Skill M3-T: phase-development-task-execution, base-be-base-rules, implementation-be-dotnet-dev, capabilities-dev-problem-solving-methodology, run-tests, base-dossier-writing. Le capability guidano i controlli discriminanti e l'esecuzione xUnit filtrata.
- Limiti M3-T: nessuna atomicita tra file dopo crash verificata. Nessuna garanzia di migrazione, compattazione o backup v2 verificata. Percorso pubblico singolo e bulk verificato; helper interno sincrono non esercitato direttamente.
- M4: closed. Qualita del codice approvata esplicitamente dall'utente. SearchByMetadata implementata nello Store; budget obbligatorio per query. Risultati con identita stabile del metadato, embedding vincente e massimo punteggio osservato. Search, HNSW e primitive invariati; nessun test modificato nel task di produzione.
- Verifica M4: compilazione riuscita e cinque casi FilteredSearchTest passati. Due MSB4011 e due xUnit1031 preesistenti; nessun nuovo warning. Le regressioni non verificano il ranking raggruppato.
- Comportamento M4: prima richiesta min(top, budget); richieste successive raddoppiate e limitate al budget residuo. Il budget limita la somma dei candidati richiesti, inclusi quelli ripetuti. Arresto al numero di gruppi richiesto, budget esaurito o risposta senza candidati. Pareggi tra gruppi ordinati per ID metadato; assenza o top/budget non positivi restituiscono lista vuota.
- Limiti M4: numero di gruppi inferiore a top ammesso. Nessuna ricerca snapshot durante mutazioni concorrenti. Budget dei candidati non limita i nodi visitati dall'indicizzatore o le sue allocazioni interne.
- Skill M4: phase-development-task-execution, phase-development-configuration-options, base-be-base-rules, implementation-be-dotnet-dev, capabilities-dev-problem-solving-methodology, base-dossier-writing. La capability guida l'identificazione del percorso di ranking e del contratto mancante.
- File M4: src/Jigen/Jigen.Store/Extensions/SearchExtensions.cs, src/Jigen/Jigen.Store/SharedMetadataStore.cs e questo dossier.
- M4-T: closed. Sei nuovi casi passati; undici casi della fixture FilteredSearchTest passati includendo le regressioni M3-T. Produzione invariata.
- Verificati M4-T: otto vettori in tre gruppi, con sei vettori nel primo gruppo. Ranking, embedding vincente e punteggio confrontati con scansione brute force completa, usando sia HNSW sia brute force. Contenuti uguali con identita metadato diverse restano tre gruppi.
- Verificati M4-T: budget cumulativo nove restituisce un solo gruppo dopo richieste da tre e sei candidati; budget sessantaquattro raggiunge tre gruppi. Filtro sul metadato aggiornato esclude il primo gruppo; risultati filtrati invariati dopo riapertura. Filtro senza corrispondenze, collezione assente e top/budget non positivi restituiscono risultati vuoti.
- File M4-T: tests/HnswTest/FilteredSearchTest.cs e questo dossier. Directory dei singoli casi in .temp/shared-filter-tests rimosse dal fixture. Due MSB4011 e due xUnit1031 preesistenti; nessun nuovo warning.
- Skill M4-T: phase-development-task-execution, base-be-base-rules, implementation-be-dotnet-dev, capabilities-dev-problem-solving-methodology, run-tests, base-dossier-writing. Le capability guidano il riferimento brute force, il caso sbilanciato e il runner filtrato.
- Limiti M4-T: confronto su dataset controllato, non garanzia di top-k esatto ANN. Nessuna misura dei nodi visitati, delle allocazioni interne o della concorrenza durante ricerca.
- M5: closed. Qualita del codice approvata esplicitamente dall'utente. Compattazione condivisa, elenco backup e statistiche v2 implementati. Nessun test modificato nel task di produzione; primitive originarie e HNSW invariati.
- Verifica M5: diciannove regressioni Store, undici regressioni HNSW, dodici regressioni attivazione e due test StoreShrinkTests passati. I due test verificano shrink manuale, auto-shrink, dati vivi, nuove scritture e riapertura. Tre xUnit2013 Store, cinque xUnit2017 PrimitiveTests, due MSB4011 e due xUnit1031 HNSW preesistenti; nessun nuovo warning.
- Implementato M5: snapshot con contenuto corrente agli stessi ID e soli record delle associazioni attive. Slot delle vecchie versioni vuoti; record MetadataRetired impediscono il riuso dei loro ID dopo replay. Nessuna rimozione o rinumerazione delle liste; metadati senza embedding conservati.
- Implementato M5: costruzione e verifica tramite StoredList; chiusura sorgenti prima della pubblicazione; attivazione e cleanup tramite PersistentFileGeneration. Riapertura risolve il manifest prima del replay. Proprietario generazione acquisito alla prima compattazione o alla riapertura di una generazione pubblicata; comportamento delle sorgenti non attivate invariato.
- Implementato M5: ShrinkAsync usa la composizione per i contenuti e il compattatore esistente per vettori e indice. Riferimenti metadato positivi conservati nell'indice; nessuna lettura dei contenuti a offset legacy. Nessun nuovo file writer nello Store.
- Implementato M5: GetFileNames elenca indice, sorgente contenuti attiva, vettori, sorgenti/indici condivisi, manifest attivo e header originale; WAL incluso quando abilitato. ContentSize legge la sorgente attiva; CollectionInfo.ContentSize conta una volta ogni metadato corrente nella collezione. DeadBytes conta payload superati, mutazioni obsolete e vettori morti; FragmentationRatio esclude gli indici derivati.
- Limiti M5: snapshot conserva il numero di slot contenuti, inclusi gli slot vuoti; non riduce l'indice posizionale managed delle liste. Statistiche eseguono scansioni e la misura di collezione usa un insieme transitorio di ID. Compattazione richiede sorgenti nella stessa directory proprietaria.
- Limiti M5: attivazione condivisa verificata, ma scambio dei file vettori/indice esistente non transazionale tra file. Nessuna atomicita dell'intero Store dopo crash dimostrata. Backup a Store chiuso con percorsi relativi conservati; nessuna snapshot di backup concorrente.
- Skill M5: phase-development-task-execution, phase-development-configuration-options, phase-development-deviation-routing, base-be-base-rules, implementation-be-dotnet-dev, capabilities-dev-problem-solving-methodology, run-tests, base-dossier-writing. Le capability guidano la verifica immediata di lifecycle, statistiche e percorso shrink.
- File M5: SharedMetadataStore.cs, Store.cs, StoreShrink.cs, DataStructures/SharedMetadataMutationKind.cs in Jigen.Store; FileGenerationOptions.cs e PersistentFileGeneration.cs nelle nuove primitive; questo dossier.
- M5-T: closed. Due nuovi casi passati; sedici casi SharedMetadataStoreTests e StoreShrinkTests passati. Produzione invariata.
- Verificati M5-T: due compattazioni successive conservano ID condivisi e metadati senza embedding, eliminano sorgenti superate e riducono i payload. ID di versione restano non validi come metadati; nuovi ID crescono senza riuso. Cancellazioni, riassociazione e aggiornamento condiviso corretti dopo riapertura.
- Verificati M5-T: copia dei soli file GetFileNames a Store chiuso, conservando percorsi relativi, riaperta in un'altra directory. Contenuti, ID metadato, vettori, gruppo vincente e statistiche correnti conservati. Backup aggiornabile e ricompattabile; sorgente originale invariata dopo modifiche alla copia.
- File M5-T: tests/JigenStoreTests/SharedMetadataStoreTests.cs e questo dossier. Directory dei nuovi casi in .temp/shared-metadata-tests rimosse dal fixture. Tre xUnit2013 preesistenti; nessun nuovo warning.
- Skill M5-T: phase-development-task-execution, base-be-base-rules, implementation-be-dotnet-dev, capabilities-dev-problem-solving-methodology, run-tests, base-dossier-writing. Le capability guidano ripetizione della compattazione, isolamento della copia e runner filtrato.
- Limiti M5-T: backup dati Store verificato con brute force; copia di un grafo HNSW esterno non verificata. Nessun backup concorrente o crash durante compattazione Store dimostrato.
- M6: closed. Qualita del codice approvata esplicitamente dall'utente. Conversione automatica implementata prima dell'apertura dei file operativi e del writer; nessun fallback legacy. Nessun test modificato nel task di produzione.
- Implementato M6: rilevazione tramite sorgente condivisa o relativo manifest v2; manifest Store attivo risolto prima dei controlli legacy. Replay read-only dell'indice legacy con tombstone; destinazione contiene soltanto entry vive. Contenuti presenti convertiti in metadati indipendenti; contenuti assenti restano assenti.
- Implementato M6: verifica ID e limiti dei record contenuto/vettore, copia byte-identica di vettori e WAL tramite nuova primitiva, riscrittura indice con ID metadato piu uno. Confronto delle entry e dei contenuti riaperti e checksum vettori/WAL prima dell'attivazione. Record sorgente incoerenti interrompono apertura senza modificare la sorgente.
- Implementato M6: una generazione Store contiene indice, vettori, header contenuti, sorgenti condivise e WAL esistente. Manifest unico attivato dopo verifica; soli file legacy elencati eliminati dalla primitiva. Riapertura con cleanup pendente verifica la destinazione prima della rimozione. Generazione non attivata non servita; tentativo successivo riparte dalla sorgente.
- Verifica M6: build Store e Primitives riuscite senza warning; sedici regressioni SharedMetadataStoreTests e StoreShrinkTests passate. Nessuna fixture legacy o interruzione del percorso Store verificata nel task di produzione; prove richieste in M6-T.
- Limiti M6: replay conserva entry vive e riferimenti in dizionari managed proporzionali all'indice. Record/tail legacy invalidi rifiutati, non riparati. Nessuna garanzia dopo perdita di alimentazione; directory candidate non attivate conservate. Nessuna verifica del riuso del grafo HNSW legacy; formato grafo invariato.
- File M6: src/Jigen/Jigen.Store/StoreMigration.cs, src/Jigen/Jigen.Store/Store.cs, src/Jigen/Jigen.Primitives/Persistance/PersistentFileGeneration.cs e questo dossier. Primitive originarie e HNSW invariati.
- Skill M6: phase-development-task-execution, phase-development-configuration-options, base-be-base-rules, implementation-be-dotnet-dev, capabilities-dev-problem-solving-methodology, run-tests, base-dossier-writing. Le capability guidano formato legacy verificato, confronto prima del cleanup e regressioni immediate.
- M6-T: closed. Sette casi StoreMigrationTests passati; produzione invariata.
- Verificati M6-T: fixture nel formato legacy con sovrascritture, tombstone, ID uguale in collezioni diverse, contenuti uguali indipendenti e vettore senza contenuto. Entry vive, contenuti, collezioni, ID e byte del file vettori conservati; sorgenti legacy rimosse dopo apertura riuscita. Seconda apertura non modifica il manifest; nuove scritture e compattazione funzionanti.
- Verificati M6-T: record contenuto vivo invalido interrompe conversione lasciando sorgenti invariate e senza manifest attivo; nuova conversione riesce dopo riparazione. Manifest temporaneo non pubblicato non attiva la destinazione; ripartenza dalla sorgente con nuova generazione.
- Verificati M6-T: cleanup pendente dopo pubblicazione ripreso con uno dei sorgenti gia assente. Destinazione vettori corrotta impedisce ulteriori cancellazioni; riparazione permette ripresa. Nessuna nuova migrazione quando il manifest attivo esiste.
- Verificati M6-T: backup a Store chiuso dopo migrazione e compattazione include entrambi i manifest; riapertura in altra directory e nuova compattazione conservano dati e vettori.
- File M6-T: tests/JigenStoreTests/StoreMigrationTests.cs e questo dossier. Directory dei singoli casi in .temp/migration-tests rimosse dal fixture. Tre xUnit2013 preesistenti; nessun nuovo warning.
- Skill M6-T: phase-development-task-execution, base-be-base-rules, implementation-be-dotnet-dev, capabilities-dev-problem-solving-methodology, run-tests, base-dossier-writing. Le capability guidano fixture binaria, confronto dei dati e simulazioni discriminanti delle interruzioni.
- Limiti M6-T: interruzioni simulate deterministicamente, nessun kill osservato dentro la migrazione o perdita di alimentazione. WAL verificato come file vuoto abilitato; replay di un WAL legacy con record pendenti non verificato. Riuso del grafo HNSW legacy non verificato. Nessuna verifica completa della soluzione o dichiarazione di prontezza al rilascio.
- L4: closed. Qualita del codice approvata esplicitamente dall'utente. PersistentFileGeneration implementata; build Primitives riuscita senza warning. Primitive esistenti, Store e test invariati nel task di produzione.
- File L4: FileGenerationOptions.cs, FileGenerationManifest.cs e PersistentFileGeneration.cs in src/Jigen/Jigen.Primitives/Persistance, piu questo dossier.
- Skill L4: phase-development-task-execution, phase-development-configuration-options, phase-development-deviation-routing, base-be-base-rules, implementation-be-dotnet-dev, capabilities-dev-problem-solving-methodology, base-dossier-writing. La capability guida l'ordine verifica/pubblicazione/cleanup e la build mirata.
- L4-T: closed. Dodici casi TestPersistentFileGeneration passati. Produzione invariata.
- Verificati L4-T: attivazione di due file verificati, riapertura, rimozione dei soli sorgenti elencati. Verifica applicativa fallita conserva manifest, generazione precedente e sorgente; successivo tentativo riuscito.
- Verificati L4-T: errore di cleanup dopo la prima eliminazione lascia la destinazione pubblicata e l'istanza non disponibile. Riapertura riprende la pulizia con sorgenti gia assenti; destinazione corrotta o mancante impedisce ulteriori eliminazioni. Dopo riparazione la pulizia termina e non elimina nuovi file omonimi alle riaperture successive.
- Verificati L4-T: manifest temporaneo completo e persistito ma non pubblicato ignorato, con e senza generazione precedente. Sorgente conservata e attivazione successiva riuscita. Percorsi fuori dall'ambito, file riservati e cleanup della destinazione rifiutati prima della pubblicazione.
- File L4-T: tests/PrimitiveTests/TestPersistentFileGeneration.cs e questo dossier. Directory dei singoli casi in .temp/file-generation-tests rimosse dal fixture. Cinque xUnit2017 preesistenti; nessun nuovo warning.
- Skill L4-T: phase-development-task-execution, base-be-base-rules, implementation-be-dotnet-dev, capabilities-dev-problem-solving-methodology, run-tests, base-dossier-writing. Le capability guidano gli stati interrotti discriminanti e il runner xUnit filtrato.
- Limiti L4-T: interruzioni simulate deterministicamente, nessuna terminazione reale del processo o perdita di alimentazione. Nessuna verifica della compattazione o migrazione Store. Prossimo task: integrazione M5.

## Generation activation

- Direzione approvata: nuova primitiva in Jigen.Primitives, riutilizzata da M5 e M6. Nessuna modifica a primitive esistenti.
- PersistentFileGeneration possiede una directory e un handle esclusivo basato su ManifestName. Un proprietario per manifest alla volta; directory attendibile e non condivisa con writer esterni.
- CreateGeneration crea una directory con Guid.CreateVersion7. GetFilePath risolve un nome file nella generazione; nessuna conoscenza di metadati o embedding.
- Activate riceve file destinazione, nomi relativi esatti delle sorgenti obsolete e callback di verifica applicativa. Il chiamante chiude writer e reader prima dell'attivazione.
- Ordine: verifica applicativa, Flush(true) dei file destinazione, checksum SHA-256, scrittura e Flush(true) del manifest temporaneo, sostituzione del manifest definitivo nella stessa directory.
- La pubblicazione del singolo manifest determina la generazione attiva. Il manifest contiene versione, ID generazione, checksum e lista cleanup; non rinomina separatamente i file dati.
- Dopo pubblicazione: verifica checksum destinazione, eliminazione dei soli file obsoleti elencati, pubblicazione del manifest senza cleanup pendente. Nessuna eliminazione di directory o enumerazione indiscriminata.
- Riapertura con cleanup pendente: verifica tutti i checksum prima della prima eliminazione; file sorgente gia assenti ammessi; ripresa della pulizia. Manifest definitivo invalido interrompe apertura senza fallback al temporaneo.
- Una verifica fallita prima della pubblicazione lascia attiva la generazione precedente e non elimina sorgenti. Errore durante pubblicazione o cleanup richiede riapertura dell'istanza.
- File attivi immutabili fino al completamento del cleanup. I checksum certificano il contenuto attivato, non gli aggiornamenti operativi successivi. La costruzione dei file e la verifica del dominio restano del chiamante tramite primitive.
- Limiti: nessuna fsync delle directory o garanzia dopo perdita di alimentazione; checksum non autentica il manifest. Generazioni preparate ma non attivate restano fuori dal cleanup automatico.

## Deviation routing

- M3: decisione sul lifecycle autorizzata dall'utente. La composizione richiede alle nuove primitive l'apertura per ricostruzione tramite DiskIndexOptions.Recreate, senza gestione diretta dei file nello Store. Contratto in Configuration options.
- Stato corrente: Compatibility e Generation activation di questo dossier. Utente ha approvato percorso v2 unico, migrazione automatica e rimozione legacy dopo verifica; nessuna modalita opt-in da implementare. Blocco sulla ricostruzione risolto da L3-T. L4-T, M6 e M6-T chiusi; evidenze e limiti nei rispettivi task.
- M5: autorizzata dall'utente una nuova primitiva di attivazione generazioni, condivisa con M6. Decisione corrente in Generation activation. M5 riprende dopo L4-T; nessun nuovo file writer nello Store.
- M3: blocco sul contratto del test di crash risolto da M3-R-T autorizzato dall'utente. Aspettativa allineata al commit sincrono di Commit and recovery specification; nessun indebolimento del commit.

## Configuration options

- FileGenerationOptions: opzioni della nuova primitiva di attivazione in Jigen.Primitives/Persistance/FileGenerationOptions.cs. DirectoryPath obbligatorio, senza default, non segreto; normalizzato e controllato nel costruttore. ManifestName opzionale, default generation, non segreto; nome base senza separatori validato prima dell'apertura. Suffissi .json, .json.pending e .lock interni alla primitiva; nessuna lettura di ambiente o configurazione dispersa.
- SharedMetadataStore deriva DirectoryPath dalla directory associazioni e ManifestName dal nome originale associazioni con suffisso .generation. Generazioni sotto directory Guid in formato N; sorgenti contents e associations, indici .index; lookup .lookup e versioni .versions ricostruiti. Nessuna nuova opzione Store o modalita opt-in.
- StoreMigration deriva DirectoryPath da DataBasePath e ManifestName da DataBaseName con suffisso .store-generation. Directory destinazione Guid in formato N; nomi dei file operativi conservati nella generazione. GetFileNames include il manifest Store quando attivo; backup richiede percorsi relativi conservati.
- DiskIndexOptions: configurazione delle nuove primitive su disco; dichiarata in Jigen.Primitives/Persistance/DiskIndexOptions.cs.
- FilePath: obbligatorio, nessun default, non segreto. ViewSize: default 1048576 byte, multiplo di 65536, minimo 65536 e massimo 1048576; non segreto.
- Recreate: opzionale, default false, non segreto. False apre o crea il file conservando i dati; true crea o tronca il solo FilePath prima del mapping, senza leggere o recuperare il contenuto precedente. Modalita distruttiva esplicita per indici derivati ricostruibili; nessun fallback automatico dopo corruzione.
- Recreate non esegue replay e non rende atomica la ricostruzione. Il chiamante ricostruisce dalle sorgenti prima di esporre letture; un'interruzione richiede una nuova ricostruzione. Non usare la modalita sui file sorgente.
- MappedIndexFile valida le opzioni prima di aprire il file. Usa una sola vista; nessuna lettura diretta di configurazione, ambiente o secret store.
- PersistentScalarArray usa buffer operativi fissi inferiori al limite approvato di 1 MiB. Nessun buffer cresce con il numero di slot.
- SharedMetadataStore deriva i percorsi degli indici dalle opzioni delle liste: FilePath associazioni con suffisso .lookup e FilePath contenuti con suffisso .versions. Recreate true alla riapertura; budget ViewSize predefinito di DiskIndexOptions. Nessuna opzione opt-in dello Store.
- Store deriva le sorgenti condivise da DataBasePath e DataBaseName con suffissi .shared.contents.jigen e .shared.associations.jigen. Espone la composizione tramite SharedMetadata e ne gestisce apertura e chiusura; nessun nuovo parametro StoreOptions.