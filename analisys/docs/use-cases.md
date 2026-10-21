# Use-case catalogue

Catalogo dei casi d'uso del sistema. Ogni caso d'uso accoppia le azioni di interfaccia alle operazioni di backend e alle superfici che le espongono, ed esplora ogni percorso fino al suo esito.

Superfici: **S-01** API in-process, **S-02** superficie REST, **S-03** superficie binaria, **S-04** interfaccia amministrativa, **S-05** artefatti di distribuzione.

## UC-01 Aprire un database embedded

- Attore: A1.
- Obiettivo: usare il database dentro il proprio processo.
- Punto di ingresso: costruzione del database sul percorso dati.
- Precondizioni: percorso scrivibile; nessun altro processo ha il database aperto.

| Azione | Accoppiamento | Superficie | Stato dopo la chiamata |
| --- | --- | --- | --- |
| Costruire il database | Locale: apre i file e acquisisce il lock | S-01 | Database aperto e pronto |
| Chiudere il database | Locale: svuota la coda e rende durevole | S-01 | Database chiuso, lock rilasciato |

Percorsi:

| Percorso | Innesto | Risultato osservabile | Comportamento del backend |
| --- | --- | --- | --- |
| Principale | Costruzione su percorso valido | Il database è pronto; le collezioni esistenti sono elencate | Apre i file, ricostruisce l'indice di posizione, riprende il registro |
| Alternativo | Percorso inesistente | Il database viene creato vuoto | Crea i file e il catalogo interno |
| Eccezione | Percorso occupato da un altro processo | Errore di lock, nessun database aperto | Rifiuta l'apertura senza modificare i file |
| Eccezione | Percorso non scrivibile | Errore di accesso, nessun database aperto | Rifiuta l'apertura |
| Eccezione | Arresto precedente non pulito | Il database si apre e l'indice è riconciliato | Riprende il registro e riconcilia l'indice |
| Interruzione | Il processo termina senza chiusura | Le scritture durevoli restano; le altre sono ricuperabili dal registro | Il registro resta sul disco |

Criteri di accettazione: la seconda apertura fallisce; la prima resta operativa; dopo il riavvio le voci sono leggibili.

## UC-02 Scrivere o aggiornare voci

- Attori: A1, A2.
- Obiettivo: inserire contenuti e vettori.
- Punto di ingresso: scrittura di una voce, di un blocco o di una transazione.

| Azione | Operazione | Superficie | Stato dopo la chiamata |
| --- | --- | --- | --- |
| Scrivere una voce con vettore | Operazione di scrittura con vettore | S-01, S-02, S-03 | Accettata, poi persistita |
| Scrivere una voce con testo | Operazione di scrittura con testo, che calcola il vettore | S-02, S-03 | Accettata, poi persistita |
| Scrivere un blocco di voci | Operazione di scrittura in blocco, con flusso in ingresso | S-02, S-03 | Numero di voci accettate |
| Aggiungere a una voce esistente | Operazione di aggiunta su chiave esistente | S-02, S-03 | Contenuto sostituito sotto la stessa chiave |
| Aprire una transazione | Operazione di apertura | S-02, S-03 | Transazione aperta |
| Confermare una transazione | Operazione di conferma | S-02, S-03 | Tutte le operazioni visibili |
| Annullare una transazione | Operazione di annullamento | S-02, S-03 | Nessuna operazione visibile |

Percorsi:

| Percorso | Innesto | Risultato osservabile | Comportamento del backend |
| --- | --- | --- | --- |
| Principale | Voce con vettore della dimensione attesa | La voce è leggibile e ricercabile | Accoda la scrittura, il writer la persiste, l'indice la riceve |
| Alternativo | Voce senza vettore | La voce è leggibile ma non compare nelle ricerche per similarità | Registra il contenuto senza vettore |
| Alternativo | Chiave già presente | Il conteggio non cresce; contenuto e vettore sono sostituiti | Scrive una nuova versione e aggiorna l'indice di posizione |
| Eccezione | Dimensione del vettore incoerente con la collezione | Rifiuto con messaggio esplicito | Non scrive nulla |
| Eccezione | Testo da calcolare senza calcolo dei vettori disponibile | Rifiuto con messaggio di configurazione | Non scrive nulla |
| Eccezione | Coda di ingestione piena | Rifiuto per timeout | Non scrive nulla |
| Eccezione | Transazione senza conferma | Nessun effetto durevole | Trascina il registro prima dell'apertura |
| Interruzione | Il client abbandona il flusso in blocco | Le voci già accettate restano | Il server chiude il flusso al termine del messaggio |

