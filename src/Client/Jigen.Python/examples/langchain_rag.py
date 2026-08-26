from jigen import ConnectionOptions, Context
from jigen.integrations.langchain import JigenVectorStore

context = Context(ConnectionOptions(database="rag"))
store = JigenVectorStore(context.collection("knowledge"))

# No external embedding dependency: Jigen embeds text server-side.
store.add_texts(
    ["Jigen can run embedded or as a server."],
    metadatas=[{"source": "architecture.md"}],
    ids=["architecture-1"],
)

retriever = store.as_retriever(search_kwargs={"k": 4})
documents = retriever.invoke("How can I deploy Jigen?")
print(documents)
