# Scouting: il client .NET di Qdrant a confronto con `Jigen.Client`

## Domanda

Come si usa il client .NET di **Qdrant** per inserire una voce con vettore denso e
sparso, cercarla e fonderla, e in che cosa l'uso differisce dal client .NET di
**Jigen** di questo repository.

## Versioni e fonti

Le differenze d'uso dipendono dalla versione: le API sotto sono quelle del client
`Qdrant.Client` **1.19.0** (ultima su NuGet al momento della ricerca) e del server
Qdrant **1.10+** per le query unificate, lette da fonti ufficiali e non da memoria.

| Fonte | Cosa è stato verificato |
| --- | --- |
| `qdrant/qdrant-dotnet`, `README.md` (ramo `main`) | Creazione del client, `CreateCollectionAsync`, `UpsertAsync`, `SearchAsync` con filtro |
| `qdrant/qdrant-dotnet`, `tests/Qdrant.Client.Tests/SparseVectorsTests.cs` | Configurazione del vettore sparso, upsert di un punto con sparso, query sparsa |
| `qdrant/qdrant-dotnet`, `tests/Qdrant.Client.Tests/PointTests.cs` | Prefetch e fusione RRF |
| `qdrant/qdrant-dotnet`, `tests/Qdrant.Client.Tests/CollectionTests.cs` | Vettori con nome (`VectorParamsMap`) |
| `qdrant/qdrant-dotnet`, `src/Qdrant.Client/IQdrantClient.cs` | Firme di `CreateCollectionAsync`, `UpsertAsync`, `SearchAsync`, `QueryAsync` |
| `qdrant/qdrant-dotnet`, `src/Qdrant.Client/Grpc/Query.cs` | Conversioni implicite verso `Query` (vettore denso, sparso, `Fusion`, `OrderBy`) |
| `qdrant/qdrant`, `lib/api/src/grpc/proto/points.proto`, `collections.proto` | Campi di `PrefetchQuery`, `Rrf`, `SparseVectorParams`, `Modifier` |
| `nuget.org`, indice di `Qdrant.Client` | Ultima versione pubblicata: 1.19.0 |

Il confronto con Jigen è fatto sul codice di questo repository: `VectorCollection<T>`
del client .NET, i comandi e le query di `Jigen.Core`, il protocollo `Jigen.proto`.

## Lo stesso scenario, due clienti

Scenario: una collezione con un vettore denso da 4 dimensioni e un vettore sparso,
una voce scritta con entrambi, una ricerca densa, una sparsa e una fusione.

### Qdrant

```csharp
using Qdrant.Client;
using Qdrant.Client.Grpc;

using var client = new QdrantClient("localhost");   // gRPC su :6334

// 1. La collezione dichiara lo schema: dimensione, metrica e nome di ogni vettore.
await client.CreateCollectionAsync(
    collectionName: "articles",
    vectorsConfig: new VectorParamsMap
    {
        Map = { ["dense"] = new VectorParams { Size = 4, Distance = Distance.Cosine } }
    },
    sparseVectorsConfig: new SparseVectorConfig(
        ("sparse", new SparseVectorParams { })));

// 2. La voce porta il denso e lo sparso come vettori con nome diversi.
await client.UpsertAsync("articles", new[]
{
    new PointStruct
    {
        Id = 42,
        Vectors = new NamedVectors
        {
            Vectors =
            {
                ["dense"] = new float[] { 0.1f, 0.2f, 0.3f, 0.4f },
                ["sparse"] = new Vector(new[] { (0.8f, 12u), (1.2f, 98u) })
            }
        },
        Payload = { ["title"] = "Il documento", ["category"] = "database" }
    }
});

// 3. Ricerca densa: il nome del vettore è il default della collezione.
var dense = await client.QueryAsync("articles", query: new float[] { 0.1f, 0.2f, 0.3f, 0.4f }, limit: 10);

// 4. Ricerca sparsa: valori e indici separati, in quest'ordine.
var sparse = await client.QueryAsync(
    "articles",
    query: new Query { Nearest = (VectorInput)(new float[] { 0.8f, 1.2f }, new uint[] { 12u, 98u }) },
    usingVector: "sparse",
    limit: 10);

// 5. Fusione: il server esegue i prefetch e fonde lui, con RRF.
var fused = await client.QueryAsync(
    "articles",
    query: new Rrf { K = 60, Weights = { 1f, 1f } },
    prefetch: new List<PrefetchQuery>
    {
        new() { Query = new float[] { 0.1f, 0.2f, 0.3f, 0.4f }, Using = "dense", Limit = 40 },
        new() { Query = (VectorInput)(new float[] { 0.8f, 1.2f }, new uint[] { 12u, 98u }), Using = "sparse", Limit = 40 }
    },
    limit: 10);
```

