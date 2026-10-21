# Analysis summary

**Verdetto: analisi completa e pronta per l'architettura** (secondo giro, sul codice al commit `b706e06`).

## Scope

Dentro l'analisi:

- Vocabolario del dominio con sinonimi vietati.
- Sei attori, diciotto flussi, nove regole di validazione, trentacinque requisiti con criterio di accettazione.
- Nove componenti con responsabilità unica e sette confini con la proprietà dei dati.
- Un modello canonico con dodici entità e sei viste derivate.
- Diciotto casi d'uso con azioni accoppiate e percorsi esplorati.
- Quindici pacchetti funzionali con dipendenze e verifica.

Fuori dall'analisi: tecnologie, algoritmi, valori predefiniti, firme e codici di errore. Restano decisioni di architettura.

## Risultati principali

- Il prototipo non è stato eseguito: l'esperienza è quella del sistema in produzione. Vedi [Functional analysis](dossier.md#functional-analysis) (dossier.md#functional-analysis).
- Il ciclo non ha domande funzionali aperte. Le lacune rilevate sono difetti del sistema attuale, non requisiti mancanti.
- Il modello canonico riduce a una entità per concetto: `VectorEntry`, `Document` e `Vector` diventano Voce con vettore facoltativo. Vedi [Data model simplification](dossier.md#data-model-simplification) (dossier.md#data-model-simplification).
- Più vettori per documento non sono una entità: sono N voci con un record condiviso. Vedi la stessa sezione.
- Ogni azione dell'interfaccia è accoppiata a una operazione e a una superficie, o dichiarata locale. Vedi [Use-case catalogue](../use-cases.md) (use-cases.md).
- Il confine distribuito nasce da un solo criterio: il calcolo dei vettori cresce separatamente, l'archivio resta unitario. Vedi [Data ownership partition across the boundaries](diagrams.md#5-data-ownership-partition-across-the-boundaries) (diagrams.md#5-data-ownership-partition-across-the-boundaries).

## Correzioni di questo giro

| Correzione | Effetto |
| --- | --- |
| I vettori di immagine non hanno una superficie REST | F-07 e UC-07 passano a S-01 e S-03 |
| La fusione cross-modale del client non aveva requisito | Aggiunti F-18, R-35 e UC-18, tracciati su T-12 |
| L'interfaccia ha una sola lingua, non due | Corretto l'attributo di qualità e il riferimento all'esperienza |
| Vocabolario dell'interfaccia in italiano, ma le etichette sono inglesi | Riscritto con le etichette effettivamente mostrate |
| Collegamenti relativi errati fra dossier, catalogo e riferimento | Corretti |

## Rischi e questioni aperte

- Richiamo su dati reali non misurato: è la domanda P2 dello scouting, che fissa i valori predefiniti dell'indice.
- `POST /api/database/{db}/collections/search-filter` accetta un filtro e lo ignora. Nessun requisito la copre.
- Le tre rotte di menu chiamate dall'interfaccia non esistono nel server.
- R-30, astrazione di provider per l'ecosistema .NET, non ha implementazione nel repository.
- Accessibilità dell'interfaccia mai verificata.

## Prossime azioni

| Azione | Attore |
| --- | --- |
| Chiudere questi tre difetti in architettura: rimozione di `search-filter`, menu statico, credenziali predefinite | Architettura |
| Fissare stack, vincoli, forme e ambienti | Architettura |
| Congelare superfici, navigazione, stati e vocabolario | Architettura |

## Diagrammi di riferimento

- [Requirements flows and surfaces](diagrams.md#3-requirements-flows-and-surfaces) (diagrams.md#3-requirements-flows-and-surfaces)
- [Canonical data model](diagrams.md#6-canonical-data-model) (diagrams.md#6-canonical-data-model)
- [Service data flows grouped by deployment](diagrams.md#12-service-data-flows-grouped-by-deployment) (diagrams.md#12-service-data-flows-grouped-by-deployment): incluso.

Il dossier completo è in [Analysis dossier](dossier.md) (dossier.md). Il verdetto è in [Analysis handoff](dossier.md#analysis-handoff) (dossier.md#analysis-handoff).
