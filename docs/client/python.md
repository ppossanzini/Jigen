# Python client

The Python package lives in `src/Client/Jigen.Python` and communicates with a
Jigen server over the same gRPC contract used by `Jigen.Client`.

It provides:

- synchronous `Context` and asynchronous `AsyncContext`;
- typed collection operations, bulk ingestion and streaming keys;
- vector search or server-embedded text search;
- equality, collection-membership, AND and OR filters;
- MessagePack compatibility with the .NET client;
- optional LangChain and LlamaIndex vector-store adapters.

See the [package README](../../src/Client/Jigen.Python/README.md) for complete
installation and usage examples.