### Jigen

```csharp
using Jigen.Client;
using Jigen.Client.BaseTypes;

// 1. Nessuno schema da dichiarare: la collezione nasce alla prima scrittura e le
//    dimensioni si fissano da quel vettore. Un solo slot denso e uno sparso.
using var context = new Context(options);
var articles = context.Collection<Article>();

// 2. Una chiamata sola, due slot della stessa voce.
await articles.AddAsync(42, new Article { Title = "Il documento" },
    embeddings: [0.1f, 0.2f, 0.3f, 0.4f],
    sparse: SparseVector.Create(terms: [12, 98], weights: [0.8f, 1.2f]));

// 3. Ricerca densa.
var dense = await articles.SearchAsync([0.1f, 0.2f, 0.3f, 0.4f], top: 10);

// 4. Ricerca sparsa.
var sparse = await articles.SearchAsync(SparseVector.Create([12, 98], [0.8f, 1.2f]), top: 10);

// 5. Fusione: il server esegue le due ricerche e le fonde lui, con RRF.
var fused = await articles.SearchHybridAsync(
    [0.1f, 0.2f, 0.3f, 0.4f],
    SparseVector.Create([12, 98], [0.8f, 1.2f]),
    top: 10, denseWeight: 1f, sparseWeight: 1f);
```

Lo scenario è lo stesso; l'ordine delle operazioni no: Qdrant impone di
**dichiarare lo schema prima**, Jigen lo deduce dalla prima scrittura.

## Differenze d'uso

### Schema e collezioni

| Aspetto | Qdrant | Jigen |
| --- | --- | --- |
| Creazione della collezione | Esplicita, **obbligatoria**, con nome, dimensione e metrica | Implicita alla prima scrittura; la dimensione si fissa dal primo vettore |
| Metrica | Scelta per collezione: `Cosine`, `Dot`, `Euclid`, `Manhattan` | Coseno, unica |
| Vettori per voce | **N vettori con nome**, decisi alla creazione | Due slot fissi: denso e sparso, senza nome |
| Vettore sparso | Dichiarato a sé, con nome, indice proprio e modificatore opzionale | Slot della voce, nessuna configurazione di collezione |
| Indici sul payload | `CreatePayloadIndexAsync` per i campi filtrati | Nessun indice di payload: il filtro valuta il contenuto serializzato |
| Quantizzazione | Per collezione (`QuantizationConfig`) | Opzione del server, SQ8 sulla collezione |
| Coerenza di scrittura | Parametro `wait` per richiedere la visibilità immediata | Semantica «accettata»; la durabilità la governa il registro di scrittura |

La differenza più profonda non è sintattica: in Qdrant lo schema è un **contratto
che precede i dati**, in Jigen è una **conseguenza dei dati**. Il primo permette
errori precoci (una dimensione sbagliata fallisce alla scrittura contro uno schema
noto) e strumenti che conoscono la collezione; il secondo toglie un passaggio ma
sposta la scoperta dell'errore più avanti, e non permette due densi sulla stessa
voce.

### Scrittura

