from __future__ import annotations

from collections.abc import AsyncIterator, Iterable, Iterator, Sequence
from typing import Any, Generic, TypeVar

from ._proto import Jigen_pb2 as pb
from .errors import JigenOperationError
from .filters import Filter
from .models import (
    CollectionIndexInfo,
    CollectionInfo,
    CollectionSparseInfo,
    Key,
    MetadataSearchResult,
    SearchOptions,
    SearchResult,
    SparseVector,
    encode_key,
)
from .serialization import DocumentSerializer, MessagePackSerializer

T = TypeVar("T")


def _tuning(options: SearchOptions | None) -> pb.SearchTuning | None:
    if options is None:
        return None
    result = pb.SearchTuning(EfSearch=options.ef_search, NoContent=options.no_content)
    if options.min_score is not None:
        result.MinScore = options.min_score
    return result


def _map_info(info: pb.CollectionInfoResponse) -> CollectionInfo:
    index = None
    if info.HasField("Index"):
        raw = info.Index
        index = CollectionIndexInfo(
            raw.IndexSizeBytes,
            raw.Nodes,
            raw.DeletedNodes,
            raw.MaxLevel,
            tuple(raw.NodesPerLevel),
            raw.AverageDegree,
            raw.Quantization,
        )
    sparse = None
    if info.HasField("Sparse"):
        raw = info.Sparse
        sparse = CollectionSparseInfo(
            raw.Terms,
            raw.Postings,
            raw.AveragePostingsPerTerm,
            raw.MemoryBytes,
            raw.RetiredTerms,
            raw.Built,
        )
    return CollectionInfo(
        info.Name,
        info.Vectors,
        info.Dimensions,
        info.ContentSize,
        info.VectorSize,
        index,
        sparse,
    )


