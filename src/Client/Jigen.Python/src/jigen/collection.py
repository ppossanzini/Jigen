from __future__ import annotations

from collections.abc import AsyncIterator, Iterable, Iterator, Mapping, Sequence
from typing import Any, Generic, TypeVar

from ._proto import Jigen_pb2 as pb
from .errors import JigenOperationError
from .filters import Filter
from .models import (
    CollectionIndexInfo,
    CollectionInfo,
    Key,
    SearchOptions,
    SearchResult,
    SparseCollectionInfo,
    encode_key,
)
from .serialization import DocumentSerializer, MessagePackSerializer

T = TypeVar("T")

# One vector, or several vectors stored together as a multi-vector entry.
VectorInput = Sequence[float] | Sequence[Sequence[float]]

# A sparse (lexical) query: term identifier to weight.
SparseQuery = Mapping[int, float]


def _apply_embedding(request: pb.Vector, embedding: VectorInput | None) -> None:
    """Fills the entry's vectors from one vector or a list of them.

    A sequence of numbers is a single vector and becomes a one-vector entry; a
    sequence of such sequences stores one entry per vector, e.g. one embedding
    per passage. All vectors must share the collection dimensions.
    """
    if embedding is None:
        return

    values = list(embedding)
    if not values:
        return

    if isinstance(values[0], (int, float)):
        request.Vectors.add(Embeddings=values)
        return

    for vector in values:
        if isinstance(vector, (int, float)):
            raise TypeError("embedding mixes scalars and vectors")
        request.Vectors.add(Embeddings=list(vector))


def _sparse_terms(sparse: SparseQuery) -> pb.SparseTerms:
    """Message holding the term-id to weight map of one vector."""
    terms = pb.SparseTerms()
    for term, weight in sparse.items():
        if term < 0:
            raise ValueError("sparse term ids cannot be negative")
        terms.Terms[term] = float(weight)
    return terms


def _sparse_lists(sparse: SparseQuery | Sequence[SparseQuery] | None, vector_count: int) -> list:
    """Sparse term lists aligned by index with an entry's vectors.

    A single mapping describes a one-vector entry; a list gives one mapping per
    vector. A list longer than the vectors would attach terms to vectors that do
    not exist, so it is refused.
    """
    if sparse is None:
        return []

    if isinstance(sparse, Mapping):
        if vector_count > 1:
            raise ValueError("a multi-vector entry needs one sparse mapping per vector")
        return [_sparse_terms(sparse)]

    values = list(sparse)
    if len(values) > vector_count:
        raise ValueError("sparse has more term lists than vectors; they are aligned by index")
    return [_sparse_terms(value) for value in values]