Criteri di accettazione: dopo la conferma tutte le operazioni sono visibili; dopo l'annullamento nessuna.

## UC-03 Cercare per similarità

- Attori: A1, A2.
- Obiettivo: ottenere i vicini più simili a una query.
- Punto di ingresso: ricerca per vettore o per testo.

| Azione | Operazione | Superficie | Stato dopo la chiamata |
| --- | --- | --- | --- |
| Cercare con un vettore | Ricerca per vettore | S-01, S-02, S-03 | Risultati ordinati per punteggio |
| Cercare con un testo | Calcolo del vettore, poi ricerca | S-02, S-03 | Risultati come sopra |
| Cercare in più collezioni | Ricerca per collezione ripetuta, poi fusione e ordinamento | S-02 | Risultati per collezione e risultato fuso |
| Cercare per esempio | Lettura del vettore di una voce, poi ricerca | S-02, S-03 | Risultati vicini alla voce data |

Percorsi:

| Percorso | Innesto | Risultato osservabile | Comportamento del backend |
| --- | --- | --- | --- |
| Principale | Collezione con voci indicizzate | Al più `top` risultati con chiave, contenuto e punteggio | Visita il grafo o confronta tutti i vettori |
| Alternativo | `top` maggiore delle voci disponibili | Restituisce tutte le voci | Nessun errore |
| Alternativo | Collezione vuota | Lista vuota | Nessun errore |
| Alternativo | Ricerca in più collezioni | Un risultato per collezione più l'elenco fuso | Esegue le ricerche, fonde e ordina |
| Eccezione | Né testo né vettore | Rifiuto `400` | Non esegue la ricerca |
| Eccezione | Testo con calcolo non disponibile | Rifiuto con messaggio di configurazione | Non esegue la ricerca |
| Eccezione | Collezione inesistente | Lista vuota o errore secondo la superficie | Nessuna voce trovata |
| Interruzione | Il client chiude la connessione | La ricerca si annulla | Rilascia il lavoro |

Criteri di accettazione: nessun risultato supera `top`; l'ordine è per punteggio decrescente.

## UC-04 Cercare con filtro

- Attori: A1, A2.
- Obiettivo: restringere i risultati a un sottoinsieme.
- Punto di ingresso: ricerca con predicato.

| Azione | Operazione | Superficie | Stato dopo la chiamata |
| --- | --- | --- | --- |
| Filtrare per uguaglianza | Ricerca con predicato | S-01, S-03 | Solo voci corrispondenti |
| Filtrare per appartenenza | Ricerca con predicato su collezione | S-01, S-03 | Solo voci che contengono il valore |
| Combinare condizioni | Ricerca con predicato composto | S-01, S-03 | Solo voci che soddisfano la combinazione |

Percorsi:

| Percorso | Innesto | Risultato osservabile | Comportamento del backend |
| --- | --- | --- | --- |
| Principale | Filtro poco selettivo | `top` risultati corrispondenti | Valuta il predicato durante la visita |
| Alternativo | Filtro molto selettivo | Meno di `top` risultati, senza errore | Espande la visita fino al limite dichiarato |
| Alternativo | Nessuna voce corrisponde | Lista vuota | Termina al limite di espansione |
| Eccezione | Operatore non supportato (`<`, `>`, `!=`, `Contains`) | Rifiuto con messaggio esplicito | Non esegue la ricerca |
| Eccezione | Percorso di proprietà non esistente | Nessuna voce corrispondente | Il predicato è falso |
| Eccezione | Filtro inviato alla superficie `search-filter` | La superficie ignora il filtro e restituisce tutte le voci | Difetto noto: la superficie non è coperta da requisito |

Criteri di accettazione: nessun risultato viola il predicato; gli operatori non supportati sono rifiutati.

## UC-05 Raggruppare per record condiviso

- Attori: A1, A2.
- Obiettivo: un risultato per unità di contenuto, non per vettore.
- Punto di ingresso: ricerca raggruppata.

