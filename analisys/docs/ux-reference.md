# Experience reference

Riferimento delle superfici dell'applicazione. Il documento è la base che lo sviluppo consuma senza reinventare.

**Stato.** Congelato dall'architettura nel giro 2 (2026-10-21). Una superficie, un punto di ingresso, uno stato o un'etichetta nuovi richiedono un ritorno all'analisi.

**Stato della direzione.** Non esiste un prototipo esplorato. La direzione di ciascuna superficie è quella attualmente in uso, osservata nel sistema esistente. Non è una direzione validata da un'esplorazione: è la direzione di partenza. Una modifica futura va registrata con il proprio motivo in questo documento.

## Superfici

| Id | Superficie | Ruolo | Punto di ingresso | Navigazione |
| --- | --- | --- | --- | --- |
| S-01 | API in-process | Uso del database dentro un processo | Costruzione del database nel codice applicativo | Nessuna: è una libreria |
| S-02 | Superficie REST | Amministrazione e operazioni da HTTP | `/api/...` e `/scalar` per la documentazione interattiva | Nessuna |
| S-03 | Superficie binaria | Operazioni tipizzate da client | Endpoint del servizio su porta dedicata | Nessuna |
| S-04.1 | Panoramica | Stato recente del server | Voce «Overview» nel menu | Panoramica verso database, collezioni, workbench |
| S-04.2 | Database | Elenco, creazione, dettaglio, accessi | Voce «Databases» nel menu | Database verso collezioni e sicurezza |
| S-04.3 | Collezioni | Elenco e dettaglio delle collezioni | Voce «Collections» nel menu | Collezioni verso esploratore del grafo e workbench |
| S-04.4 | Workbench | Prova di una ricerca | Voce «Workbench» nel menu, oppure dal dettaglio di una collezione | Workbench verso collezioni |
| S-04.5 | Esploratore del grafo | Struttura dell'indice | Voce «Graph Explorer» nel menu, oppure dal dettaglio di una collezione | Esploratore verso collezioni |
| S-04.6 | Sicurezza | Utenti e ruoli | Voce «Security» nel menu | Sicurezza verso database |
| S-04.7 | Accesso | Autenticazione | Rotta di accesso, obbligatoria per ogni altra superficie | Accesso verso la superficie richiesta |
| S-04.8 | Impostazioni | Segnaposto senza funzioni | Voce «Settings» nel menu | Nessuna |
| S-05 | Artefatti di distribuzione | Installazione e avvio | Pacchetto o immagine | Nessuna |

## Superfici e stati

Gli stati di caricamento, vuoto e inattivo appartengono a questo documento. L'esito di un percorso, compresi rifiuto ed errore, appartiene al [Use-case catalogue](../use-cases.md) (use-cases.md).

| Superficie | Flusso | Caricamento | Vuoto | Inattivo |
| --- | --- | --- | --- | --- |
| S-04.1 Panoramica | UC-15 | Scheletro dei grafici con la finestra selezionata | Nessun database: indicatori a zero e messaggio | Finestra selezionata conservata fino al cambio |
| S-04.2 Database | UC-09, UC-10 | Tabella con righe segnaposto | «Nessun database» con invito a crearne uno | Pannello di creazione chiuso; dettaglio chiuso |
| S-04.3 Collezioni | UC-11 | Tabella con righe segnaposto | Nessun database scelto, oppure database senza collezioni | Collezione non selezionata |
| S-04.4 Workbench | UC-13 | Pulsante di invio in attesa | Nessun risultato dopo l'invio | Campi compilati conservati |
| S-04.5 Esploratore del grafo | UC-12 | Area del grafo con indicatore | Collezione senza indice: messaggio esplicito | Evidenziazione assente |
| S-04.6 Sicurezza | UC-14 | Tabelle con righe segnaposto | Nessun utente, nessun ruolo | Scheda utenti attiva |
| S-04.7 Accesso | — | Pulsante di accesso in attesa | — | Campi vuoti |
| S-04.8 Impostazioni | — | — | Segnaposto | — |