def _tuning(options: SearchOptions | None) -> pb.SearchTuning | None:
    if options is None:
        return None
    result = pb.SearchTuning(EfSearch=options.ef_search, NoContent=options.no_content)
    if options.min_dense_score is not None:
        result.MinDenseScore = options.min_dense_score
    if options.min_sparse_score is not None:
        result.MinSparseScore = options.min_sparse_score
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
        raw_sparse = info.Sparse
        sparse = SparseCollectionInfo(
            raw_sparse.Segments,
            raw_sparse.Terms,
            raw_sparse.Blocks,
            raw_sparse.BlockSize,
            raw_sparse.Tombstones,
            raw_sparse.SizeBytes,
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

    def _vector(
        self,
        key: Key,
        content: T,
        embedding: VectorInput | None,
        sparse: SparseQuery | Sequence[SparseQuery] | None = None,
    ) -> pb.Vector:
        request = pb.Vector(
            Database=self.database,
            Collection=self.name,
            Key=encode_key(key),
            Content=self.serializer.serialize(content),
        )
        _apply_embedding(request, embedding)
        for terms in _sparse_lists(sparse, len(request.Vectors)):
            request.Sparse.append(terms)
        return request

    def _document(
        self,
        key: Key,
        content: T,
        text: str,
        sparse: SparseQuery | None = None,
        sparse_from_sentence: bool = False,
    ) -> pb.Document:
        request = pb.Document(
            Database=self.database,
            Collection=self.name,
            Key=encode_key(key),
            Content=self.serializer.serialize(content),
            Sentence=text,
        )
        if sparse:
            request.Sparse.CopyFrom(_sparse_terms(sparse))
        if sparse_from_sentence:
            request.SparseFromSentence = True
        return request

    def _vector_request(
        self,
        embedding: Sequence[float] | None,
        top: int,
        filter: Filter | None,
        options: SearchOptions | None,
        sparse: SparseQuery | None = None,
    ) -> pb.SearchVectorRequest:
        request = pb.SearchVectorRequest(
            Database=self.database,
            Collection=self.name,
            Top=top,
        )
        if embedding:
            request.Embeddings.extend(embedding)
        if filter is not None:
            request.Filter.CopyFrom(filter.node)
        if sparse:
            request.SparseQuery.update(sparse)
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
        sparse: SparseQuery | None = None,
        sparse_from_sentence: bool = False,
    ) -> pb.SearchDocumentRequest:
        request = pb.SearchDocumentRequest(
            Database=self.database,
            Collection=self.name,
            Sentence=text,
            Top=top,
        )
        if filter is not None:
            request.Filter.CopyFrom(filter.node)
        if sparse:
            request.SparseQuery.update(sparse)
        if sparse_from_sentence:
            request.SparseFromSentence = True
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
                dense_score=item.DenseScore if item.HasField("DenseScore") else None,
                sparse_score=item.SparseScore if item.HasField("SparseScore") else None,
            )
            for item in response.Results
        ]

    def _search_request(
        self,
        dense: str | Sequence[float] | None,
        sparse: SparseQuery | None,
        top: int,
        filter: Filter | None,
        options: SearchOptions | None,
        sparse_from_sentence: bool = False,
    ) -> pb.SearchVectorRequest | pb.SearchDocumentRequest:
        if isinstance(dense, str):
            return self._text_request(dense, top, filter, options, sparse, sparse_from_sentence)
        return self._vector_request(dense, top, filter, options, sparse)