| Azione | Operazione | Superficie | Stato dopo la chiamata |
| --- | --- | --- | --- |
| Creare un record | Operazione di creazione record | S-01, S-02, S-03 | Indice del record restituito |
| Associare una voce al record | Operazione di associazione | S-01, S-02, S-03 | La voce legge il contenuto condiviso |
| Cercare raggruppando | Ricerca raggruppata con budget | S-01, S-02, S-03 | Al più `top` gruppi |
| Aggiornare il record | Operazione di aggiornamento record | S-01, S-02, S-03 | Tutte le voci associate leggono il nuovo contenuto |
| Leggere l'associazione di una voce | Operazione di lettura associazione | S-02, S-03 | Indice del record, oppure assenza |

Percorsi:

| Percorso | Innesto | Risultato osservabile | Comportamento del backend |
| --- | --- | --- | --- |
| Principale | Budget sufficiente | `top` gruppi, ognuno con indice, chiave vincente e punteggio massimo | Raccoglie candidati e raggruppa |
| Alternativo | Budget esaurito | Meno di `top` gruppi, senza errore | Interrompe la raccolta |
| Alternativo | Più voci sullo stesso record | Un solo gruppo | Tiene il punteggio migliore |
| Alternativo | Pareggio di punteggio | Ordine stabile per indice del record | Ordina per indice |
| Eccezione | Budget assente o zero | Rifiuto con messaggio esplicito | Non esegue la ricerca |
| Eccezione | Associazione a chiave inesistente | Rifiuto con messaggio esplicito | Non associa |
| Eccezione | Associazione a indice sconosciuto | Rifiuto con messaggio esplicito | Non associa |
| Eccezione | Record cancellato ma non compattato | La voce non è leggibile ma il record resta | Conserva il record e l'indice |
| Interruzione | Il client abbandona | Nessuna associazione parziale | La scrittura è atomica per voce |

Criteri di accettazione: un gruppo per record; nessun gruppo con punteggio inferiore al massimo delle sue voci.

## UC-06 Generare vettori di testo

- Attori: A1, A2, A3.
- Obiettivo: ottenere vettori da testo.
- Punto di ingresso: richiesta di calcolo.

| Azione | Operazione | Superficie | Stato dopo la chiamata |
| --- | --- | --- | --- |
| Calcolare un vettore | Operazione di calcolo singolo, con task e profilo | S-01, S-02, S-03 | Vettore restituito |
| Calcolare un blocco | Operazione di calcolo in blocco | S-02, S-03 | Vettori nello stesso ordine degli ingressi |
| Calcolare su più profili | Operazione di calcolo multi-profilo | S-02 | Dizionario per nome di profilo |
| Elencare i task disponibili | Operazione di lettura dei task | S-02 | Elenco dei prefissi supportati |

Percorsi:

| Percorso | Innesto | Risultato osservabile | Comportamento del backend |
| --- | --- | --- | --- |
| Principale | Testo breve | Vettore della dimensione del profilo | Tokenizza, inferisce, estrae e normalizza |
| Alternativo | Testo oltre il limite | Vettore ottenuto per blocchi o per testa-coda | Applica la strategia configurata |
| Alternativo | Blocco con ingressi vuoti | Righe vuote in posizione, risultati per gli altri | Filtra i vuoti e ripristina le posizioni |
| Alternativo | Profilo senza prefisso | Vettore con il task predefinito | Usa il default del profilo |
| Eccezione | Profilo sconosciuto | Rifiuto con messaggio esplicito | Non calcola |
| Eccezione | Coda piena oltre il timeout | Rifiuto per timeout | Non calcola |
| Eccezione | Calcolo non configurato | Rifiuto con messaggio di configurazione | Non calcola |
| Interruzione | Il chiamante annulla | Il chiamante torna subito; il lavoro in corso termina | Rilascia la richiesta dalla coda se non avviata |

Criteri di accettazione: la dimensione del vettore coincide con quella del profilo; l'ordine del blocco è preservato.

## UC-07 Generare vettori di immagine

- Attori: A2, A3.
- Obiettivo: ottenere vettori da immagini.
- Punto di ingresso: richiesta di calcolo immagine.

