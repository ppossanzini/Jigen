import type { Metadata } from "next";
import { CodeBlock, DetailShell } from "../_components/DetailShell";

export const metadata: Metadata = {
  title: "Clients & integrations — Jigen DB",
  description: "Choose between Jigen's .NET, Python, gRPC and LLM framework clients.",
};

const dotnet = `using Jigen.Client;

var context = new Context(new ConnectionOptions {
  HostName = "localhost",
  Port = 3223,
  DatabaseName = "rag"
});

var knowledge = context.Collection<Article>("knowledge");
knowledge.Add("doc-1", article,
  sentence: "Jigen stores HNSW graphs on disk.");

var hits = await knowledge.SearchAsync(
  "How does persistence work?", top: 5);`;

const python = `from jigen import ConnectionOptions, Context

with Context(ConnectionOptions(database="rag")) as ctx:
    knowledge = ctx.collection("knowledge")
    knowledge.upsert(
        "doc-1",
        {"text": "Jigen stores HNSW graphs on disk."},
        text="Jigen stores HNSW graphs on disk.",
    )

    hits = knowledge.search(
        "How does persistence work?", top=5
    )`;

const langchain = `from jigen.integrations.langchain import JigenVectorStore

store = JigenVectorStore(ctx.collection("knowledge"))
retriever = store.as_retriever(search_kwargs={"k": 4})

documents = retriever.invoke(
    "How can I deploy Jigen?"
)`;

export default function ClientsPage() {
  return (
    <DetailShell eyebrow="CLIENTS & LLM INTEGRATIONS" index="04 / 05" title="One protocol. Native workflows in every stack." intro="Use Jigen through a typed .NET API, an idiomatic sync/async Python client, standard gRPC or REST, and adapters that make the same collections available to familiar RAG frameworks.">
      <section className="detailSection wrap">
        <div className="detailHeading"><span>01</span><div><h2>Choose the surface, not a different database.</h2><p>Every client targets the same server contract and collection model. Pick the abstraction that fits the application boundary and keep data portable across workflows.</p></div></div>
        <div className="clientMatrix">
          <article className="active"><div className="clientTop"><span>.NET</span><small>FIRST-PARTY</small></div><h3>Jigen.Client</h3><p>Typed collections, async operations, LINQ filters and direct access to the complete gRPC surface.</p><ul><li>.NET 8 and newer</li><li>Contractless MessagePack</li><li>Dictionary-style API</li></ul><a href="https://www.nuget.org/packages/Jigen.Client">NuGet package ↗</a></article>
          <article><div className="clientTop"><span>PY</span><small>FIRST-PARTY</small></div><h3>jigen-client</h3><p>Idiomatic synchronous and asyncio contexts with batch ingestion, filters and LLM adapters.</p><ul><li>Python 3.10+</li><li>LangChain + LlamaIndex</li><li>Client or server embeddings</li></ul><a href="https://github.com/ppossanzini/Jigen/tree/main/src/Client/Jigen.Python">Python source ↗</a></article>
          <article><div className="clientTop"><span>RPC</span><small>OPEN PROTOCOL</small></div><h3>gRPC / REST</h3><p>Generate another language client from the protobuf contract, or call operational endpoints over HTTP.</p><ul><li>Streaming bulk ingestion</li><li>Portable protobuf schema</li><li>REST for simple integrations</li></ul><a href="https://github.com/ppossanzini/Jigen/blob/main/docs/server/grpc-api.md">Protocol reference ↗</a></article>
        </div>
      </section>

      <section className="detailBand"><div className="wrap"><div className="detailHeading"><span>02</span><div><h2>The same operation, expressed naturally.</h2><p>Both first-party clients preserve the same concepts—context, collection, document, vector and ranked result—without forcing one language's conventions onto the other.</p></div></div><div className="codeGrid clientCodeGrid"><CodeBlock label=".NET · CLIENT.CS">{dotnet}</CodeBlock><CodeBlock label="PYTHON · CLIENT.PY">{python}</CodeBlock></div></div></section>

      <section className="detailSection wrap">
        <div className="detailHeading"><span>03</span><div><h2>Built for familiar RAG pipelines.</h2><p>The Python package keeps framework dependencies optional. Install only the adapter you use; the underlying collection and serialized content stay the same.</p></div></div>
        <div className="llmGrid">
          <article className="llmFlow"><div className="framework"><small>LANGCHAIN</small><strong>VectorStore</strong></div><i>→</i><div className="adapter"><small>ADAPTER</small><strong>JigenVectorStore</strong></div><i>→</i><div className="database"><small>COLLECTION</small><strong>Jigen DB</strong></div><p>Use <code>add_texts</code>, similarity search and <code>as_retriever()</code>. Bring any LangChain embedding model or use Jigen's ONNX service.</p></article>
          <article className="llmFlow"><div className="framework"><small>LLAMAINDEX</small><strong>VectorStoreIndex</strong></div><i>→</i><div className="adapter"><small>ADAPTER</small><strong>JigenVectorStore</strong></div><i>→</i><div className="database"><small>COLLECTION</small><strong>Jigen DB</strong></div><p>Store text and node metadata with their embeddings, then reconstruct a queryable index directly from the collection.</p></article>
        </div>
      </section>

      <section className="detailBand"><div className="wrap splitDetail"><div><div className="eyebrow"><span /> LANGCHAIN RETRIEVER</div><h2>From collection to retriever in three lines.</h2><p>With no external embedding object, ingestion and queries use the model configured on the Jigen server. Pass a LangChain Embeddings instance to move that responsibility to the client.</p><div className="badgeRow"><span>SERVER ONNX</span><b>OR</b><span>CLIENT EMBEDDINGS</span></div></div><CodeBlock label="RAG.PY">{langchain}</CodeBlock></div></section>

      <section className="detailSection wrap">
        <div className="detailHeading"><span>04</span><div><h2>Compatibility at a glance.</h2><p>Choose based on the programming model and integration layer your application already uses.</p></div></div>
        <div className="compatTable" role="table" aria-label="Jigen client compatibility">
          <div className="compatHead" role="row"><span>CAPABILITY</span><span>.NET</span><span>PYTHON</span><span>LANGCHAIN</span><span>LLAMAINDEX</span><span>RAW API</span></div>
          {[["Vector search","●","●","●","●","●"],["Server-side embeddings","●","●","●","—","●"],["Client-side embeddings","●","●","●","●","●"],["Metadata filters","●","●","●","●","●"],["Streaming ingestion","●","●","via core","via core","●"],["Async workflow","●","●","●","●","client-specific"]].map(row => <div className="compatRow" role="row" key={row[0]}>{row.map((cell,index)=><span role="cell" className={cell === "●" ? "yes" : ""} key={`${row[0]}-${index}`}>{cell}</span>)}</div>)}
        </div>
        <p className="tableNote">Framework adapters expose the capabilities expected by each abstraction. The core Python and .NET clients remain the complete Jigen-native surfaces.</p>
      </section>
    </DetailShell>
  );
}
