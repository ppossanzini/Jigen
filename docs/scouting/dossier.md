# Scouting: struttura su disco dei vettori sparsi

Giro di scouting dedicato alla sola struttura su disco dei vettori sparsi. Ambito: come i vettori sparsi vengono scritti, riaperti e interrogati su disco. Fuori ambito: il calcolo dei pesi, la fusione con la ricerca densa, le superfici di rete.

Input: codice al commit `ee9e75c`, implementazione precedente sul ramo `oldoldold` (commit `297e3c4`).

## Scouting diagrams

Preferenza di lettura: diagrammi prima. Il set completo è in [Scouting diagrams](diagrams.md) (diagrams.md). Ogni diagramma è richiamato dalla propria sezione.

Indice:

1. [Phase flow](diagrams.md#1-phase-flow) (diagrams.md#1-phase-flow)
2. [Position in the lifecycle](diagrams.md#2-position-in-the-lifecycle) (diagrams.md#2-position-in-the-lifecycle)
3. [Data sources, actors and output types](diagrams.md#3-data-sources-actors-and-output-types) (diagrams.md#3-data-sources-actors-and-output-types)
4. [Management services per step](diagrams.md#4-management-services-per-step) (diagrams.md#4-management-services-per-step)
5. [Problem and outcome frame](diagrams.md#5-problem-and-outcome-frame) (diagrams.md#5-problem-and-outcome-frame)
6. [Capability and risk map](diagrams.md#6-capability-and-risk-map) (diagrams.md#6-capability-and-risk-map)
7. [Adopt-extend-build map](diagrams.md#7-adopt-extend-build-map) (diagrams.md#7-adopt-extend-build-map)
8. [Question and proof map](diagrams.md#8-question-and-proof-map) (diagrams.md#8-question-and-proof-map)

Il terzo overview richiesto, i flussi raggruppati per deployment, è omesso: in questa fase non esiste una decisione di deployment da mostrare.

La mappa e i suoi punti di controllo sono in [Phase flow](diagrams.md#1-phase-flow) (diagrams.md#1-phase-flow).

## Problem framing

**Problema.** Un database che deve conservare pesi lessicali per voce non ha oggi un formato su disco che si riapra in tempo indipendente dalla dimensione dei dati senza tenere più copie delle stesse coppie termine/peso.

**Per chi.** Lo sviluppatore che usa l'engine embedded e l'amministratore che riapre un database dopo un arresto. L'esito si osserva sul tempo di apertura e sullo spazio occupato, non sulla qualità della ricerca.

**Esito osservabile.**

| Esito | Soglia |
| --- | --- |
| Tempo di apertura di un database con vettori sparsi | non cresce con il numero di coppie termine/peso archiviate |
| Copie delle stesse coppie termine/peso su disco | una sola |
| Spazio delle liste rispetto alle coppie grezze | inferiore, con il rapporto misurato prima dello sviluppo |
| Ricerca per termine | restituisce i documenti con il termine senza scansionare la collezione |

**Vincoli non negoziabili.**

| Vincolo | Stato | Cosa impone |
| --- | --- | --- |
| Nessuna ricostruzione dell'indice all'apertura | dato: decisione dell'utente, 23 ottobre | l'apertura legge un manifest e mappa i file, non rilegge le liste |
| Nessuna duplicazione delle coppie termine/peso | dato: decisione dell'utente, 23 ottobre | una sola copia autorevole, le liste non sono un secondo archivio |
| Un solo scrittore per database | dato: comportamento attuale dello Store | le liste si scrivono dal thread di scrittura, in append |
| Nessun servizio esterno nella modalità embedded | dato: [Stack Profile](../../StackProfile.md) (StackProfile.md) | le liste vivono nel processo, su file memory-mapped |
| Licenza permissiva | dato: licenza Apache-2.0 del progetto | nessuna dipendenza con licenza incompatibile |
| Spazio e latenza dell'indice denso invariati | dato: misure correnti in [Benchmarks & Hardware Support](../../benchmarks.md) (benchmarks.md), da rieseguire dopo la prima consegna | il formato dei vettori densi non si tocca |

**Fuori ambito.**

- Calcolare i pesi nel server: nessun analizzatore, nessuna statistica di corpus.
- Generare vettori sparsi da modelli: i pesi arrivano dal chiamante.
- Fondere i punteggi denso e sparso.
- Comprimere o potare per la latenza in questa prima consegna.

**Domande aperte, ordinate per impatto.**

| Id | Domanda | Impatto |
| --- | --- | --- |
| [A-1](#assumption-interrogation) (dossier.md#assumption-interrogation) | Il tempo di apertura resta indipendente dalla dimensione dei dati? | Decide l'intera struttura |
| [A-3](#assumption-interrogation) (dossier.md#assumption-interrogation) | L'append senza riscritture è compatibile con gli aggiornamenti? | Decide il modello a segmenti e le tombstone |
| [A-6](#assumption-interrogation) (dossier.md#assumption-interrogation) | Un solo vettore sparso per voce o più di uno? | Decide la forma del record |
| [A-4](#assumption-interrogation) (dossier.md#assumption-interrogation) | La compressione è necessaria? | Decide spazio e lavoro |
| [A-5](#assumption-interrogation) (dossier.md#assumption-interrogation) | La potatura serve? | Decide latenza e lavoro |

## Assumption interrogation

| Id | Assunzione | Come si falsifica | Stato |
| --- | --- | --- | --- |
| A-1 | L'apertura può essere indipendente dalla dimensione dei dati | Un database con 10 milioni di coppie si apre nello stesso tempo di uno con 10.000 | Aperta: prova Pr-1 |
| A-2 | Una sola copia delle coppie basta a servire la ricerca | Una ricerca che richiede un dato assente dall'archivio e presente solo nell'indice | Vera se le coppie e le liste sono lo stesso file, o se le liste si riscrivono in modo differito |
| A-3 | Scrivere in append non richiede riscritture delle liste esistenti | Un aggiornamento della stessa voce che modifica liste già scritte | Falsa con liste per termine in un unico file mutabile: serve un modello a segmenti immutabili con tombstone |
| A-4 | La compressione è necessaria | Lo spazio grezzo delle coppie resta sotto il budget dichiarato su un corpus reale | Aperta: prova Pr-2 |
| A-5 | La potatura serve | La latenza della ricerca esatta su 1 milione di coppie resta sotto la soglia richiesta | Aperta: prova Pr-3 |
| A-6 | L'utente accetta una sola forma di vettore sparso per voce | Una richiesta di più slot lessicali per la stessa voce | Aperta: domanda Q-1 |
| A-7 | Il formato può evolvere senza rompere gli archivi | La riapertura di un archivio scritto da una versione precedente fallisce | Vera solo con un numero di versione nel manifest |

Assunzioni che invalidano l'idea se false: A-1 e A-2. Entrambe descrivono la causa del comportamento osservato nell'implementazione precedente: le coppie erano conservate in tre forme (file dei dati, log delle posizioni, indice invertito in memoria) e a ogni apertura lo `Store` rileggeva l'intero log delle posizioni con `LoadSparseIndex`, mentre l'indice invertito si costruiva alla prima ricerca.

## Solution scouting

Criterio di rifiuto fissato prima del confronto: una libreria è adottabile solo se si integra con lo `Store` esistente (scrittura dal thread dedicato, file memory-mapped, licenza permissiva) e se evita la ricostruzione all'apertura.

| Capacità | Ricerca | Decisione |
| --- | --- | --- |
| File dei record, indice delle posizioni, flush, compattazione, cache di lettura | asset interno già usato dal grafo HNSW: `StoredList<T, TOptions>` in `src/Jigen/Jigen.Primitives/Persistance/StoredList.cs`, con file dati, file `.index` a voci fisse da 24 byte, `ShrinkDb`, flush incrementale | extend |
| Formato delle coppie termine/peso | nessuna libreria .NET adatta; il payload è un `IStorableItem` serializzato dentro `StoredList` | build |
| Indirizzamento per termine | `StoredList` indirizza per indice intero, non per chiave: manca un dizionario termine → indice | build |
| Compressione delle liste | PISA espone Elias-Fano, partitioned Elias-Fano, SIMD-BP128 e StreamVByte come schemi di riferimento, non come pacchetto .NET; nessun port .NET maturo verificato | build, dopo Pr-2 |
| Potatura dei candidati | algoritmi pubblicati e implementati in PISA e Lucene; nessuna libreria .NET pronta | build, dopo Pr-3 |
| Ricerca esatta sulle liste | nessuna dipendenza: attraversamento delle liste di posting | build |

**Cosa offre `StoredList` e cosa non offre.** Apertura: legge in un solo accesso sequenziale il file `.index` e materializza una voce da 24 byte per elemento, senza rileggere i payload e senza costruire strutture derivate. Scrittura: `Add` appende, il setter riscrive sul posto se il record sta in `MaxLength`, altrimenti appende un nuovo record e lascia spazio morto che `ShrinkDb` recupera. Rimozione: `RemoveAt` sposta gli indici degli elementi successivi. Non offre l'accesso per chiave, non comprime il payload e non tiene l'indice fuori dalla memoria.

**Conseguenza sul modello.** Una voce di `StoredList` è un termine. Le liste di posting stanno in blocchi append-only, anch'essi elementi di una seconda `StoredList`, e il record del termine contiene il puntatore al blocco più recente, la frequenza di documento e il massimo di blocco.

### Costo di apertura

All'apertura `StoredList` legge il file `.index` con un solo accesso sequenziale e tiene in memoria una voce da 24 byte per elemento. La memoria occupata è quindi 24 byte per elemento, e il tempo è la lettura sequenziale di quei byte.

| Elemento di `StoredList` | Quantità | Memoria all'apertura |
| --- | --- | --- |
| termine | termini distinti presenti nella collezione | 24 byte per termine: 1 milione di termini sono 24 MB |
| blocco di posting | numero totale di blocchi persistenti | 24 byte per blocco: 2 milioni di blocchi sono 48 MB |

Il conteggio dei termini e il numero di blocchi sono valori da misurare: valgono Pr-1 e Pr-3. Con blocchi di capacità fissa, il numero di blocchi cresce anche con il numero di coppie termine/documento. Due `StoredList` richiedono almeno 24 × (termini + blocchi) byte per gli indici, oltre a dizionario, oggetti e cache. Questo non dimostra un costo di apertura indipendente dai dati.

**Perché il blocco esiste.** Un blocco nuovo si appende e il record del termine aggiorna il puntatore, quindi un inserimento non riscrive la lista già scritta. Una soglia di compattazione può limitare il numero di segmenti, ma non limita il numero di blocchi a capacità fissa dentro ciascun segmento.

**Aggiornamento.** Il record del termine si riscrive sul posto finché sta in `MaxLength`; un blocco nuovo si appende. La cancellazione di un documento non riscrive le liste: la ricerca verifica che la chiave del posting sia ancora presente nell'indice delle posizioni denso.

Nessuna capacità si adotta come dipendenza esterna. Il progetto non introduce un motore di ricerca esterno nella modalità embedded.

## Analogue solutions

**Problema e destinatario della ricerca.** Conservare pesi lessicali per voce su disco, riaprirli senza ricostruzione, interrogarli per termine, per lo sviluppatore di un database vettoriale embedded.

| Id | Tipo | Prodotto | Cosa fa | Cosa dimostra | Fonte |
| --- | --- | --- | --- | --- | --- |
| I-1 | diretto | Lucene | Segmenti immutabili con liste di posting per blocco e dati di impatto per blocco | Che un indice su disco si aggiorna per segmenti immutabili e si pota senza rileggere tutto | [From MAXSCORE to Block-Max WAND](https://link.springer.com/content/pdf/10.1007/978-3-030-45442-5_3.pdf) |
| I-2 | diretto | Qdrant | Vettori sparsi come coppie indici/valori, indice definito «simile a un indice invertito» e dichiarato esatto, senza algoritmi di approssimazione; due forme, una mutabile aggiornata all'inserimento e una immutabile e compatta costruita nella stessa ottimizzazione dell'indice denso; livello di memoria configurabile per indice | Che l'indice può essere esatto e che la forma immutabile si costruisce in modo differito | [Qdrant indexing](https://qdrant.tech/documentation/manage-data/indexing/) |
| I-3 | diretto | Vespa | `wand` e `weakAnd` su liste di posting, punteggio come somma lineare | Che la ricerca sparsa è un prodotto interno massimo su spazio sparso, potabile | [Vespa WAND](https://docs.vespa.ai/en/ranking/wand.html) |
| I-4 | adiacente | PISA e BMP | Compressione delle liste, dati WAND, potatura per blocchi per la ricerca sparsa appresa | Che esistono schemi di compressione misurati e un'implementazione di riferimento della potatura | [PISA](https://pisa.readthedocs.io/en/latest/) · [BMP (SIGIR 2024)](https://dl.acm.org/doi/10.1145/3626772.3657906) |
| I-5 | diretto | Milvus, indice `SPARSE_INVERTED_INDEX` | Sei algoritmi selezionabili: `DAAT_MAXSCORE`, `DAAT_WAND`, `TAAT_NAIVE`, `BLOCK_MAX_MAXSCORE`, `BLOCK_MAX_WAND`, `SINDI`; metrica BM25 o prodotto interno; parametro di ricerca `drop_ratio_search` che scarta i valori più piccoli | Che gli algoritmi di potatura sono opzioni di configurazione sullo stesso indice, con predefiniti per carico: `DAAT_MAXSCORE` per BM25, `SINDI` per prodotto interno | [Milvus sparse inverted index](https://github.com/milvus-io/milvus-docs/blob/master/site/en/userGuide/indexes/sparse/sparse-inverted-index.md) |
| I-6 | adiacente | SINDI (ICDE 2026) | Indice a finestre fisse di identificativi di documento con calcolo SIMD del prodotto interno, accesso sequenziale alle liste, scarto delle componenti di valore basso | Che la finestra di documenti è la forma usata per rendere sequenziale l'accesso e vettorizzabile il calcolo; con recall@50 sopra il 99% riporta da 4,2 a 26,4 volte le query al secondo di SEISMIC | [SINDI](https://arxiv.org/abs/2509.08395) |

**Cosa è difficile, per evidenza.** I-1 e I-3 mostrano che la potatura richiede dati ausiliari per blocco e che il punteggio interno deve restare una somma lineare. I-4 mostra che la compressione delle liste è un capitolo a sé, con più schemi e misure pubblicate. Nessuna fonte dichiara un tempo di apertura: i motori citati non ricostruiscono l'indice all'apertura, lo mantengono in file propri.

**Conseguenze per questa idea.**

- Requisito acquisito: le liste vivono in file propri, scritti in append, mai ricostruiti all'apertura.
- Requisito acquisito: il manifest porta un numero di versione del formato.
- Direzione evitata: tenere l'indice solo in memoria e ricostruirlo all'apertura.
- Direzione evitata: duplicare le coppie in un archivio per voce e in un indice separato.

**Limite del confronto.** Le configurazioni esposte dai prodotti non dimostrano che tutti gli algoritmi condividano lo stesso layout fisico dei dati.

### Algoritmi di ricerca a confronto

Confronto richiesto dall'utente il 23 ottobre. Criteri: esattezza del top-k, dati ausiliari da mantenere, lavoro per query, compatibilità con liste scritte in append.

| Id | Algoritmo | Top-k esatto | Dati ausiliari | Pro | Contro |
| --- | --- | --- | --- | --- | --- |
| AL-1 | Scansione delle liste dei termini, coda a priorità per il top-k | sì | nessuno | codice minimo, nessun dato da mantenere, esatto per costruzione | lavoro proporzionale alla somma delle lunghezze delle liste |
| AL-2 | WAND | sì | peso massimo per termine | salta i documenti la cui somma dei massimi non raggiunge la soglia corrente; [Vespa](https://docs.vespa.ai/en/ranking/wand.html) riporta oltre il 90% di valutazioni complete in meno su una query di esempio | il massimo per termine va aggiornato a ogni modifica; il guadagno cala quando le liste sono corte |
| AL-3 | Block-Max WAND | sì | massimo per blocco di ogni lista | soglia più stretta di WAND, quindi più blocchi saltati; usato da [Lucene](https://link.springer.com/content/pdf/10.1007/978-3-030-45442-5_3.pdf) | un massimo per blocco per termine, da mantenere; più codice di WAND |
| AL-4 | MaxScore | sì, con limiti superiori validi | peso massimo per termine | separa liste essenziali e non essenziali usando la soglia top-k; i posting restano ordinati per documento | efficacia dipendente dalla distribuzione dei pesi e dalla soglia |
| AL-5 | Block-Max MaxScore | sì, con limiti superiori validi | massimo per termine e per blocco, confini e indirizzamento dei blocchi | applica limiti locali alla selezione delle liste di MaxScore | richiede iteratori capaci di saltare blocchi senza decodificarne i posting |
| AL-6 | Block-Max Pruning (BMP) | sì nella modalità sicura rispetto agli impatti indicizzati; approssimato con potatura o terminazione anticipata | limiti per intervallo di documenti e indice ibrido per blocco; implementazione di riferimento con impatti a 8 bit | ordina i blocchi per limite superiore della query e visita prima quelli con contributo potenziale maggiore | cambia il layout rispetto a liste globali per termine; la sicurezza del ranking non dimostra equivalenza ai float originali |
| AL-7 | SEISMIC | no | blocchi di vettori raggruppati per similarità e vettori riassuntivi, oltre ai dati per valutare i candidati | riduce i candidati per query sparse lunghe | strutture ausiliarie e costruzione dei gruppi; prestazioni pubblicate in memoria, non una verifica del layout su disco |

**Conseguenza per il formato.** AL-1 non richiede massimi precalcolati. Aggiungere AL-3 dopo la scrittura dei dati senza ricostruzione richiede di predisporre nel formato massimi, confini e indirizzamento dei blocchi. La sola presenza dei pesi non evita di rileggerli per calcolare i massimi mancanti.

**Prerequisiti che AL-3 e AL-5 impongono alla struttura.** I campi possono stare nei record previsti; il layout deve anche consentire di leggere confini e massimi senza leggere il payload da saltare.

| Cosa serve | Come si ottiene | Aggiunta |
| --- | --- | --- |
| peso massimo per termine | massimo dei massimi dei blocchi del termine | campo facoltativo da 4 byte nel record del termine |
| massimo di impatto per blocco | calcolato quando il blocco si chiude | campo da 4 byte nell'intestazione del blocco |
| elenco dei blocchi di un termine | il record del termine punta all'ultimo blocco, ogni blocco punta al precedente | campo da 4 byte per blocco |
| posting ordinati per documento | proprietà di scrittura del blocco, non un dato in più | nessuna |
| documenti ancora vivi | indice delle posizioni denso, già presente | nessuna |
| soglie e confini della query | calcolati a ogni query | nessuna |

Restano da decidere il riferimento al documento dentro il posting e la compattazione dei blocchi. Il riferimento non può essere la posizione assegnata dallo `Store`: `ShrinkAsync` ricompatta i file densi e riassegna le posizioni.

**Filtri.** Con un filtro sui metadati la soglia di potatura deve tenere conto delle voci scartate dal filtro; la via più semplice è disattivare la potatura quando c'è un filtro e lasciare AL-1.

### Paper su prestazioni e struttura dei dati

Ricerca del 23 ottobre 2026. Le fonti distinguono struttura dell'indice, algoritmo di visita e compressione. I risultati misurati dagli autori non sono benchmark di Jigen.

| Paper e fonte primaria | Esattezza | Struttura e lavoro evitato | Limite per questo progetto |
| --- | --- | --- | --- |
| Turtle e Flood, [Query Evaluation: Strategies and Optimizations](https://doi.org/10.1016/0306-4573(95)00020-H), 1995 | MaxScore esatto con limiti superiori validi | Liste ordinate per documento; massimi per termine; liste essenziali generano candidati e le altre completano il punteggio solo quando necessario | Non descrive apertura o aggiornamenti dello Store; non implica posting ordinati per peso |
| Ding e Suel, [Faster Top-k Document Retrieval Using Block-Max Indexes](https://doi.org/10.1145/2009916.2010048), SIGIR 2011 | esatto con limiti superiori validi | Liste ordinate per documento, blocchi e massimi locali; salta intervalli che non superano la soglia top-k | Dettagli numerici del paper non verificati nel testo completo; il layout deve rendere accessibili confini e massimi prima del payload |
| Mallia, Suel e Tonellotto, [Faster Learned Sparse Retrieval with Block-Max Pruning](https://arxiv.org/abs/2405.01117), SIGIR 2024; [codice BMP](https://github.com/pisa-engine/BMP) | modalità sicura e approssimata | Intervalli comuni di documenti; dentro ogni blocco, directory ordinata dei termini e liste locali; limiti aggregati per query ordinano i blocchi da visitare | L'implementazione richiede impatti quantizzati a 8 bit; il layout ibrido non coincide con liste globali per termine |
| Bruch, Nardini, Rulli e Venturini, [Efficient Inverted Indexes for Approximate Retrieval over Learned Sparse Representations](https://arxiv.org/abs/2404.18812), SIGIR 2024; [codice SEISMIC](https://github.com/TusKANNy/seismic) | approssimato | Liste selezionate e blocchi raggruppati per similarità, riassunti vettoriali per selezionare blocchi, valutazione dei candidati | Riassunti e dati per il calcolo finale richiedono spazio aggiuntivo; supporto nativo su disco non verificato |
| [Pairing Clustered Inverted Indexes with kNN Graphs for Fast Approximate Retrieval over Learned Sparse Representations](https://arxiv.org/abs/2408.04443), CIKM 2024, SeismicWave | approssimato | Estende SEISMIC con visita per importanza ed espansione dei candidati tramite grafo k-NN precalcolato | Il grafo aggiunge spazio e costruzione; non è necessario per la ricerca esatta sulle liste |
| [Dynamic Superblock Pruning for Fast Learned Sparse Retrieval](https://arxiv.org/abs/2504.17045), SIGIR 2025, DSP | modalità sicura e approssimata dichiarate dagli autori | Gerarchia di superblocchi e blocchi; selezione grossolana prima di valutare unità più piccole | Limiti e directory gerarchiche da misurare; nessuna prova di apertura senza replay o prestazioni SSD acquisita |
| [SINDI: An Efficient Index for Sparse Vector Approximate Maximum Inner Product Search](https://arxiv.org/abs/2509.08395), ICDE 2026; [VSAG](https://github.com/antgroup/vsag) | approssimato | Liste che conservano identificatori e valori, finestre fisse di documenti, prodotto interno in batch con SIMD e accessi sequenziali; potatura delle componenti | VSAG dichiara SINDI_V2 con I/O in memoria e su disco; questa variante non ha un paper dedicato verificato né misure SSD raccolte qui |
| Lemire, Boytsov e Kurz, [SIMD Compression and the Intersection of Sorted Integers](https://arxiv.org/abs/1401.6399), 2016; [PISA](https://github.com/pisa-engine/pisa) | codifica lossless degli interi | Compressione a blocchi degli identificatori e decodifica SIMD; SIMD-BP128 è un riferimento per lo spazio e il costo di lettura dei posting | Non comprime automaticamente i pesi float e non è un algoritmo di selezione top-k |

**Risultati pubblicati con condizioni.**

- BMP riporta circa 2–60 volte la velocità dei metodi precedenti nella ricerca sicura, nei carichi del paper con rappresentazioni sparse apprese. Il riferimento è il ranking degli impatti indicizzati, non una garanzia sul ranking dei float prima della quantizzazione.
- SeismicWave riporta fino a 2,2 volte la velocità di SEISMIC sui due dataset del paper, con ricerca approssimata.
- SINDI riporta su MS MARCO 4,2–26,4 volte le query al secondo rispetto a SEISMIC e PyANNs con recall@50 superiore al 99%. Il risultato non misura SINDI_V2 su SSD.
- Questi rapporti non costituiscono una classifica: cambiano corpus, modelli, hardware, k e accuratezza. MRR@10 misura rilevanza e non sostituisce recall@k rispetto al top-k esatto.

**Conseguenze sul formato da discutere, non decisioni adottate.**

1. Liste globali per termine con massimi per blocco consentono WAND/MaxScore e varianti block-max; BMP propone invece liste locali dentro intervalli comuni di documenti.
2. Una directory di blocchi persistente con intervallo, offset e limite superiore consente di saltare payload. Una catena di puntatori al blocco precedente non offre da sola accesso diretto al blocco successivo utile.
3. Riassunti SEISMIC, gerarchie DSP e grafo SeismicWave sono dati ausiliari distinti dai pesi. La loro persistenza evita di ricostruirli, ma non elimina il loro costo di spazio.
4. I limiti di potatura devono rispettare metrica e segno dei pesi. Un solo massimo non basta in generale con contributi negativi; questa ricerca non fissa il dominio dei pesi ammessi.
5. I paper trattano il ranking di vettori. Con più vettori per documento occorre fissare aggregazione e deduplicazione prima di applicare soglie top-k di documento.
6. Nessun risultato raccolto dimostra il tempo di apertura richiesto. Pr-1 deve misurare separatamente apertura, prima query a cache fredda e memoria residente, includendo le directory persistenti.

**Esito del confronto.** Restano da confrontare sullo stesso corpus tre layout: liste per termine con block-max, blocchi per intervallo di documenti come BMP e finestre con valori sequenziali come SINDI. SEISMIC, SeismicWave e DSP restano riferimenti per il compromesso accuratezza, spazio e latenza; nessuno è adottato con questa ricerca.

## Technical feasibility

| Id | Capacità | Rischio dominante | Stato |
| --- | --- | --- | --- |
| C-1 | Registrare le coppie termine/peso in record persistenti | Formato dell'elemento serializzato dentro `StoredList` e dimensione di `MaxLength` per gli aggiornamenti sul posto | noto |
| C-2 | Aprire il database senza ricostruire l'indice | L'apertura resta lineare nel numero di elementi di `StoredList`, quindi il conteggio degli elementi decide la memoria occupata. Vedi il conteggio in [Costo di apertura](#costo-di-apertura) (dossier.md#costo-di-apertura) | ignoto: decide l'esito |
| C-3 | Trovare i documenti che portano un termine | Manca l'indirizzamento per termine: serve un dizionario termine → indice, e una lista il cui record cresce oltre `MaxLength` viene spostata | noto |
| C-4 | Aggiornare e cancellare senza riscrivere le liste | Spazio morto e `ShrinkDb`; la cancellazione di un documento non deve riscrivere ogni lista che lo contiene | noto |
| C-5 | Contenere lo spazio delle liste | Rapporto di compressione reale misurato su dati veri | ignoto |
| C-6 | Potare i candidati per la latenza | Costo dei dati per blocco e perdita di esattezza | ignoto, rimandato |

**Capacità critica.** C-2. Se l'apertura torna a crescere con il numero di documenti o di coppie, il tempo di apertura ha lo stesso andamento dell'implementazione precedente.

**Evidenza che chiude gli ignoti.** C-2: misura del tempo di apertura su due ordini di grandezza di dati. C-5: misura dello spazio su un corpus reale con e senza compressione. C-6: latenza della ricerca esatta a 1 milione di coppie.

**Conclusione sulla realizzabilità.** Realizzabile con ambito ridotto: registrazione e ricerca esatta, senza potatura. Compressione e potatura restano fuori dalla prima consegna e si decidono dopo le misure.

## Unknowns plan

| Id | Domanda | Decisione bloccata | Prova minima | Quando |
| --- | --- | --- | --- | --- |
| Pr-1 | Il tempo di apertura resta piatto al crescere delle coppie? | C-2 e l'intera struttura | Due `StoredList` con 10.000 e 1.000.000 di termini, misurando apertura e memoria; poi lo stesso con 1.000.000 di documenti e 1.000.000 di termini, per mostrare che il costo segue il vocabolario | prima della stima |
| Pr-2 | Quanto spazio risparmia la compressione? | C-5 | Scrivere le stesse coppie con e senza codifica compatta, misurare i due file | dopo la prima consegna |
| Pr-3 | La ricerca esatta basta alla latenza richiesta? | C-6 | Cercare con 100 query su 1.000.000 di coppie con AL-1; ripetere con AL-2 e AL-3 sugli stessi dati e confrontare il tempo per query | dopo la prima consegna |
| Pr-4 | Le tombstone e la compattazione mantengono corretto l'esito? | C-4 | Sovrascrivere e cancellare la stessa voce, riaprire, verificare assenza di duplicati nei risultati | prima della prima consegna |
| Pr-5 | Un solo vettore sparso per voce o più? | Q-1 e la forma del record | Porre la domanda all'utente con i due costi indicati | prima della stima |

La domanda sul formato del record si chiude in [Solution scouting](#solution-scouting) (dossier.md#solution-scouting) con il formato scelto e non richiede prova.

## Order of magnitude estimate

| Id | Capacità | Magnitudine |
| --- | --- | --- |
| C-1 | Registrare le coppie in record persistenti sopra `StoredList` | 1–3 giorni |
| C-2 | Aprire senza ricostruzione, con dizionario dei termini | 3–5 giorni |
| C-3 | Ricerca esatta sulle liste | 3–6 giorni |
| C-4 | Spazio morto e compattazione riusando `ShrinkDb` | 2–5 giorni |
| C-5 | Compressione delle liste | 4–10 giorni, dopo Pr-2 |
| C-6 | Potatura per blocchi | 8–15 giorni, dopo Pr-3 |

**Intervallo aggregato, provvisorio.** Da una settimana e mezza a tre settimane per C-1, C-2, C-3 e C-4, grazie al riuso di `StoredList` per file, indice, flush e compattazione. Da quattro a otto settimane con compressione e potatura. L'intervallo è provvisorio finché Pr-1 non conferma il costo di apertura.

**Incertezza.** Pr-1 e Pr-3 possono spostare l'intervallo in entrambi i versi. Pr-5 può cambiare la forma del record e quindi C-1. Le stime non coprono le superfici di rete, fuori da questo giro.

**Costo delle prove.** Pr-1, Pr-2 e Pr-3 richiedono insieme da uno a due giorni di misura sui banchi già presenti in `tests/JigenBenchmarks`.

## Recommendation and handoff

**Decisione confermata.** Implementare progressivamente Block-Max MaxScore con pesi float non quantizzati. Il primo incremento è il blocco persistente di posting su `StoredList`; algoritmo e integrazione nello Store seguono in task separati, con gate di approvazione prima dei nuovi test. Attore unico ed esecuzione sequenziale confermati il 23 ottobre 2026. Piano: [Piano di sviluppo dei vettori sparsi](../development/task-plan.md).

**Progetto e isolamento.** Il progetto `Jigen.Index.Sparse` possiede i tipi, la persistenza e l'algoritmo specifici degli indici sparsi; dipende da `Jigen.Primitives` per `StoredList`. Ogni indice appartiene a una collezione: file, directory dei termini, blocchi e ciclo di vita sono separati per collezione, non condivisi a livello di database. Richiesta diretta dell'utente del 23 ottobre 2026. I tipi generici di persistenza restano nelle primitive.

**Ranking confermato.** Una query sparsa su una collezione calcola il prodotto
scalare separatamente per vettore. Il punteggio del documento è il massimo
dei prodotti scalari dei suoi vettori sparsi; ogni documento compare una volta.
I risultati seguono il punteggio decrescente e, a parità, la chiave binaria
lessicografica crescente. Conferma esplicita dell'utente del 23 ottobre 2026.
La ricerca considera solo i vettori con almeno un termine in comune con la query.
I documenti senza vettori corrispondenti sono esclusi. Nel massimo per documento
non entrano i vettori senza termini comuni: il loro zero non sostituisce un
punteggio negativo dei vettori corrispondenti. Questa selezione dei candidati
è confermata dall'utente; il top-k è esatto su questo insieme, non sull'intera
collezione.

**Rischio da verificare.** C-2 resta aperta: il riuso di `StoredList` non dimostra un'apertura indipendente dalla quantità dei dati. La directory dei blocchi e il salto dei payload richiedono verifica nel task dell'algoritmo.

**Fuori ambito del giro.** Compressione e quantizzazione, fusione con il punteggio denso, superfici di rete, calcolo dei pesi nel server.

**Tramandato al ciclo di elicitazione.**

- Acquisito per il primo incremento: blocco serializzabile in `StoredList`, riferimenti ai documenti indipendenti dalle posizioni dense, indice del vettore sparso dentro il documento.
- Aperto per l'integrazione: indirizzamento dei termini e dei blocchi, forma pubblica dei vettori sparsi, aggiornamenti e recupero.

**Handoff.** Sviluppo del primo incremento autorizzato direttamente, senza chiusura formale di analisi e architettura. Stato ed evidenze in [Task execution](../development/dossier.md#task-execution).
