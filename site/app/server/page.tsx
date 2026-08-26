import type { Metadata } from "next";
import { CodeBlock, DetailShell } from "../_components/DetailShell";
export const metadata: Metadata = { title: "Server deployment — Jigen DB", description: "Deploy Jigen with Docker, gRPC, REST and scalable embedding workers." };
const compose = `services:
  jigendb:
    image: ppossanzini/jigendb:latest
    ports: ["3223:3223", "13223:13223"]
    volumes: ["jigen-data:/data/jigendb"]
    environment:
      Kaido__Enabled: "true"
      RabbitMQ__HostName: rabbitmq

  embeddings:
    image: ppossanzini/jigen-embeddings:latest
    volumes: ["./models:/data/onnx"]
    deploy: { replicas: 2 }

  rabbitmq:
    image: rabbitmq:3-management`;
const client = `var context = new Context(new ConnectionOptions {
  HostName = "jigen.internal",
  Port = 3223,
  TLS = true,
  DatabaseName = "catalog"
});

var products = context.Collection<Product>("products");
products.Add(id, product, sentence: product.Description);

var hits = products.Search(
  "quiet mechanical keyboard",
  x => x.Category == "keyboards",
  top: 5);`;
export default function ServerPage(){return <DetailShell eyebrow="SERVER TOPOLOGY" index="03 / 04" title="One vector service. Multiple clients and workers." intro="The server turns the embedded engine into a shared platform: multi-database hosting, gRPC and REST APIs, typed .NET access, identity modules and independently scalable embedding generation.">
  <section className="detailSection wrap"><div className="detailHeading"><span>01</span><div><h2>Pick the deployment shape</h2><p>Start all-in-one for the smallest operational footprint. Separate embedding workers when inference becomes the scaling constraint.</p></div></div><div className="topologyGrid"><article><small>COMPACT</small><h3>All-in-one</h3><div className="topology"><span>CLIENT</span><i>→</i><strong>DB + ONNX</strong><i>→</i><b>DISK</b></div><p>Database and inference share one container. Best for evaluation, small teams and predictable traffic.</p></article><article className="active"><small>DISTRIBUTED</small><h3>Worker topology</h3><div className="topology"><span>CLIENTS</span><i>→</i><strong>JIGEN</strong><i>⇄</i><b>RABBITMQ + WORKERS</b></div><p>Scale CPU or GPU inference independently while the database remains the stable query endpoint.</p></article></div></section>
  <section className="detailBand"><div className="wrap splitDetail"><div><div className="eyebrow"><span /> CONTAINER TOPOLOGY</div><h2>Scale inference, not every component.</h2><p>Embedding workers compete as RabbitMQ consumers. Add replicas to increase throughput without changing the database endpoint or application clients.</p><ul className="checkList"><li>gRPC database API on port 3223</li><li>REST and Insight UI on port 13223</li><li>Persistent database volume at /data/jigendb</li><li>Models mounted under /data/onnx</li></ul></div><CodeBlock label="COMPOSE.YAML">{compose}</CodeBlock></div></section>
  <section className="detailSection wrap"><div className="detailHeading"><span>02</span><div><h2>A typed client over gRPC</h2><p>Collections preserve the .NET programming model while predicates become server-side content filters.</p></div></div><div className="splitDetail"><CodeBlock label="CLIENT.CS">{client}</CodeBlock><div className="sideNotes"><article><b>Context</b><p>Connection, TLS and target database.</p></article><article><b>VectorCollection&lt;T&gt;</b><p>Typed content, keys and search operations.</p></article><article><b>LINQ filters</b><p>Expressions translated to the server protocol.</p></article></div></div></section>
</DetailShell>}
