# Analysis summary

**Verdetto: analisi completa e pronta per l'architettura** (secondo giro sul codice al commit `b706e06`, più l'incremento dei vettori sparsi).

## Scope

Dentro l'analisi:

- Vocabolario del dominio con sinonimi vietati.
- Sei attori, ventuno flussi, dodici regole di validazione, quarantuno requisiti con criterio di accettazione.
- Nove componenti con responsabilità unica e sette confini con la proprietà dei dati.
- Un modello canonico con tredici entità e sette viste derivate.
- Ventuno casi d'uso con azioni accoppiate e percorsi esplorati.
- Diciotto pacchetti funzionali con dipendenze e verifica.

Fuori dall'analisi: tecnologie, algoritmi, valori predefiniti, firme e codici di errore. Restano decisioni di architettura.

## Risultati principali

- Il prototipo non è stato eseguito: l'esperienza è quella del sistema in produzione. Vedi [Functional analysis](dossier.md#functional-analysis) (dossier.md#functional-analysis).
- Il ciclo non ha domande funzionali aperte. Le lacune rilevate sono difetti del sistema attuale, non requisiti mancanti.
- Il modello canonico riduce a una entità per concetto: `VectorEntry`, `Document` e `Vector` diventano Voce con vettore facoltativo. Vedi [Data model simplification](dossier.md#data-model-simplification) (dossier.md#data-model-simplification).
- Più vettori per documento non sono una entità: sono N voci con un record condiviso. Vedi la stessa sezione.
- Ogni azione dell'interfaccia è accoppiata a una operazione e a una superficie, o dichiarata locale. Vedi [Use-case catalogue](../use-cases.md) (use-cases.md).
- Il confine distribuito nasce da un solo criterio: il calcolo dei vettori cresce separatamente, l'archivio resta unitario. Vedi [Data ownership partition across the boundaries](diagrams.md#5-data-ownership-partition-across-the-boundaries) (diagrams.md#5-data-ownership-partition-across-the-boundaries).
- Il vettore sparso è un secondo slot della Voce, non una seconda entità: R-36…R-41 aggiungono capacità di ricerca lessicale senza duplicare il modello. Vedi [Data model simplification](dossier.md#data-model-simplification) (dossier.md#data-model-simplification).
- La ricerca sparsa è esatta finché la potatura è spenta; con un filtro attivo la potatura non è ammessa. È una garanzia dichiarata, non un dettaglio realizzativo.

## Correzioni di questo giro

| Correzione | Effetto |
| --- | --- |
| I vettori di immagine non hanno una superficie REST | F-07 e UC-07 passano a S-01 e S-03 |
| La fusione cross-modale del client non aveva requisito | Aggiunti F-18, R-35 e UC-18, tracciati su T-12 |
| L'interfaccia ha una sola lingua, non due | Corretto l'attributo di qualità e il riferimento all'esperienza |
| Vocabolario dell'interfaccia in italiano, ma le etichette sono inglesi | Riscritto con le etichette effettivamente mostrate |
| Collegamenti relativi errati fra dossier, catalogo e riferimento | Corretti |
| Vettori sparsi assenti dal modello e dai requisiti | Aggiunti R-36…R-41, un secondo slot vettoriale, l'Elenco dei termini, tre casi d'uso e tre pacchetti |

## Rischi e questioni aperte

- Richiamo su dati reali non misurato: è la domanda P2 dello scouting, che fissa i valori predefiniti dell'indice.
- `POST /api/database/{db}/collections/search-filter` accetta un filtro e lo ignora. Nessun requisito la copre.
- Le tre rotte di menu chiamate dall'interfaccia non esistono nel server.
- R-30, astrazione di provider per l'ecosistema .NET, non ha implementazione nel repository.
- Accessibilità dell'interfaccia mai verificata.
- La memoria dell'indice dei termini non è misurata: il limite dichiarabile dipende dalla prova U1 dello scouting dedicato.
- La potatura della ricerca sparsa può perdere risultati ammessi: l'esattezza è dichiarata solo senza potatura o con filtro.

## Prossime azioni

| Azione | Attore |
| --- | --- |
| Chiudere questi tre difetti in architettura: rimozione di `search-filter`, menu statico, credenziali predefinite | Architettura |
| Fissare stack, vincoli, forme e ambienti | Architettura |
| Congelare superfici, navigazione, stati e vocabolario | Architettura |
| Chiudere l'incremento dei vettori sparsi in architettura: indice dei termini, esattezza, fusione | Architettura |
| Eseguire le prove U1, U2, U3 e U4 dello [scouting dedicato](../scouting/sparse-vectors/dossier.md#unknowns-plan) (scouting/sparse-vectors/dossier.md#unknowns-plan) | Sviluppo e maintainer |

## Diagrammi di riferimento

- [Requirements flows and surfaces](diagrams.md#3-requirements-flows-and-surfaces) (diagrams.md#3-requirements-flows-and-surfaces)
- [Canonical data model](diagrams.md#6-canonical-data-model) (diagrams.md#6-canonical-data-model)
- [Service data flows grouped by deployment](diagrams.md#12-service-data-flows-grouped-by-deployment) (diagrams.md#12-service-data-flows-grouped-by-deployment): incluso.

Il dossier completo è in [Analysis dossier](dossier.md) (dossier.md). Il verdetto è in [Analysis handoff](dossier.md#analysis-handoff) (dossier.md#analysis-handoff).
