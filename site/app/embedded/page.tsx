import type { Metadata } from "next";
import { CodeBlock, DetailShell } from "../_components/DetailShell";

export const metadata: Metadata = { title: "Embedded engine — Jigen DB", description: "Use Jigen in-process in a .NET application." };
const setup = `dotnet add package Jigen.Store
dotnet add package Jigen.Indexer.HNSW

var options = new StoreOptions {
  DataBasePath = "./data/jigen",
  DataBaseName = "catalog",
  Indexer = new SmallWorldIndexer(
    new SmallWorldOptions(
      m: 16,
      efConstruction: 200,
      efSearch: 64,
      storagePath: "./data/jigen/hnsw"))
};

using var store = new Store(options);`;
const query = `await store.AppendContent(new VectorEntry {
  Id = Guid.NewGuid().ToByteArray(),
  CollectionName = "products",
  Content = serializer.Serialize(product),
  Embedding = productEmbedding
});

var results = store.Search(
  "products", queryEmbedding, top: 10);

await store.SaveChangesAsync();`;

export default function EmbeddedPage(){return <DetailShell eyebrow="IN-PROCESS ENGINE" index="02 / 04" title="Vector search with no service boundary." intro="Install two NuGet packages, choose a local path and query vectors through direct C# calls. Embedded mode is designed for applications where latency, deployment simplicity and data locality matter.">
  <section className="detailSection wrap"><div className="detailHeading"><span>01</span><div><h2>Build the store</h2><p>The store owns persistence and collection data. The indexer is injected, so exact and approximate search remain an explicit application choice.</p></div></div><div className="splitDetail"><CodeBlock label="PROGRAM.CS">{setup}</CodeBlock><div className="sideNotes"><article><b>Store</b><p>Lifecycle, files, collections and durability.</p></article><article><b>SmallWorldIndexer</b><p>Persistent HNSW graph and search policy.</p></article><article><b>StoreOptions</b><p>Database identity, path and ingestion behavior.</p></article></div></div></section>
  <section className="detailBand"><div className="wrap splitDetail"><div><div className="eyebrow"><span /> WRITE → SEARCH → CHECKPOINT</div><h2>The smallest useful loop.</h2><p>Content and vectors are written together under a collection name. Queries return ranked entries, while SaveChangesAsync creates a clear durability point.</p><div className="callouts"><span><b>01</b> Append</span><span><b>02</b> Search</span><span><b>03</b> Persist</span></div></div><CodeBlock label="INGEST + QUERY">{query}</CodeBlock></div></section>
  <section className="detailSection wrap"><div className="detailHeading"><span>02</span><div><h2>When embedded is the right boundary</h2><p>Choose it when the application and vector store share a lifecycle, security boundary and scaling unit.</p></div></div><div className="useGrid"><article><span>DESKTOP</span><h3>Local intelligence</h3><p>Private semantic search with no network dependency.</p></article><article><span>EDGE</span><h3>Data locality</h3><p>Keep vectors close to devices and constrained links.</p></article><article><span>SERVICE</span><h3>Single-node APIs</h3><p>Add vector search without operating another database.</p></article><article><span>TESTING</span><h3>Deterministic fixtures</h3><p>Create isolated databases directly in integration tests.</p></article></div></section>
</DetailShell>}