| Azione | Operazione | Superficie | Stato dopo la chiamata |
| --- | --- | --- | --- |
| Calcolare un vettore immagine | Operazione di calcolo immagine | S-01, S-03 | Vettore restituito |
| Calcolare un blocco di immagini | Operazione di calcolo in blocco | S-03 | Vettori nello stesso ordine |
| Calcolare le tile | Operazione di calcolo tile | S-03 | Vettori delle tile più quello dell'immagine intera in coda |

Percorsi:

| Percorso | Innesto | Risultato osservabile | Comportamento del backend |
| --- | --- | --- | --- |
| Principale | Immagine supportata | Vettore della dimensione attesa | Decodifica, ridimensiona, normalizza, inferisce |
| Alternativo | Immagine molto grande | Più tile più il vettore globale | Divide in tile sovrapposte |
| Alternativo | Lotto misto | Un vettore per immagine | Elabora in batch |
| Eccezione | Formato non supportato | Rifiuto con messaggio esplicito | Non calcola |
| Eccezione | Modello immagine non configurato | Rifiuto con messaggio di configurazione | Non calcola |
| Eccezione | Byte non decodificabili | Rifiuto con messaggio esplicito | Non calcola |
| Interruzione | Il chiamante annulla | Torna subito | Rilascia la richiesta |

Criteri di accettazione: l'ultimo vettore della modalità tile è quello dell'immagine intera.

## UC-08 Dividere un testo in passaggi

- Attori: A2, A3.
- Obiettivo: ottenere passaggi sovrapposti con i loro vettori.
- Punto di ingresso: richiesta di passaggi.

| Azione | Operazione | Superficie | Stato dopo la chiamata |
| --- | --- | --- | --- |
| Contare i passaggi | Operazione di conteggio | S-02, S-03 | Numero di passaggi |
| Calcolare i passaggi | Operazione di calcolo passaggi | S-02, S-03 | Passaggi numerati con vettore |

Percorsi:

| Percorso | Innesto | Risultato osservabile | Comportamento del backend |
| --- | --- | --- | --- |
| Principale | Parametri validi | Passaggi ordinati con vettori | Divide e calcola in un solo passaggio |
| Alternativo | Parametri assenti | Usa i valori predefiniti | Applica la configurazione |
| Alternativo | Testo più corto di un passaggio | Un solo passaggio | Restituisce l'intero testo |
| Eccezione | Dimensione fuori intervallo | Rifiuto `400` con il limite | Non divide |
| Eccezione | Sovrapposizione maggiore o uguale alla dimensione | Rifiuto `400` | Non divide |
| Eccezione | Calcolo non disponibile | Rifiuto con messaggio di configurazione | Non calcola |
| Interruzione | Il chiamante annulla | Torna subito | Rilascia la richiesta |

Criteri di accettazione: il conteggio coincide con il numero di passaggi restituiti.

## UC-09 Gestire i database del server

- Attore: A3.
- Obiettivo: creare, vedere ed eliminare database.
- Punto di ingresso: voce «Databases» nel menu dell'interfaccia, oppure superficie REST.

| Azione | Operazione | Superficie | Stato dopo la chiamata |
| --- | --- | --- | --- |
| Aprire l'elenco | Elenco dei database leggibili dall'utente | S-04, S-02 | Tabella popolata |
| Creare un database | Creazione con nome | S-04, S-02 | Il database appare nell'elenco |
| Aprire il dettaglio | Lettura dettagli | S-04, S-02 | Dimensioni, collezioni e utenti |
| Eliminare un database | Eliminazione con scelta sui file | S-04, S-02 | Il database scompare |
| Ricaricare l'elenco | Nuova lettura | S-04 | Tabella aggiornata |

Percorsi:

| Percorso | Innesto | Risultato osservabile | Comportamento del backend |
| --- | --- | --- | --- |
| Principale | Nome valido | Il database esiste e si apre | Crea i file, registra nel catalogo, apre il database |
| Alternativo | Eliminazione senza file | Il database sparisce e i file restano | Rimuove dal catalogo e chiude il database |
| Alternativo | Eliminazione con file | Il database sparisce e i file sono rimossi | Rimuove dal catalogo, chiude e cancella |
| Alternativo | Utente senza database | Stato vuoto con invito a crearne uno | Elenco filtrato |
| Eccezione | Nome non valido | Messaggio sul campo, nessuna chiamata | Nessuna |
| Eccezione | Nome già esistente | Errore visibile, elenco invariato | Rifiuta la creazione |
| Eccezione | Database non accessibile all'utente | `404`, nessun dettaglio | Non rivela l'esistenza |
| Eccezione | Eliminazione negata | `404` o `403`, elenco invariato | Nessuna modifica |
| Interruzione | Chiusura della finestra durante l'eliminazione | L'elenco al rientro riflette lo stato reale | L'operazione è idempotente al riavvio |

