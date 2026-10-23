from __future__ import annotations

from collections.abc import AsyncIterator, Iterable, Iterator, Sequence
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
    encode_key,
)
from .serialization import DocumentSerializer, MessagePackSerializer

T = TypeVar("T")

# One vector, or several vectors stored together as a multi-vector entry.
VectorInput = Sequence[float] | Sequence[Sequence[float]]


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
    return CollectionInfo(
        info.Name, info.Vectors, info.Dimensions, info.ContentSize, info.VectorSize, index
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

    def _vector(self, key: Key, content: T, embedding: VectorInput | None) -> pb.Vector:
        request = pb.Vector(
            Database=self.database,
            Collection=self.name,
            Key=encode_key(key),
            Content=self.serializer.serialize(content),
        )
        _apply_embedding(request, embedding)
        return request

    def _document(self, key: Key, content: T, text: str) -> pb.Document:
        return pb.Document(
            Database=self.database,
            Collection=self.name,
            Key=encode_key(key),
            Content=self.serializer.serialize(content),
            Sentence=text,
        )

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
        embedding: VectorInput | None = None,
        text: str | None = None,
    ) -> None:
        if (embedding is None) == (text is None):
            raise ValueError("provide exactly one of embedding or text")
        response = (
            self._stub.SetVector(self._vector(key, content, embedding))
            if embedding is not None
            else self._stub.SetDocument(self._document(key, content, text or ""))
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

    def upsert_many(self, entries: Iterable[tuple[Key, T, VectorInput]]) -> int:
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

    async def upsert(
        self,
        key: Key,
        content: T,
        *,
        embedding: VectorInput | None = None,
        text: str | None = None,
    ) -> None:
        if (embedding is None) == (text is None):
            raise ValueError("provide exactly one of embedding or text")
        response = await (
            self._stub.SetVector(self._vector(key, content, embedding))
            if embedding is not None
            else self._stub.SetDocument(self._document(key, content, text or ""))
        )
        if not response.Success:
            raise JigenOperationError(response.Message)

    async def upsert_many(self, entries: Iterable[tuple[Key, T, VectorInput]]) -> int:
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
