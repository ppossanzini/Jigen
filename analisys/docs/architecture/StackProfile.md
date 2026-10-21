# Stack Profile

Status: Locked
Round: 2 + incremento vettori sparsi (2026-10-21)
Input criteria: [Technical analysis](../analysis/dossier.md#technical-analysis) (analysis/dossier.md#technical-analysis)

## Slots

| Slot | Tecnologia | Versione | Criterio soddisfatto |
| --- | --- | --- | --- |
| Piattaforma di esecuzione | .NET / C# | 10.0 (SDK `net10.0`) | Linguaggio del nucleo, calcolo vettoriale locale, hosting di due superfici di rete |
| Client .NET | .NET | `net8.0` | Consumo da applicazioni LTS, indipendente dal runtime del server |
| Mediazione | Hikyaku (+ Hikyaku.Contracts) | 9.4.3 / 9.1.2 | Un solo dispatcher per comandi e query su tutte le superfici |
| Trasporto fra processi | Hikyaku.Kaido + RabbitMQ | 9.4.7 | Consegna affidabile e consumo concorrente da più lavoratori |
| Mapping | MapZilla | 9.1.5 | Conversione fra contratti e modelli senza codice ripetuto |
| Archivio su file | Jigen.Store (proprietario) | in-repo | Append-only, mappatura in memoria, lock esclusivo, recupero dopo arresto |
| Indice approssimato | Jigen.Indexer.HNSW (proprietario) | in-repo | Persistenza su disco, inserimenti concorrenti, cancellazione logica, quantizzazione |
| Indice dei termini | Jigen.Indexer.Sparse (proprietario) | in-repo | Indice invertito persistente, ricostruibile, sotto lo stesso scrittore singolo |
| Fusione dei risultati | Implementazione propria di RRF nel server | in-repo | Ordine unico senza confronto fra scale, deterministico |
| Numerica | System.Numerics.Tensors + System.IO.Hashing | 10.0.9 | Distanze e hash senza allocazioni per voce |
| Serializzazione persistita | MessagePack | 3.1.7 | Contenuto e strutture dell'indice compatti e versionabili |
| Serializzazione REST | Newtonsoft.Json | 13.0.4 | Payload JSON con oggetti polimorfi |
| Superficie binaria | gRPC su ASP.NET Core | Grpc.AspNetCore 2.80.0, Google.Protobuf 3.35.1, Grpc.Tools 2.82.0 | Operazioni tipizzate e flussi in ingresso e in uscita |
| Superficie testuale | ASP.NET Core MVC + OpenAPI + Scalar | ASP.NET Core 10.0.9, Scalar 2.16.10 | Amministrazione e uso da HTTP, documentazione interattiva |
| Runtime di embedding | ONNX Runtime | 1.27.0 (CPU, CUDA), DirectML 1.24.4, OpenVINO 1.24.1 | Modelli locali su CPU e acceleratori, con ripiego su CPU |
| Estensioni ONNX | ONNX Runtime Extensions | 0.14.0 | Pre e post processing dentro il grafo |
| Tokenizzazione | Microsoft.ML.Tokenizers + Tokenizers.HuggingFace | 2.0.0 / 3.23.1 | Tokenizzatori locali e JSON di Hugging Face |
| Immagini | OpenCvSharp5 | 5.0.0.20260806 | Decodifica, ridimensionamento e tile |
| Identità | ASP.NET Core Identity + OpenIddict + EF Core SQLite | Identity 10.0.9, OpenIddict 7.5.0, EF Core 10.0.9 | Autenticazione a token, ruoli, client, archivio locale |
| Interfaccia amministrativa | Vue 3 + Vite + TypeScript + Naive UI + UnoCSS + ECharts | Vue 3.5.34, Naive UI 2.44.1, ECharts 6.0.0 | Tabelle, grafi, temi, una sola lingua |
| Client Python | Python + gRPC | 3.10+, grpcio 1.67+, protobuf 7+, msgpack 1.x | Client tipizzato e integrazioni LLM |
| Integrazioni LLM | LangChain `langchain-core`, LlamaIndex `llama-index-core` | 0.3–2.x / 0.12–1.x | Adattatori per framework Python |
| Container | Docker su `mcr.microsoft.com/dotnet/aspnet:10.0` | 10.0 | Separazione del calcolo dei vettori, archivio unitario |
| Pacchetti | NuGet | — | Libreria installabile |
| Test .NET | xUnit 2.9.3 + Microsoft.NET.Test.Sdk 18.7.0 | — | Test di comportamento per progetto |
| Benchmark | BenchmarkDotNet 0.14 | — | Misure riproducibili |
| Test Python | pytest 8 + ruff + mypy | — | Qualità del client Python |
| Qualità interfaccia | oxlint + eslint + `vue-tsc` | — | Tipi e lint prima della build |
| Documentazione | MkDocs con tema Read the Docs, pubblicato su Read the Docs | — | Documentazione pubblicata dal repository |
| Integrazione continua | GitHub Actions | — | Build, test, pacchetti e immagini |

## Slot dichiarati assenti

| Slot | Stato | Motivo |
| --- | --- | --- |
| Broker di messaggi obbligatorio | Assente | Il calcolo locale resta disponibile: il broker entra solo con la delega attiva |
| Database relazionale per i dati del prodotto | Assente | Solo l'identità usa un database relazionale; voci e indici restano su file |
| Serie temporali per le metriche | Assente | I campioni vivono in memoria nel processo |
| Provider `Microsoft.Extensions.VectorData` | Assente | Nessun progetto lo implementa: resta un obbligo di R-30 |
| Generazione dei vettori sparsi (SPLADE, ELSER, pesi BGE-M3) | Assente | I pesi sono forniti dal chiamante: decisione dello scouting sui vettori sparsi |
| Analizzatore di testo e BM25 nel server | Assente | Nessun requisito; cambierebbe la natura del prodotto |
| Seconda lingua dell'interfaccia | Assente | Solo `en-us` nel repository |
| Telemetria distribuita | Assente | Nessun requisito di tracciamento fra processi |
