import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Jigen DB — Vector search, native to .NET",
  description:
    "A visual technical guide to Jigen DB: embedded vector engine, gRPC/REST server, persistent HNSW and ONNX embeddings.",
};

const embeddedCode = `dotnet add package Jigen.Store
dotnet add package Jigen.Indexer.HNSW

using var store = new Store(new StoreOptions {
  DataBasePath = "./data",
  DataBaseName = "products"
});

await store.AppendContent(new VectorEntry {
  CollectionName = "catalog",
  Embedding = embedding,
  Content = serializer.Serialize(product)
});

var hits = store.Search("catalog", query, top: 10);`;

const dockerCode = `docker run -d --name jigendb \\
  -p 3223:3223 -p 13223:13223 \\
  -v jigendb-data:/data/jigendb \\
  -v jigendb-models:/data/onnx \\
  ppossanzini/jigendb-all-in-one:latest`;

export default function Home() {
  return (
    <main>
      <header className="nav wrap">
        <a className="brand" href="#top" aria-label="Jigen DB, back to top">
          <span className="brandMark">J</span>
          <span>JIGEN <b>DB</b></span>
        </a>
        <nav aria-label="Main navigation">
          <a href="/architecture/">Architecture</a>
          <a href="/clients/">Clients</a>
          <a href="#quickstart">Quick start</a>
          <a href="/benchmarks/">Benchmarks</a>
        </nav>
        <a className="navCta" href="https://github.com/ppossanzini/Jigen">GitHub ↗</a>
      </header>

      <section className="hero wrap" id="top">
        <div className="heroCopy">
          <div className="eyebrow"><span /> VECTOR DATABASE · NATIVE TO .NET</div>
          <h1>Vectors, in your process.<br/><em>Or in your stack.</em></h1>
          <p className="lead">Jigen is an open-source vector database written in C#. Run it like SQLite inside your application, or deploy it as a gRPC and REST server with built-in ONNX embeddings.</p>
          <div className="heroActions">
            <a className="button primary" href="#quickstart">Start in 60 seconds <span>→</span></a>
            <a className="button ghost" href="https://github.com/ppossanzini/Jigen/tree/main/docs">Read the documentation</a>
          </div>
          <div className="signals" aria-label="Key characteristics">
            <span><i>✓</i> Apache 2.0</span>
            <span><i>✓</i> .NET 8 / 10</span>
            <span><i>✓</i> Disk-backed HNSW</span>
          </div>
        </div>

        <div className="heroVisual" aria-label="Vector search flow">
          <div className="gridGlow" />
          <div className="vectorCard query">
            <small>QUERY VECTOR</small>
            <div className="vectorBars">{[44,72,30,86,58,68,39,78,52,91,34,64].map((h,i)=><i key={i} style={{height:`${h}%`}} />)}</div>
            <code>[0.21, −0.48, 0.87, …]</code>
          </div>
          <div className="flowLine one"><span>cosine</span></div>
          <div className="coreOrb"><b>J</b><span>HNSW<br/>INDEX</span></div>
          <div className="flowLine two"><span>top k</span></div>
          <div className="resultStack">
            <article><b>01</b><div><strong>Semantic match</strong><small>score 0.984</small></div><i>98%</i></article>
            <article><b>02</b><div><strong>Related context</strong><small>score 0.917</small></div><i>92%</i></article>
            <article><b>03</b><div><strong>Candidate result</strong><small>score 0.863</small></div><i>86%</i></article>
          </div>
          <div className="latency"><span>SEARCH P50</span><strong>0.19<small> ms</small></strong><em>10k × 128 dim*</em></div>
        </div>
      </section>

      <section className="decision wrap" id="architecture">
        <div className="sectionIntro">
          <div className="eyebrow"><span /> ONE ENGINE, TWO TOPOLOGIES</div>
          <h2>Choose where search<br/>should live.</h2>
          <p>The same mental model carries you from a local prototype to a distributed architecture. The operational boundary changes; the way you work with vectors does not.</p>
        </div>
        <div className="modes">
          <article className="mode featured">
            <div className="modeTop"><span className="number">01</span><span className="pill">LOWEST LATENCY</span></div>
            <h3>Embedded</h3>
            <p>The database runs in the same process as your application. No network hop, memory-mapped files and local persistence.</p>
            <div className="miniArch"><span>YOUR APP</span><i>direct call</i><strong>JIGEN STORE</strong><b>NVMe</b></div>
            <ul><li>Desktop, edge and single-node services</li><li>High-throughput ingestion</li><li>Exact or HNSW search</li></ul>
            <a href="/embedded/">Explore the embedded engine <span>→</span></a>
          </article>
          <article className="mode">
            <div className="modeTop"><span className="number">02</span><span className="pill">SCALE OUT</span></div>
            <h3>Server</h3>
            <p>A multi-database host for multiple applications, with a typed .NET client, REST, gRPC and independently scalable embedding workers.</p>
            <div className="miniArch server"><span>CLIENTS</span><i>gRPC / REST</i><strong>JIGEN SERVER</strong><b>WORKERS</b></div>
            <ul><li>Shared services and multiple teams</li><li>Built-in ONNX embeddings</li><li>Scalable RabbitMQ workers</li></ul>
            <a href="/server/">Explore the server topology <span>→</span></a>
          </article>
        </div>
      </section>

      <section className="pipeline">
        <div className="wrap">
          <div className="sectionHeading"><div><div className="eyebrow"><span /> FROM DATA TO RESULTS</div><h2>A pipeline you can reason about.</h2></div><p>Every stage is explicit, configurable and observable. Jigen does not hide the cost of search—it gives you the controls to manage it.</p></div>
          <div className="steps">
            <article><b>1</b><span className="stepIcon">Aa</span><h3>Content</h3><p>Typed documents, text, images or vectors you already computed.</p></article>
            <article><b>2</b><span className="stepIcon">∿</span><h3>Embedding</h3><p>ONNX generation in-process or on dedicated workers.</p></article>
            <article><b>3</b><span className="stepIcon">⌘</span><h3>Persistence</h3><p>Append-only, memory-mapped storage with crash recovery.</p></article>
            <article><b>4</b><span className="stepIcon">◎</span><h3>Index</h3><p>Exact brute force or persistent HNSW with SQ8 quantization.</p></article>
            <article><b>5</b><span className="stepIcon">↗</span><h3>Results</h3><p>Top-k, similarity scores, metadata filters and exact reranking.</p></article>
          </div>
        </div>
      </section>

      <section className="quickstart wrap" id="quickstart">
        <div className="sectionHeading"><div><div className="eyebrow"><span /> QUICK START</div><h2>From zero to the first query.</h2></div><p>Start with the topology closest to your architecture. Both paths lead to the same concepts: collections, embeddings and search.</p></div>
        <div className="codeGrid">
          <article className="codePanel" id="embedded"><header><div><span className="dot"/><span className="dot"/><span className="dot"/></div><b>EMBEDDED · C#</b><small>Program.cs</small></header><pre><code>{embeddedCode}</code></pre><footer><span>01</span> Store and query vectors without an external service.</footer></article>
          <article className="codePanel light" id="server"><header><div><span className="dot"/><span className="dot"/><span className="dot"/></div><b>SERVER · DOCKER</b><small>terminal</small></header><pre><code>{dockerCode}</code></pre><footer><span>02</span> gRPC on :3223 · REST and Insight UI on :13223</footer></article>
        </div>
      </section>

      <section className="performance" id="performance">
        <div className="wrap perfGrid">
          <div className="perfCopy"><div className="eyebrow lightEye"><span /> COMPARATIVE BENCHMARK</div><h2>The advantage<br/>of a shorter path.</h2><p>In embedded mode, a query is a direct call: no serialization, network stack or container boundary. On the 10k × 128 dimensions test, that means a measured P50 latency of 0.19 ms.</p><a className="button inverse" href="/benchmarks/">Explore the benchmark ↗</a></div>
          <div className="chart" aria-label="Indicative P50 latency comparison in milliseconds"><div className="chartHead"><span>SEARCH LATENCY · P50</span><small>LOWER IS BETTER</small></div>
            {[['JIGEN',0.19,'3%'],['QDRANT',1.18,'16%'],['MILVUS',5.99,'79%'],['PGVECTOR',7.60,'100%']].map(([name,value,width])=><div className={`bar ${name==='JIGEN'?'hot':''}`} key={String(name)}><label>{name}</label><div><i style={{width:String(width)}} /></div><strong>{value}<small> ms</small></strong></div>)}
            <p>* Jigen runs embedded; the other systems run as containerized services. This comparison measures different architectures and is intentionally contextualized.</p>
          </div>
        </div>
      </section>

      <section className="capabilities wrap">
        <div className="sectionHeading"><div><div className="eyebrow"><span /> BUILT FOR .NET</div><h2>You keep the controls.</h2></div></div>
        <div className="capGrid">
          <article><span>MEM</span><h3>Predictable persistence</h3><p>Append-only files, memory mapping and durability checkpoints for fast restarts.</p></article>
          <article><span>HNSW</span><h3>Disk-backed index</h3><p>Concurrent inserts and deletes, optional SQ8 and exact reranking to recover precision.</p></article>
          <article><span>ONNX</span><h3>Local embeddings</h3><p>CPU by default, with configurable CUDA, DirectML, OpenVINO and CoreML providers.</p></article>
          <article><span>LINQ</span><h3>Typed client</h3><p>Dictionary-like collections and LINQ predicates translated into server-side filters.</p></article>
        </div>
      </section>

      <section className="finalCta wrap"><div><div className="eyebrow lightEye"><span /> OPEN SOURCE · APACHE 2.0</div><h2>Bring semantic search<br/>into your .NET stack.</h2><p>Start embedded. Move to the server when you need it. Keep the operational model simple.</p></div><div className="ctaActions"><a className="button white" href="https://github.com/ppossanzini/Jigen">Explore the repository <span>↗</span></a><a href="https://www.nuget.org/packages/Jigen.Store">Jigen.Store on NuGet →</a></div></section>

      <footer className="footer wrap"><div className="brand"><span className="brandMark">J</span><span>JIGEN <b>DB</b></span></div><p>A native vector database for the .NET ecosystem.</p><div><a href="https://github.com/ppossanzini/Jigen/tree/main/docs">Docs</a><a href="https://github.com/ppossanzini/Jigen">GitHub</a><a href="https://www.nuget.org/packages/Jigen.Store">NuGet</a></div></footer>
    </main>
  );
}