| Aspetto | Qdrant | Jigen |
| --- | --- | --- |
| Forma | `UpsertAsync(collection, IEnumerable<PointStruct>)` | `AddAsync(key, content, embeddings, sparse)` |
| Contenuto | `Payload` è una mappa di `Value` protobuf: il client **non conosce il tipo** | `content` è `T`, serializzato dal client e riletto nella stessa forma |
| Chiave | `ulong` o `Guid` (UUID) | Qualunque `byte[]`, con `VectorKey` e conversione di tipo |
| Due vettori | Due chiavi nella mappa dei vettori con nome | Due proprietà della stessa voce |
| Espressione dello sparso | `new Vector(new[] { (peso, indice) })` — coppie peso-indice | `SparseVector.Create(terms, weights)` — due array paralleli |
| Scrittura in blocco | Una chiamata, elenco di punti | `AddRangeAsync` in streaming su una sola chiamata |
| Validazione del vettore sparso | Il server rifiuta indici fuori ordine o duplicati | Il client valida la forma, il server rivalida contro il tetto configurato |

Sull'espressione dello sparso la differenza è solo ergonomica ma genera errori
diversi: le coppie `(peso, indice)` di Qdrant rendono impossibile un disallineamento
fra i due elenchi ma si leggono al contrario rispetto a come si pensa un vettore
sparso; i due array di Jigen si leggono nell'ordine naturale ma richiedono una
lunghezza uguale, che è il primo errore che il client deve intercettare.

### Ricerca

| Aspetto | Qdrant | Jigen |
| --- | --- | --- |
| API | Una sola: `QueryAsync`, con l'operazione dentro `Query` | Un metodo per operazione: `SearchAsync`, `SearchAsync(sparse)`, `SearchHybridAsync` |
| Scelta del vettore | Nome (`usingVector`) | Il tipo dell'argomento |
| Query sparsa | `(float[] valori, uint[] indici)` | `SparseVector` |
| Testo | Il server non calcola embedding dal testo nella query unificata (serve un modello di inferenza o `Document`/`Image`) | `SearchAsync("frase")` calcola l'embedding con il modulo ONNX del server |
| Filtro | `Filter` con condizioni tipizzate e indici di payload | Espressione LINQ tradotta dal client in un AST, valutata sul contenuto |
| Soglia e tuning | `scoreThreshold`, `SearchParams` | `SearchOptions` con `MinScore`, `EfSearch`, `NoContent` |
| Raggruppamento | `QueryGroupsAsync` per campo di payload | `SearchByMetadata` per record condiviso, con budget obbligatorio |
| Risultato | `ScoredPoint` con `Id`, `Score`, `Payload`, `Vectors` | `VectorSearchResult<T>` con `Key`, `Score`, `Content` tipizzato |

Il punto più visibile per chi arriva da Qdrant è che **la modalità non è un nome ma
un tipo**: non si sceglie `usingVector: "sparse"`, si passa uno `SparseVector`. In
cambio, Qdrant ha una sola API da imparare e la stessa forma serve per
raccomandazioni, scoperta, campionamento e ordinamento per payload; Jigen ha un
metodo per caso, più leggibile ma più superficie da conoscere.

### Fusione

| Aspetto | Qdrant | Jigen |
| --- | --- | --- |
| Dove avviene | Nel server, tramite `prefetch` + `Query.Rrf`/`Fusion` | Nel server, dentro l'operazione di ricerca ibrida |
| Forma .NET | `query: new Rrf { K = 60, Weights = { … } }` più l'elenco dei `PrefetchQuery` | `SearchHybridAsync(dense, sparse, denseWeight, sparseWeight)` |
| Pesi | Uno per ciascun prefetch, nell'ordine dei prefetch | Due, uno per modalità |
| Costante `k` | Configurabile per query (`Rrf.K`) | Fissa a 60 (`RankFusion.DefaultK`) |
| Profondità | `Limit` su ogni prefetch, decisa dal chiamante | Dedotta dal server (`top × 4`) |
| Algoritmi | RRF, DBSF, e fusione annidata su più stadi | RRF |
| Provenienza del risultato | Non dichiarata: il punteggio è quello fuso | `InDense`, `InSparse`, `DenseScore`, `SparseScore` su ogni risultato |

Due differenze contano più delle altre. **La profondità dei prefetch** è un
parametro esplicito in Qdrant e una deduzione in Jigen: chi arriva da Qdrant cerca
il parametro e non lo trova, e non può correggere un richiamo basso senza alzare
`top`. **La provenienza** è l'inverso: Jigen dichiara quale modalità ha trovato la
voce, Qdrant no, e chi arriva a Jigen guadagna una informazione che prima doveva
ricostruire da due ricerche separate.

