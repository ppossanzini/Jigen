from llama_index.core import VectorStoreIndex

from jigen import ConnectionOptions, Context
from jigen.integrations.llama_index import JigenVectorStore

context = Context(ConnectionOptions(database="rag"))
store = JigenVectorStore(context.collection("knowledge"))
index = VectorStoreIndex.from_vector_store(store)
query_engine = index.as_query_engine(similarity_top_k=4)
print(query_engine.query("How does Jigen persist its HNSW graph?"))