class VectorCollection(_CollectionBase[T]):
    """Synchronous, typed collection facade mirroring Jigen.Client."""

    def _run_search(
        self,
        dense: str | Sequence[float] | None,
        sparse: SparseQuery | None,
        top: int,
        filter: Filter | None,
        options: SearchOptions | None,
        sparse_from_sentence: bool = False,
    ) -> pb.SearchVectorResponse:
        request = self._search_request(dense, sparse, top, filter, options, sparse_from_sentence)
        if isinstance(request, pb.SearchDocumentRequest):
            return self._stub.SearchDocument(request)
        return self._stub.SearchVector(request)

    def upsert(
        self,
        key: Key,
        content: T,
        *,
        embedding: VectorInput | None = None,
        text: str | None = None,
        sparse: SparseQuery | Sequence[SparseQuery] | None = None,
        sparse_from_sentence: bool = False,
    ) -> None:
        if (embedding is None) == (text is None):
            raise ValueError("provide exactly one of embedding or text")
        if sparse_from_sentence and text is None:
            raise ValueError(
                "sparse_from_sentence needs text: the server derives it from the sentence"
            )
        response = (
            self._stub.SetVector(self._vector(key, content, embedding, sparse))
            if embedding is not None
            else self._stub.SetDocument(
                self._document(key, content, text or "", sparse, sparse_from_sentence)
            )
        )
        if not response.Success:
            raise JigenOperationError(response.Message)

    def append(
        self,
        key: Key,
        content: T,
        *,
        embedding: VectorInput | None = None,
        text: str | None = None,
        sparse: SparseQuery | Sequence[SparseQuery] | None = None,
        sparse_from_sentence: bool = False,
    ) -> None:
        if (embedding is None) == (text is None):
            raise ValueError("provide exactly one of embedding or text")
        if sparse_from_sentence and text is None:
            raise ValueError(
                "sparse_from_sentence needs text: the server derives it from the sentence"
            )
        response = (
            self._stub.AppendVector(self._vector(key, content, embedding, sparse))
            if embedding is not None
            else self._stub.AppendDocument(
                self._document(key, content, text or "", sparse, sparse_from_sentence)
            )
        )
        if not response.Success:
            raise JigenOperationError(response.Message)

    def upsert_many(self, entries: Iterable[tuple]) -> int:
        """Streams vector entries: ``(key, content, vector)`` with optional sparse terms.

        A fourth item carries the sparse terms of the entry: one mapping for a
        one-vector entry, or a list of mappings aligned with the vectors.
        """
        response = self._stub.SetVectors(self._vector(*entry) for entry in entries)
        if not response.Success:
            raise JigenOperationError(response.Message)
        return response.Accepted

    def upsert_texts(self, entries: Iterable[tuple]) -> int:
        """Streams document entries: ``(key, content, text)`` with optional sparse terms."""
        response = self._stub.SetDocuments(self._document(*entry) for entry in entries)
        if not response.Success:
            raise JigenOperationError(response.Message)
        return response.Accepted

    def search(
        self,
        dense: str | Sequence[float] | None = None,
        sparse: SparseQuery | None = None,
        *,
        top: int = 10,
        filter: Filter | None = None,
        options: SearchOptions | None = None,
        sparse_from_sentence: bool = False,
    ) -> list[SearchResult[T]]:
        """Hybrid search: the dense and the sparse half fused by reciprocal rank.

        A text fills the dense half (the server embeds it) and, with
        ``sparse_from_sentence``, the sparse half too — the same tokenizer pass
        produces both. Either half may be omitted, but not both.
        """
        if dense is None and not sparse:
            raise ValueError("a hybrid search needs a dense query, a sparse query, or both")
        if isinstance(dense, str) and not dense.strip():
            dense = None
        return self._results(
            self._run_search(dense, sparse, top, filter, options, sparse_from_sentence)
        )

    # ``search_hybrid`` stays as an alias of the hybrid ``search``.
    search_hybrid = search

    def search_dense(
        self,
        query: str | Sequence[float],
        *,
        top: int = 10,
        filter: Filter | None = None,
        options: SearchOptions | None = None,
    ) -> list[SearchResult[T]]:
        """Dense half only: a text is embedded by the server, a vector is used as is."""
        if isinstance(query, str):
            if not query.strip():
                return []
        elif not query:
            return []
        return self._results(self._run_search(query, None, top, filter, options))

    def search_sparse(
        self,
        sparse: SparseQuery,
        *,
        top: int = 10,
        filter: Filter | None = None,
        options: SearchOptions | None = None,
    ) -> list[SearchResult[T]]:
        """Sparse half only: the query maps term identifiers to weights."""
        if not sparse:
            return []
        return self._results(self._run_search(None, sparse, top, filter, options))

    def filter(self, filter: Filter) -> list[SearchResult[T]]:
        request = pb.SearchFilterRequest(
            Database=self.database, Collection=self.name, Filter=filter.node
        )
        return self._results(self._stub.SearchFilter(request))

    def get(self, key: Key) -> T | None:
        response = self._stub.GetContent(self._item_key(key))
        return self.serializer.deserialize(response.content) if response.content else None

    def get_embeddings(self, key: Key) -> list[list[float]] | None:
        vectors = self._stub.GetEmbeddings(self._item_key(key)).Vectors
        return [list(vector.Embeddings) for vector in vectors] if vectors else None

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

    async def _run_search(
        self,
        dense: str | Sequence[float] | None,
        sparse: SparseQuery | None,
        top: int,
        filter: Filter | None,
        options: SearchOptions | None,
        sparse_from_sentence: bool = False,
    ) -> pb.SearchVectorResponse:
        request = self._search_request(dense, sparse, top, filter, options, sparse_from_sentence)
        if isinstance(request, pb.SearchDocumentRequest):
            return await self._stub.SearchDocument(request)
        return await self._stub.SearchVector(request)

    async def upsert(
        self,
        key: Key,
        content: T,
        *,
        embedding: VectorInput | None = None,
        text: str | None = None,
        sparse: SparseQuery | Sequence[SparseQuery] | None = None,
        sparse_from_sentence: bool = False,
    ) -> None:
        if (embedding is None) == (text is None):
            raise ValueError("provide exactly one of embedding or text")
        if sparse_from_sentence and text is None:
            raise ValueError(
                "sparse_from_sentence needs text: the server derives it from the sentence"
            )
        response = await (
            self._stub.SetVector(self._vector(key, content, embedding, sparse))
            if embedding is not None
            else self._stub.SetDocument(
                self._document(key, content, text or "", sparse, sparse_from_sentence)
            )
        )
        if not response.Success:
            raise JigenOperationError(response.Message)

    async def upsert_many(self, entries: Iterable[tuple]) -> int:
        """Streams vector entries: ``(key, content, vector)`` with optional sparse terms."""

        async def requests() -> AsyncIterator[pb.Vector]:
            for entry in entries:
                yield self._vector(*entry)

        response = await self._stub.SetVectors(requests())
        if not response.Success:
            raise JigenOperationError(response.Message)
        return response.Accepted

    async def upsert_texts(self, entries: Iterable[tuple]) -> int:
        """Streams document entries: ``(key, content, text)`` with optional sparse terms."""

        async def requests() -> AsyncIterator[pb.Document]:
            for entry in entries:
                yield self._document(*entry)

        response = await self._stub.SetDocuments(requests())
        if not response.Success:
            raise JigenOperationError(response.Message)
        return response.Accepted

    async def search(
        self,
        dense: str | Sequence[float] | None = None,
        sparse: SparseQuery | None = None,
        *,
        top: int = 10,
        filter: Filter | None = None,
        options: SearchOptions | None = None,
        sparse_from_sentence: bool = False,
    ) -> list[SearchResult[T]]:
        """Hybrid search: the dense and the sparse half fused by reciprocal rank.

        A text fills the dense half (the server embeds it) and, with
        ``sparse_from_sentence``, the sparse half too — the same tokenizer pass
        produces both. Either half may be omitted, but not both.
        """
        if dense is None and not sparse:
            raise ValueError("a hybrid search needs a dense query, a sparse query, or both")
        if isinstance(dense, str) and not dense.strip():
            dense = None
        return self._results(
            await self._run_search(dense, sparse, top, filter, options, sparse_from_sentence)
        )

    # ``search_hybrid`` stays as an alias of the hybrid ``search``.
    search_hybrid = search

    async def search_dense(
        self,
        query: str | Sequence[float],
        *,
        top: int = 10,
        filter: Filter | None = None,
        options: SearchOptions | None = None,
    ) -> list[SearchResult[T]]:
        """Dense half only: a text is embedded by the server, a vector is used as is."""
        if isinstance(query, str):
            if not query.strip():
                return []
        elif not query:
            return []
        return self._results(await self._run_search(query, None, top, filter, options))

    async def search_sparse(
        self,
        sparse: SparseQuery,
        *,
        top: int = 10,
        filter: Filter | None = None,
        options: SearchOptions | None = None,
    ) -> list[SearchResult[T]]:
        """Sparse half only: the query maps term identifiers to weights."""
        if not sparse:
            return []
        return self._results(await self._run_search(None, sparse, top, filter, options))

    async def get(self, key: Key) -> T | None:
        response = await self._stub.GetContent(self._item_key(key))
        return self.serializer.deserialize(response.content) if response.content else None

    async def get_embeddings(self, key: Key) -> list[list[float]] | None:
        vectors = (await self._stub.GetEmbeddings(self._item_key(key))).Vectors
        return [list(vector.Embeddings) for vector in vectors] if vectors else None

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