class _CollectionBase(Generic[T]):
    def __init__(
        self,
        stub: Any,
        database: str,
        name: str,
        serializer: DocumentSerializer | None = None,
    ) -> None:
        if not database.strip():
            raise ValueError("database is required")
        if not name.strip():
            raise ValueError("collection name is required")
        self._stub = stub
        self.database = database
        self.name = name
        self.serializer = serializer or MessagePackSerializer()

    @property
    def _collection_key(self) -> pb.CollectionKey:
        return pb.CollectionKey(Database=self.database, Collection=self.name)

    def _item_key(self, key: Key) -> pb.ItemKey:
        return pb.ItemKey(Database=self.database, Collection=self.name, Key=encode_key(key))

    @staticmethod
    def _sparse_message(sparse: SparseVector) -> pb.SparseVector:
        return pb.SparseVector(Terms=sparse.terms, Weights=sparse.weights)

    def _vector(
        self,
        key: Key,
        content: T,
        embedding: Sequence[float] | None,
        sparse: SparseVector | None = None,
    ) -> pb.Vector:
        vector = pb.Vector(
            Database=self.database,
            Collection=self.name,
            Key=encode_key(key),
            Content=self.serializer.serialize(content),
            Embeddings=embedding or (),
        )
        if sparse is not None:
            vector.Sparse.CopyFrom(self._sparse_message(sparse))
        return vector

    def _document(
        self, key: Key, content: T, text: str, sparse: SparseVector | None = None
    ) -> pb.Document:
        document = pb.Document(
            Database=self.database,
            Collection=self.name,
            Key=encode_key(key),
            Content=self.serializer.serialize(content),
            Sentence=text,
        )
        if sparse is not None:
            document.Sparse.CopyFrom(self._sparse_message(sparse))
        return document

    def _sparse_request(
        self,
        sparse: SparseVector,
        top: int,
        filter: Filter | None,
        options: SearchOptions | None,
    ) -> pb.SearchSparseRequest:
        request = pb.SearchSparseRequest(
            Database=self.database,
            Collection=self.name,
            Sparse=self._sparse_message(sparse),
            Top=top,
        )
        if filter is not None:
            request.Filter.CopyFrom(filter.node)
        tuning = _tuning(options)
        if tuning is not None:
            request.Tuning.CopyFrom(tuning)
        return request

    def _hybrid_request(
        self,
        embedding: Sequence[float] | None,
        sparse: SparseVector | None,
        top: int,
        dense_weight: float,
        sparse_weight: float,
        filter: Filter | None,
        options: SearchOptions | None,
    ) -> pb.SearchHybridRequest:
        dense_supplied = bool(embedding)
        if not dense_supplied and sparse is None:
            raise ValueError("a hybrid search needs at least one of the two vectors")

        request = pb.SearchHybridRequest(
            Database=self.database, Collection=self.name, Top=top
        )
        if dense_supplied:
            request.Embeddings.extend(embedding or ())
        if sparse is not None:
            request.Sparse.CopyFrom(self._sparse_message(sparse))

        # The server refuses both vectors without both weights: declare them
        # here so a caller that forgot one fails before the round trip.
        if dense_supplied and sparse is not None:
            request.DenseWeight = dense_weight
            request.SparseWeight = sparse_weight
        elif dense_supplied:
            request.DenseWeight = dense_weight
        else:
            request.SparseWeight = sparse_weight

        if filter is not None:
            request.Filter.CopyFrom(filter.node)
        tuning = _tuning(options)
        if tuning is not None:
            request.Tuning.CopyFrom(tuning)
        return request

    def _vector_request(
        self,
        embedding: Sequence[float],
        top: int,
        filter: Filter | None,
        options: SearchOptions | None,
    ) -> pb.SearchVectorRequest:
        request = pb.SearchVectorRequest(
            Database=self.database,
            Collection=self.name,
            Embeddings=embedding,
            Top=top,
        )
        if filter is not None:
            request.Filter.CopyFrom(filter.node)
        tuning = _tuning(options)
        if tuning is not None:
            request.Tuning.CopyFrom(tuning)
        return request

    def _text_request(
        self,
        text: str,
        top: int,
        filter: Filter | None,
        options: SearchOptions | None,
    ) -> pb.SearchDocumentRequest:
        request = pb.SearchDocumentRequest(
            Database=self.database,
            Collection=self.name,
            Sentence=text,
            Top=top,
        )
        if filter is not None:
            request.Filter.CopyFrom(filter.node)
        tuning = _tuning(options)
        if tuning is not None:
            request.Tuning.CopyFrom(tuning)
        return request

    def _results(self, response: pb.SearchVectorResponse) -> list[SearchResult[T]]:
        return [
            SearchResult(
                key=item.Key,
                content=self.serializer.deserialize(item.Content) if item.Content else None,
                score=item.Score,
                in_dense=item.InDense,
                in_sparse=item.InSparse,
                dense_score=item.DenseScore,
                sparse_score=item.SparseScore,
            )
            for item in response.Results
        ]

    def _metadata_request(
        self,
        query: str | Sequence[float],
        top: int,
        candidate_budget: int,
        filter: Filter | None,
        options: SearchOptions | None,
    ) -> pb.SearchByMetadataRequest:
        if top <= 0:
            raise ValueError("top must be greater than zero")
        if candidate_budget <= 0:
            raise ValueError("candidate_budget must be greater than zero")

        request = pb.SearchByMetadataRequest(
            Database=self.database,
            Collection=self.name,
            Top=top,
            CandidateBudget=candidate_budget,
        )
        if isinstance(query, str):
            if not query.strip():
                raise ValueError("query is required")
            request.Sentence = query
        else:
            if not query:
                raise ValueError("query embeddings are required")
            request.Embeddings.extend(query)
        if filter is not None:
            request.Filter.CopyFrom(filter.node)
        tuning = _tuning(options)
        if tuning is not None:
            request.Tuning.CopyFrom(tuning)
        return request

    def _metadata_results(
        self, response: pb.SearchByMetadataResponse
    ) -> list[MetadataSearchResult[T]]:
        return [
            MetadataSearchResult(
                metadata_index=item.MetadataIndex,
                key=item.Key,
                content=self.serializer.deserialize(item.Content) if item.Content else None,
                score=item.Score,
            )
            for item in response.Results
        ]