### Errori e ciclo di vita

| Aspetto | Qdrant | Jigen |
| --- | --- | --- |
| Collezione assente | Errore: la collezione va creata | La collezione nasce alla prima scrittura |
| Dimensione sbagliata | Errore alla scrittura, contro lo schema | Errore alla scrittura, contro la dimensione fissata dal primo vettore |
| Vettore sparso senza registro | Non applicabile | Rifiuto esplicito: senza registro di scrittura lo sparso non si recupera |
| Chiusura | `QdrantClient` è `IDisposable` | `Context` è `IDisposable`, chiude il canale gRPC |
| Errori di trasporto | `RpcException` | `JigenServerException` e gli esiti tipizzati dei comandi |

## Cosa impariamo

1. **Lo schema esplicito è una scelta, non un dettaglio.** Qdrant paga un
   passaggio in più ma rende la collezione interrogabile e validabile prima che
   contenga dati. Jigen ha scelto il contrario; la conseguenza da conoscere è che
   una dimensione sbagliata si scopre alla prima scrittura e che non esistono due
   densi sulla stessa voce.
2. **La forma della query sparsa cambia l'errore tipico.** Le coppie peso-indice
   eliminano il disallineamento fra i due elenchi; i due array paralleli lo
   rendono il primo errore possibile. Il client di Jigen lo intercetta prima della
   rete, che è la mitigazione giusta.
3. **La fusione ha bisogno di due parametri che oggi non esponiamo**: la costante
   `k` e la profondità dei candidati. Sono le due manopole che in Qdrant si usano
   per correggere un richiamo basso in ibrido.
4. **La provenienza del risultato è un vantaggio di Jigen da non perdere**: nessun
   cliente Qdrant può dire, da una risposta fusa, quale modalità ha trovato una
   voce.
5. **L'ergonomia del contenuto tipizzato è un vantaggio di Jigen** per chi scrive
   applicazioni .NET: `Payload` di `Value` protobuf costringe a convertire a mano,
   `VectorEntry<T>` no.

## Divergenze da decidere, non da copiare

| Idea da Qdrant | Vale la pena? | Motivo |
| --- | --- | --- |
| Vettori con nome, più di due per voce | **No, ora** | Rompe il contratto a due slot e il formato del registro; nessun requisito lo chiede |
| Costante `k` della fusione configurabile | **Sì, come opzione** | Costo basso, corregge un richiamo basso senza toccare i pesi |
| Profondità dei candidati per modalità | **Da misurare prima** | Oggi è `top × 4`: senza una misura il parametro sarebbe una leva cieca |
| Modificatore IDF sul vettore sparso | **No** | Sposterebbe il calcolo delle statistiche di corpus nel server: decisione esclusa dallo scouting sullo sparso (R-36) |
| Indici sul payload | **Da valutare a parte** | Il filtro attuale valuta il contenuto; un indice cambierebbe il modello di filtro, non solo le prestazioni |
| Metrica per collezione | **No, ora** | Il coseno è una garanzia dichiarata; cambiarla è un incremento, non un'opzione |

## Domande aperte

- La profondità fissa `top × 4` perde richiamo su collezioni grandi? Serve la
  misura U3 già prevista dallo scouting sullo sparso; la stessa misura direbbe se
  esporre la profondità.
- La costante `k = 60` è la scelta giusta per corpora molto piccoli, dove le
  posizioni sono poche e la formula appiattisce le differenze?

## Riferimenti

- [Vettori sparsi e ricerca ibrida](../../concepts/sparse-vectors.md) — il contratto in-process
- [Uso del client .NET](../../../docs/client/usage.md) — `SparseVector`, `SearchHybridAsync`
- [Scouting sui vettori sparsi](../sparse-vectors/dossier.md) — la decisione di non calcolare lo sparso nel server
- [Protocollo Jigen](../../../src/Server/Jigen.Grpc/Protos/Jigen.proto) — i messaggi effettivi
