# Architecture summary

**Verdetto: architettura pronta per lo sviluppo.** Stack, vincoli, forme, ambienti e riferimento all'esperienza sono decisi. Lo sviluppo esegue e non riapre; una deviazione torna qui.

## Scope

Deciso in questa fase:

- Stack completo per ogni slot richiesto dall'analisi, con versioni e criterio.
- Trentaquattro vincoli verificabili: piattaforma, dati, prestazioni, sicurezza, dati personali, interfaccia, test, licenze.
- Sedici forme, dalla composizione dei moduli alla fusione cross-modale nel client.
- Tre topologie di distribuzione con la proprietà dei dati per unità.
- Cinque ambienti, politica di configurazione, segreti e dati, percorso di promozione.
- Congelamento delle superfici, della navigazione, degli stati e del vocabolario dell'interfaccia.

Fuori da questa fase: la scrittura del codice, le schede di task per attore e la correzione dei difetti trovati.

## Risultati principali

- Lo stack è quello che il codice già usa: .NET 10, Jigen.Store, Jigen.Indexer.HNSW, ONNX Runtime 1.27, gRPC e REST sullo stesso dispatcher, RabbitMQ per la delega, ASP.NET Core Identity con OpenIddict, Vue 3 con Naive UI. Vedi [Stack Profile](StackProfile.md) (StackProfile.md).
- Le tre topologie rispondono al solo criterio distribuito registrato: il calcolo dei vettori cresce separatamente, l'archivio resta unitario. Vedi [Deployable units and data ownership](diagrams.md#4-deployable-units-and-data-ownership) (diagrams.md#4-deployable-units-and-data-ownership).
- Un solo proprietario per dato anche nella forma distribuita; le copie derivate sono quattro e dichiarate. Vedi [Service data flows grouped by deployment](diagrams.md#13-service-data-flows-grouped-by-deployment) (diagrams.md#13-service-data-flows-grouped-by-deployment).
- La superficie REST `search-filter` è rimossa: nessun requisito la copre e il filtro resta su API in-process e superficie binaria. Vedi [Pattern decisions](dossier.md#pattern-decisions) (dossier.md#pattern-decisions).
- Il menu dell'interfaccia è statico: le tre chiamate a rotte inesistenti spariscono. Vedi [Technical constraints](dossier.md#technical-constraints) (dossier.md#technical-constraints), K-27.
- Il registro di scrittura passa da spento a attivo per default nel server, con durabilità a gruppi. Vedi K-06.

## Rischi e questioni aperte

- Il richiamo su dati reali non è misurato: il valore predefinito dell'indice resta la scommessa P2 dello scouting. K-13 fissa la soglia, non il valore.
- Due obblighi di prodotto non sono coperti dal codice attuale: il provider `Microsoft.Extensions.VectorData` (R-30) e la SPA confezionata dentro `wwwroot`.
- Le credenziali predefinite sono pubblicate nella configurazione: finché K-17 non è realizzato, un'immagine avviata così com'è è esposta.
- L'accessibilità non è mai stata verificata: K-25 fissa l'obiettivo, nessun controllo esiste.
- La lingua dell'interfaccia è una sola. L'obiettivo di due lingue non è coperto da requisito ed esce dal ciclo.

## Prossime azioni

| Azione | Attore |
| --- | --- |
| Aprire la pianificazione per attore sui pacchetti T-01…T-15 | Sviluppo |
| Abilitare il registro di scrittura per default e coprirlo con test di arresto non pulito | Sviluppo |
| Confezionare la SPA in `wwwroot` e servirla dalla stessa origine | Sviluppo |
| Rimuovere `search-filter` e le tre chiamate di menu | Sviluppo |
| Introdurre il controllo delle credenziali predefinite e l'elenco CORS configurabile | Sviluppo |
| Misurare richiamo e latenza su 100.000 embedding reali | Maintainer |

## Diagrammi di riferimento

- [Stack at a glance](diagrams.md#3-stack-at-a-glance) (diagrams.md#3-stack-at-a-glance)
- [Module and layering shape](diagrams.md#5-module-and-layering-shape) (diagrams.md#5-module-and-layering-shape)
- [Search path](diagrams.md#7-search-path) (diagrams.md#7-search-path)
- [Service data flows grouped by deployment](diagrams.md#13-service-data-flows-grouped-by-deployment) (diagrams.md#13-service-data-flows-grouped-by-deployment): incluso.

Il dossier completo è in [Architecture dossier](dossier.md) (dossier.md). Il verdetto e ciò che lo sviluppo non deve riaprire sono in [Architecture handoff](dossier.md#architecture-handoff) (dossier.md#architecture-handoff).