Criteri di accettazione: creazione ed eliminazione sopravvivono al riavvio del server.

## UC-10 Assegnare utenti a un database

- Attore: A3.
- Obiettivo: decidere chi legge un database.
- Punto di ingresso: pannello utenti nel dettaglio del database.

| Azione | Operazione | Superficie | Stato dopo la chiamata |
| --- | --- | --- | --- |
| Aprire il pannello | Lettura degli utenti del database e di tutti gli utenti | S-04, S-02 | Selezione multipla popolata |
| Selezionare utenti | Locale | S-04 | Nessuna chiamata |
| Salvare | Sostituzione dell'insieme degli utenti | S-04, S-02 | Elenco aggiornato |
| Rimuovere tutti | Sostituzione con insieme vuoto | S-04, S-02 | Nessun utente associato |

Percorsi:

| Percorso | Innesto | Risultato osservabile | Comportamento del backend |
| --- | --- | --- | --- |
| Principale | Insieme completo inviato | Gli utenti vedono il database | Sostituisce l'insieme e lo persiste |
| Alternativo | Nessuna modifica | Nessuna chiamata | Nessuna |
| Eccezione | Utente sconosciuto nell'insieme | Rifiuto con messaggio esplicito | Nessuna modifica |
| Eccezione | Chiamante non amministratore | `403` | Nessuna modifica |
| Eccezione | Database inesistente | `404` | Nessuna modifica |
| Interruzione | Navigazione altrove prima del salvataggio | Le modifiche locali sono perse | Nessuna modifica |

Criteri di accettazione: dopo il salvataggio l'utente vede il database; dopo la rimozione non lo vede.

## UC-11 Esplorare collezioni e voci

- Attore: A3.
- Obiettivo: vedere cosa contiene un database e leggere una voce.
- Punto di ingresso: voce «Collections» nel menu.

| Azione | Operazione | Superficie | Stato dopo la chiamata |
| --- | --- | --- | --- |
| Scegliere il database | Elenco dei database | S-04, S-02 | Collezioni caricate |
| Aprire una collezione | Informazioni di collezione | S-04, S-02 | Metriche dell'indice |
| Aprire una voce | Lettura del contenuto | S-04, S-02 | Contenuto mostrato |
| Modificare il contenuto | Scrittura sulla stessa chiave | S-04, S-02 | Contenuto sostituito |
| Eliminare una voce | Eliminazione per chiave | S-04, S-02 | Voce assente |
| Aprire il workbench | Navigazione con database e collezione preselezionati | S-04 | Nessuna chiamata |
| Aprire l'esploratore del grafo | Navigazione con database e collezione preselezionati | S-04 | Nessuna chiamata |

Percorsi:

| Percorso | Innesto | Risultato osservabile | Comportamento del backend |
| --- | --- | --- | --- |
| Principale | Collezione con voci | Tabella con nome, voci, dimensioni, dimensioni su disco e metriche | Legge le informazioni |
| Alternativo | Database senza collezioni | Stato vuoto con messaggio | Elenco vuoto |
| Alternativo | Collezione senza indice | Messaggio «nessuna metrica» | Nessun grafo |
| Alternativo | Nessun database scelto | Messaggio di scelta | Nessuna chiamata |
| Eccezione | Chiave interpretata con il tipo sbagliato | Voce non trovata | Nessuna voce |
| Eccezione | Contenuto non deserializzabile | Errore di lettura mostrato | Restituisce i byte grezzi |
| Eccezione | Scrittura su una voce eliminata da un altro client | Conflitto visibile al ricaricamento | Scrive la nuova versione |
| Interruzione | Chiusura della finestra durante la modifica | Al rientro la voce ha il contenuto salvato | Nessuna modifica se non inviata |

Criteri di accettazione: ogni valore mostrato corrisponde a una lettura; ogni scrittura è visibile dopo il ricaricamento.