class VectorCollection(_CollectionBase[T]):
    """Synchronous, typed collection facade mirroring Jigen.Client."""

    def upsert(
        self,
        key: Key,
        content: T,
        *,
        embedding: Sequence[float] | None = None,
        text: str | None = None,
        sparse: SparseVector | None = None,
    ) -> None:
        """Stores one entry, optionally with both vectors in the same write.

        Either ``embedding`` or ``text`` supplies the dense side (``text`` is
        embedded server-side); ``sparse`` is independent of both and is never
        derived from the text. ``sparse`` alone stores a lexical-only entry.
        """
        if embedding is None and text is None and sparse is None:
            raise ValueError("provide embedding, text or sparse")
        if embedding is not None and text is not None:
            raise ValueError("provide at most one of embedding or text")
        response = (
            self._stub.SetVector(self._vector(key, content, embedding, sparse))
            if embedding is not None or text is None
            else self._stub.SetDocument(self._document(key, content, text or "", sparse))
        )
        if not response.Success:
            raise JigenOperationError(response.Message)

    def append(
        self,
        key: Key,
        content: T,
        *,
        embedding: Sequence[float] | None = None,
        text: str | None = None,
    ) -> None:
        if (embedding is None) == (text is None):
            raise ValueError("provide exactly one of embedding or text")
        response = (
            self._stub.AppendVector(self._vector(key, content, embedding))
            if embedding is not None
            else self._stub.AppendDocument(self._document(key, content, text or ""))
        )
        if not response.Success:
            raise JigenOperationError(response.Message)

    def upsert_many(self, entries: Iterable[tuple[Key, T, Sequence[float]]]) -> int:
        response = self._stub.SetVectors(
            self._vector(key, content, vector) for key, content, vector in entries
        )
        if not response.Success:
            raise JigenOperationError(response.Message)
        return response.Accepted

    def upsert_texts(self, entries: Iterable[tuple[Key, T, str]]) -> int:
        response = self._stub.SetDocuments(
            self._document(key, content, text) for key, content, text in entries
        )
        if not response.Success:
            raise JigenOperationError(response.Message)
        return response.Accepted

    def search(
        self,
        query: str | Sequence[float],
        *,
        top: int = 10,
        filter: Filter | None = None,
        options: SearchOptions | None = None,
    ) -> list[SearchResult[T]]:
        if isinstance(query, str):
            if not query.strip():
                return []
            return self._results(
                self._stub.SearchDocument(self._text_request(query, top, filter, options))
            )
        if not query:
            return []
        return self._results(
            self._stub.SearchVector(self._vector_request(query, top, filter, options))
        )

    def filter(self, filter: Filter) -> list[SearchResult[T]]:
        request = pb.SearchFilterRequest(
            Database=self.database, Collection=self.name, Filter=filter.node
        )
        return self._results(self._stub.SearchFilter(request))

    # ── Shared metadata: many embeddings reference one metadata record. ──

    def create_metadata(self, content: T) -> int:
        """Creates a shared metadata record and returns its opaque index."""
        response = self._stub.CreateMetadata(
            pb.CreateMetadataRequest(
                Database=self.database, Content=self.serializer.serialize(content)
            )
        )
        return response.MetadataIndex

    def update_metadata(self, metadata_index: int, content: T) -> bool:
        """Replaces the content read by every embedding associated to the record."""
        response = self._stub.UpdateMetadata(
            pb.UpdateMetadataRequest(
                Database=self.database,
                MetadataIndex=metadata_index,
                Content=self.serializer.serialize(content),
            )
        )
        return response.Updated

    def get_metadata_index(self, key: Key) -> int:
        """Returns the associated metadata index, or -1 when absent."""
        response = self._stub.GetEmbeddingMetadata(
            pb.EmbeddingMetadataRequest(
                Database=self.database, Collection=self.name, Key=encode_key(key)
            )
        )
        return response.MetadataIndex

    def associate_metadata(self, key: Key, metadata_index: int) -> bool:
        """Associates or reassociates an existing key to a metadata record."""
        response = self._stub.AssociateEmbedding(
            pb.AssociateEmbeddingRequest(
                Database=self.database,
                Collection=self.name,
                Key=encode_key(key),
                MetadataIndex=metadata_index,
            )
        )
        if not response.Success:
            raise JigenOperationError(response.Message)
        return response.Success

    def search_by_metadata(
        self,
        query: str | Sequence[float],
        *,
        top: int = 10,
        candidate_budget: int,
        filter: Filter | None = None,
        options: SearchOptions | None = None,
    ) -> list[MetadataSearchResult[T]]:
        """Grouped search: one result per metadata record, best score wins.

        ``candidate_budget`` is mandatory and caps the sum of requested
        candidates across attempts; fewer than ``top`` groups may be returned.
        """
        request = self._metadata_request(query, top, candidate_budget, filter, options)
        return self._metadata_results(self._stub.SearchByMetadata(request))

    # ── Lexical slot ──

    def get_sparse(self, key: Key) -> SparseVector | None:
        """Reads the sparse vector stored with a key; ``None`` when absent."""
        message = self._stub.GetSparse(self._item_key(key))
        if not message.Terms:
            return None
        return SparseVector(terms=tuple(message.Terms), weights=tuple(message.Weights))

    def search_sparse(
        self,
        sparse: SparseVector,
        *,
        top: int = 10,
        filter: Filter | None = None,
        options: SearchOptions | None = None,
    ) -> list[SearchResult[T]]:
        """Exact lexical search over the entries sharing a term with the query."""
        return self._results(
            self._stub.SearchSparse(self._sparse_request(sparse, top, filter, options))
        )

    def search_hybrid(
        self,
        embedding: Sequence[float] | None = None,
        sparse: SparseVector | None = None,
        *,
        top: int = 10,
        dense_weight: float = 1.0,
        sparse_weight: float = 1.0,
        filter: Filter | None = None,
        options: SearchOptions | None = None,
    ) -> list[SearchResult[T]]:
        """Fuses the dense and the lexical ranking by position.

        A single modality needs no weight; both together need both, declared.
        """
        request = self._hybrid_request(
            embedding, sparse, top, dense_weight, sparse_weight, filter, options
        )
        return self._results(self._stub.SearchHybrid(request))

    def get(self, key: Key) -> T | None:
        response = self._stub.GetContent(self._item_key(key))
        return self.serializer.deserialize(response.content) if response.content else None

    def get_embedding(self, key: Key) -> list[float] | None:
        values = self._stub.GetEmbedding(self._item_key(key)).Embeddings
        return list(values) if values else None

    def contains(self, key: Key) -> bool:
        return self._stub.Contains(self._item_key(key)).Success

    def delete(self, key: Key) -> bool:
        return self._stub.DeleteVector(self._item_key(key)).Success

    def clear(self) -> bool:
        return self._stub.Clear(self._collection_key).Success

    def count(self) -> int:
        return self._stub.Count(self._collection_key).Count

    def keys(self) -> list[bytes]:
        return list(self._stub.GetAllKeys(self._collection_key).Keys)

    def stream_keys(self, chunk_size: int = 0) -> Iterator[bytes]:
        request = pb.StreamKeysRequest(
            Database=self.database, Collection=self.name, ChunkSize=chunk_size
        )
        for chunk in self._stub.StreamKeys(request):
            yield from chunk.Keys

    def info(self) -> CollectionInfo:
        return _map_info(self._stub.GetCollectionInfo(self._collection_key))