## Direzione per superficie

La direzione si registra come scelte risolte, non come valori.

| Superficie | Composizione | Densità | Gerarchia | Elemento distintivo |
| --- | --- | --- | --- | --- |
| S-04.1 Panoramica | Indicatori in alto, grafici sotto, selettore di finestra nell'intestazione | Compatta | Indicatore numerico prima del grafico | Selettore di finestra a quattro voci |
| S-04.2 Database | Tabella unica con azioni in riga, pannello laterale per il dettaglio | Compatta | Nome del database prima delle dimensioni | Riquadro di dettaglio con sezione utenti |
| S-04.3 Collezioni | Selettore di database in alto, tabella sotto, dettaglio laterale | Compatta | Collezione prima delle metriche dell'indice | Due collegamenti dal dettaglio verso workbench e grafo |
| S-04.4 Workbench | Modulo a righe, risultati sotto | Arieggiata | Selettori prima del risultato | Tempi di fase mostrati accanto al risultato |
| S-04.5 Esploratore del grafo | Grafo a tutta area con controlli sovrapposti | Arieggiata | Grafo prima dei controlli | Evidenziazione dei nodi corrispondenti |
| S-04.6 Sicurezza | Schede per utenti e ruoli, tabella per scheda | Compatta | Scheda prima della tabella | Doppia scheda con tabelle omogenee |
| S-04.7 Accesso | Modulo centrato, una colonna | Arieggiata | Modulo prima del messaggio | Accesso con nome utente e password |
| S-04.8 Impostazioni | Segnaposto | — | — | Assente per decisione: nessuna funzione |

## Vocabolario dell'interfaccia

Etichette effettivamente mostrate, allineate al glossario. La lingua dell'interfaccia è una sola: inglese (`en-us`).

| Etichetta mostrata | Significato | Dove compare |
| --- | --- | --- |
| Database | Insieme di file sotto un nome | Menu, titolo di sezione, colonna |
| Collection | Gruppo di voci | Menu, titolo di sezione, colonna |
| Document | Unità con contenuto e vettore | Colonna e azioni; è l'etichetta di «Voce» nell'interfaccia |
| Key | Identificatore della voce | Colonna e campo |
| Vector | Sequenza di numeri | Colonna e campo |
| Dimensions | Lunghezza del vettore | Colonna |
| Vector size | Byte occupati dai vettori | Colonna |
| Free space | Spazio recuperabile | Colonna |
| Max level | Profondità del grafo | Colonna |
| Deleted | Nodi cancellati logicamente | Colonna |
| Quantization | Riduzione di precisione del grafo | Colonna |
| Window | Intervallo temporale dei campioni | Selettore |
| User | Identità autenticata | Titolo di scheda |
| Role | Insieme di permessi | Titolo di scheda |

Nessuna superficie mostra i record condivisi: il termine non ha etichetta.

## Divergenze registrate

| Divergenza | Motivo |
| --- | --- |
| La pagina impostazioni non ha funzioni | Nessun requisito la richiede. Resta come segnaposto per non rimuovere una voce di menu già pubblicata. |
| L'interfaccia non espone i record condivisi né la ricerca raggruppata | I requisiti R-10 e R-11 sono raggiungibili solo dalle superfici di programmazione. La lacuna è dichiarata, non risolta. |
| Il menu si carica da tre rotte inesistenti nel server | Residuo del modello di partenza dell'interfaccia. Va corretto dall'architettura. |
| La superficie `search-filter` accetta un filtro e lo ignora | Difetto dichiarato. Nessun caso d'uso la copre. |
| Non esiste una direzione validata da un prototipo | Non c'è stato prototipo. La direzione attuale è il punto di partenza, non un esito di validazione. |
| L'interfaccia mostra «Document» per la voce | Etichetta già pubblicata. Il termine di dominio resta «Voce» nel codice e nei documenti; l'etichetta non introduce un secondo concetto. |
| L'interfaccia ha una sola lingua, inglese | Nessuna seconda lingua nel codice. L'obiettivo di due lingue non è coperto da un requisito. |