## UC-12 Esplorare il grafo dell'indice

- Attore: A3.
- Obiettivo: vedere la struttura dell'indice di una collezione.
- Punto di ingresso: voce «Graph Explorer» nel menu.

| Azione | Operazione | Superficie | Stato dopo la chiamata |
| --- | --- | --- | --- |
| Scegliere database e collezione | Snapshot del grafo | S-04, S-02 | Grafo disegnato |
| Evidenziare i nodi corrispondenti a una ricerca | Ricerca con filtro sul grafo | S-04 | Nodi evidenziati |
| Azzerare l'evidenziazione | Locale | S-04 | Nessuna chiamata |
| Ricaricare | Nuovo snapshot | S-04, S-02 | Grafo aggiornato |

Percorsi:

| Percorso | Innesto | Risultato osservabile | Comportamento del backend |
| --- | --- | --- | --- |
| Principale | Collezione indicizzata | Nodi, livelli e adiacenze disegnati | Restituisce lo snapshot |
| Alternativo | Collezione senza indice | Messaggio esplicito | Restituisce uno snapshot vuoto |
| Alternativo | Grafo molto grande | Il disegno si riduce, i controlli restano usabili | Limita i nodi restituiti |
| Eccezione | Collezione inesistente | Errore mostrato | Nessuno snapshot |
| Eccezione | Utente senza accesso | `404` | Nessuno snapshot |
| Interruzione | Navigazione altrove | Il disegno è ricostruito al rientro | Nessuna chiamata in sospeso |

Criteri di accettazione: il numero di nodi disegnati coincide con quello dello snapshot.

## UC-13 Provare una ricerca

- Attore: A3.
- Obiettivo: verificare il comportamento di una ricerca senza scrivere codice.
- Punto di ingresso: voce «Workbench» nel menu.

| Azione | Operazione | Superficie | Stato dopo la chiamata |
| --- | --- | --- | --- |
| Scegliere database e collezione | Elenco e informazioni | S-04, S-02 | Selettori popolati |
| Scegliere la modalità di query | Locale | S-04 | Nessuna chiamata |
| Impostare il numero di risultati | Locale | S-04 | Nessuna chiamata |
| Inviare la ricerca su una collezione | Ricerca su collezione | S-04, S-02 | Risultati con punteggio |
| Inviare la ricerca su più collezioni | Ricerca multi-collezione | S-04, S-02 | Risultati per collezione, fusi e tempi di fase |

Percorsi:

| Percorso | Innesto | Risultato osservabile | Comportamento del backend |
| --- | --- | --- | --- |
| Principale | Testo valido | Risultati ordinati con punteggio | Calcola il vettore e cerca |
| Alternativo | Vettore incollato | Risultati senza calcolo | Cerca direttamente |
| Alternativo | Nessun risultato | Stato vuoto | Lista vuota |
| Eccezione | Testo vuoto | Messaggio sul campo, nessuna chiamata | Nessuna |
| Eccezione | Vettore non numerico | Messaggio sul campo, nessuna chiamata | Nessuna |
| Eccezione | Calcolo dei vettori non disponibile | Errore con messaggio di configurazione | Rifiuta la ricerca per testo |
| Eccezione | Collezione inesistente | Errore visibile | Nessun risultato |
| Interruzione | Nuova ricerca durante la precedente | Vale l'ultima risposta | Annulla la precedente dove possibile |

Criteri di accettazione: i tempi di fase sono presenti nel risultato multi-collezione; nessun risultato supera il numero richiesto.

## UC-14 Amministrare utenti e ruoli

- Attore: A3.
- Obiettivo: gestire identità e permessi.
- Punto di ingresso: voce «Security» nel menu, schede utenti e ruoli.

| Azione | Operazione | Superficie | Stato dopo la chiamata |
| --- | --- | --- | --- |
| Aprire la scheda utenti | Elenco utenti | S-04, S-02 | Tabella popolata |
| Aprire il dettaglio utente | Lettura utente e ruoli | S-04, S-02 | Dettaglio mostrato |
| Creare un utente | Creazione con nome, password e ruoli | S-04, S-02 | L'utente compare nell'elenco |
| Modificare un utente | Aggiornamento | S-04, S-02 | Modifiche visibili |
| Eliminare un utente | Eliminazione | S-04, S-02 | L'utente scompare |
| Aprire la scheda ruoli | Elenco ruoli | S-04, S-02 | Tabella popolata |
| Creare, modificare ed eliminare un ruolo | Operazioni su ruolo | S-04, S-02 | Elenco aggiornato |
| Leggere gli utenti di un ruolo | Lettura per ruolo | S-04, S-02 | Elenco mostrato |