class AsyncVectorCollection(_CollectionBase[T]):
    """Async collection facade backed by ``grpc.aio``."""

    async def upsert(
        self,
        key: Key,
        content: T,
        *,
        embedding: Sequence[float] | None = None,
        text: str | None = None,
        sparse: SparseVector | None = None,
    ) -> None:
        """Stores one entry, optionally with both vectors in the same write."""
        if embedding is None and text is None and sparse is None:
            raise ValueError("provide embedding, text or sparse")
        if embedding is not None and text is not None:
            raise ValueError("provide at most one of embedding or text")
        response = await (
            self._stub.SetVector(self._vector(key, content, embedding, sparse))
            if embedding is not None or text is None
            else self._stub.SetDocument(self._document(key, content, text or "", sparse))
        )
        if not response.Success:
            raise JigenOperationError(response.Message)

    async def upsert_many(self, entries: Iterable[tuple[Key, T, Sequence[float]]]) -> int:
        async def requests() -> AsyncIterator[pb.Vector]:
            for key, content, vector in entries:
                yield self._vector(key, content, vector)

        response = await self._stub.SetVectors(requests())
        if not response.Success:
            raise JigenOperationError(response.Message)
        return response.Accepted

    async def upsert_texts(self, entries: Iterable[tuple[Key, T, str]]) -> int:
        async def requests() -> AsyncIterator[pb.Document]:
            for key, content, text in entries:
                yield self._document(key, content, text)

        response = await self._stub.SetDocuments(requests())
        if not response.Success:
            raise JigenOperationError(response.Message)
        return response.Accepted

    async def search(
        self,
        query: str | Sequence[float],
        *,
        top: int = 10,
        filter: Filter | None = None,
        options: SearchOptions | None = None,
    ) -> list[SearchResult[T]]:
        if isinstance(query, str):
            if not query.strip():
                return []
            return self._results(
                await self._stub.SearchDocument(self._text_request(query, top, filter, options))
            )
        if not query:
            return []
        return self._results(
            await self._stub.SearchVector(self._vector_request(query, top, filter, options))
        )

    # ── Lexical slot ──

    async def get_sparse(self, key: Key) -> SparseVector | None:
        """Reads the sparse vector stored with a key; ``None`` when absent."""
        message = await self._stub.GetSparse(self._item_key(key))
        if not message.Terms:
            return None
        return SparseVector(terms=tuple(message.Terms), weights=tuple(message.Weights))

    async def search_sparse(
        self,
        sparse: SparseVector,
        *,
        top: int = 10,
        filter: Filter | None = None,
        options: SearchOptions | None = None,
    ) -> list[SearchResult[T]]:
        """Exact lexical search over the entries sharing a term with the query."""
        return self._results(
            await self._stub.SearchSparse(self._sparse_request(sparse, top, filter, options))
        )

    async def search_hybrid(
        self,
        embedding: Sequence[float] | None = None,
        sparse: SparseVector | None = None,
        *,
        top: int = 10,
        dense_weight: float = 1.0,
        sparse_weight: float = 1.0,
        filter: Filter | None = None,
        options: SearchOptions | None = None,
    ) -> list[SearchResult[T]]:
        """Fuses the dense and the lexical ranking by position."""
        request = self._hybrid_request(
            embedding, sparse, top, dense_weight, sparse_weight, filter, options
        )
        return self._results(await self._stub.SearchHybrid(request))

    async def get(self, key: Key) -> T | None:
        response = await self._stub.GetContent(self._item_key(key))
        return self.serializer.deserialize(response.content) if response.content else None

    # ── Shared metadata: many embeddings reference one metadata record. ──

    async def create_metadata(self, content: T) -> int:
        """Creates a shared metadata record and returns its opaque index."""
        response = await self._stub.CreateMetadata(
            pb.CreateMetadataRequest(
                Database=self.database, Content=self.serializer.serialize(content)
            )
        )
        return response.MetadataIndex

    async def update_metadata(self, metadata_index: int, content: T) -> bool:
        """Replaces the content read by every embedding associated to the record."""
        response = await self._stub.UpdateMetadata(
            pb.UpdateMetadataRequest(
                Database=self.database,
                MetadataIndex=metadata_index,
                Content=self.serializer.serialize(content),
            )
        )
        return response.Updated

    async def get_metadata_index(self, key: Key) -> int:
        """Returns the associated metadata index, or -1 when absent."""
        response = await self._stub.GetEmbeddingMetadata(
            pb.EmbeddingMetadataRequest(
                Database=self.database, Collection=self.name, Key=encode_key(key)
            )
        )
        return response.MetadataIndex

    async def associate_metadata(self, key: Key, metadata_index: int) -> bool:
        """Associates or reassociates an existing key to a metadata record."""
        response = await self._stub.AssociateEmbedding(
            pb.AssociateEmbeddingRequest(
                Database=self.database,
                Collection=self.name,
                Key=encode_key(key),
                MetadataIndex=metadata_index,
            )
        )
        if not response.Success:
            raise JigenOperationError(response.Message)
        return response.Success

    async def search_by_metadata(
        self,
        query: str | Sequence[float],
        *,
        top: int = 10,
        candidate_budget: int,
        filter: Filter | None = None,
        options: SearchOptions | None = None,
    ) -> list[MetadataSearchResult[T]]:
        """Grouped search: one result per metadata record, best score wins."""
        request = self._metadata_request(query, top, candidate_budget, filter, options)
        return self._metadata_results(await self._stub.SearchByMetadata(request))

    async def get_embedding(self, key: Key) -> list[float] | None:
        values = (await self._stub.GetEmbedding(self._item_key(key))).Embeddings
        return list(values) if values else None

    async def contains(self, key: Key) -> bool:
        return (await self._stub.Contains(self._item_key(key))).Success

    async def delete(self, key: Key) -> bool:
        return (await self._stub.DeleteVector(self._item_key(key))).Success

    async def clear(self) -> bool:
        return (await self._stub.Clear(self._collection_key)).Success

    async def count(self) -> int:
        return (await self._stub.Count(self._collection_key)).Count

    async def stream_keys(self, chunk_size: int = 0) -> AsyncIterator[bytes]:
        request = pb.StreamKeysRequest(
            Database=self.database, Collection=self.name, ChunkSize=chunk_size
        )
        async for chunk in self._stub.StreamKeys(request):
            for key in chunk.Keys:
                yield key

    async def info(self) -> CollectionInfo:
        return _map_info(await self._stub.GetCollectionInfo(self._collection_key))