Percorsi:

| Percorso | Innesto | Risultato osservabile | Comportamento del backend |
| --- | --- | --- | --- |
| Principale | Dati validi | L'entità compare o cambia nell'elenco | Persiste la modifica |
| Alternativo | Nessun utente | Stato vuoto con invito a crearne uno | Elenco vuoto |
| Eccezione | Nome utente già esistente | Conflitto mostrato | Nessuna modifica |
| Eccezione | Ruolo in uso eliminato | La rimozione è rifiutata o le assegnazioni sono aggiornate, secondo la regola dichiarata | Nessuna assegnazione orfana |
| Eccezione | Chiamante non amministratore | `403` | Nessuna modifica |
| Eccezione | Eliminazione dell'utente corrente | Sessione terminata al ricaricamento | Elimina e invalida il token |
| Interruzione | Navigazione altrove prima del salvataggio | Modifiche locali perse | Nessuna modifica |

Criteri di accettazione: un utente creato può autenticarsi; un ruolo eliminato non compare più nelle assegnazioni.

## UC-15 Osservare lo stato del server

- Attore: A3.
- Obiettivo: leggere lo stato recente del server.
- Punto di ingresso: voce «Overview» nel menu.

| Azione | Operazione | Superficie | Stato dopo la chiamata |
| --- | --- | --- | --- |
| Aprire la panoramica | Lettura dei campioni della finestra attiva | S-04, S-02 | Indicatori e grafici |
| Cambiare finestra | Lettura dei campioni della nuova finestra | S-04, S-02 | Grafici ridisegnati |
| Ricaricare | Nuova lettura | S-04 | Grafici aggiornati |

Percorsi:

| Percorso | Innesto | Risultato osservabile | Comportamento del backend |
| --- | --- | --- | --- |
| Principale | Storico disponibile | Grafici con campioni a intervallo dichiarato | Restituisce i campioni della finestra |
| Alternativo | Server appena avviato | Meno campioni del previsto, nessun errore | Restituisce quelli disponibili |
| Alternativo | Nessun database aperto | Indicatori a zero, grafici vuoti | Nessun database nello stato |
| Eccezione | Utente senza accesso ad alcuni database | I database non accessibili non compaiono | Filtra per diritto |
| Eccezione | Lettura fallita | Messaggio di errore, dati precedenti conservati | Nessun dato |
| Interruzione | Uscita dalla pagina | Le letture periodiche si fermano | Rilascia le richieste |

Criteri di accettazione: ogni finestra restituisce campioni con l'intervallo dichiarato e nessun campione fuori intervallo.

## UC-16 Eseguire una transazione

- Attori: A1, A2.
- Obiettivo: rendere un gruppo di scritture atomico rispetto alla durabilità.
- Punto di ingresso: apertura di una transazione.

| Azione | Operazione | Superficie | Stato dopo la chiamata |
| --- | --- | --- | --- |
| Aprire | Operazione di apertura | S-01, S-02, S-03 | Identificativo di transazione |
| Aggiungere una voce | Operazione di aggiunta | S-01, S-02, S-03 | Operazione accodata |
| Eliminare una voce | Operazione di eliminazione | S-01, S-02, S-03 | Operazione accodata |
| Confermare | Operazione di conferma | S-01, S-02, S-03 | Tutte le operazioni visibili e durevoli |
| Annullare | Operazione di annullamento | S-01, S-02, S-03 | Nessuna operazione visibile |
| Abbandonare | Scadenza o chiusura | S-02, S-03 | La transazione è scartata |

Percorsi:

| Percorso | Innesto | Risultato osservabile | Comportamento del backend |
| --- | --- | --- | --- |
| Principale | Operazioni valide e conferma | Tutte le operazioni visibili | Scrive un blocco unico nel registro |
| Alternativo | Transazione vuota confermata | Nessun effetto, nessun errore | Nessun blocco |
| Eccezione | Annullamento | Nessun effetto | Scarta il buffer |
| Eccezione | Arresto prima della conferma | Nessun effetto al riavvio | Tronca il registro prima dell'apertura |
| Eccezione | Transazione annidata | Rifiuto, la lettura si ferma all'ultima posizione valida | Nessun effetto |
| Eccezione | Cancellazione di collezione dentro una transazione | Rifiuto | Nessun effetto |
| Eccezione | Registro non attivo | Rifiuto con messaggio esplicito | Nessuna transazione |
| Interruzione | Il client abbandona | La transazione scade | Scarta il buffer |

Criteri di accettazione: dopo la conferma tutte le operazioni sono visibili; dopo un arresto prima della conferma nessuna.

## UC-17 Distribuire e aggiornare

- Attore: A4.
- Obiettivo: installare e aggiornare il prodotto.
- Punto di ingresso: artefatti di distribuzione.

| Azione | Operazione | Superficie | Stato dopo la chiamata |
| --- | --- | --- | --- |
| Installare il pacchetto | Locale: nessuna chiamata | S-05 | Libreria disponibile al progetto |
| Avviare il container completo | Locale: avvio del processo | S-05 | Server raggiungibile, calcolo incluso |
| Avviare il container del server | Locale: avvio del processo | S-05 | Server raggiungibile, calcolo delegato |
| Avviare i lavoratori di calcolo | Locale: avvio del processo | S-05 | I lavoratori consumano le richieste |
| Aggiornare la versione | Locale: sostituzione degli artefatti | S-05 | Dati esistenti riletti |

Percorsi:

| Percorso | Innesto | Risultato osservabile | Comportamento del backend |
| --- | --- | --- | --- |
| Principale | Volume dati montato | Il server riapre i database esistenti | Ricostruisce gli indici di posizione |
| Alternativo | Nessun volume | Il server parte vuoto | Crea il catalogo |
| Eccezione | Porta già occupata | Avvio fallito con messaggio | Nessun processo attivo |
| Eccezione | Modelli di embedding assenti | Il server parte e rifiuta le ricerche per testo | Messaggio di configurazione |
| Eccezione | Credenziali predefinite non cambiate | Il server parte e resta esposto | Nessun blocco: rischio dichiarato |
| Eccezione | Versione con formato dati incompatibile | Avvio fallito con messaggio | Nessuna conversione silenziosa |
| Interruzione | Arresto durante la scrittura | Al riavvio il registro recupera | Riconcilia l'indice |

Criteri di accettazione: ogni topologia avvia un server raggiungibile e una chiamata di verifica risponde.

## UC-18 Combinare risultati di più modalità

- Attori: A1, A2.
- Obiettivo: ordinare insieme risultati di collezioni diverse senza che una modalità seppellisca l'altra.
- Punto di ingresso: chiamata della fusione nel codice applicativo, dopo le ricerche.

| Azione | Operazione | Superficie | Stato dopo la chiamata |
| --- | --- | --- | --- |
| Cercare in ogni modalità | Operazioni di ricerca, una per collezione | S-02, S-03 | Un elenco ordinato per modalità |
| Fondere i risultati | Locale: calibrazione per gruppo e ordinamento unico | S-01 client .NET | Un elenco unico con la modalità di provenienza |

Percorsi:

| Percorso | Innesto | Risultato osservabile | Comportamento del backend |
| --- | --- | --- | --- |
| Principale | Due gruppi con punteggi non confrontabili | Un elenco unico ordinato per punteggio calibrato | Nessuna chiamata: legge i risultati già ottenuti |
| Alternativo | Un gruppo con un solo risultato | Il suo punteggio calibrato è neutro e non altera l'ordine | Deviazione standard nulla trattata come assenza di informazione |
| Alternativo | Tre o più gruppi | Un unico ordinamento | Calibra ogni gruppo separatamente |
| Eccezione | Gruppo vuoto | Il gruppo non contribuisce all'ordine | Nessun errore |
| Eccezione | Elenco di gruppi nullo | Rifiuto immediato | Solleva un errore di argomento |
| Interruzione | Il chiamante abbandona | Nessun effetto: non c'è stato | Nessuna |

Criteri di accettazione: il migliore di ogni gruppo non finisce sistematicamente in fondo; il punteggio grezzo resta leggibile accanto a quello calibrato.
