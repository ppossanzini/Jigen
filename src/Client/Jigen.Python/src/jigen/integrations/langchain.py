from __future__ import annotations

import uuid
from collections.abc import Iterable, Sequence
from typing import Any

try:
    from langchain_core.documents import Document
    from langchain_core.embeddings import Embeddings
    from langchain_core.vectorstores import VectorStore
except ImportError as exc:  # pragma: no cover - depends on optional package
    raise ImportError(
        "Install the LangChain adapter with: pip install 'jigen-client[langchain]'"
    ) from exc

from ..collection import VectorCollection
from ..filters import Filter, equals


def _metadata_filter(value: dict[str, Any] | Filter | None) -> Filter | None:
    if value is None or isinstance(value, Filter):
        return value
    result: Filter | None = None
    for key, expected in value.items():
        item = equals(f"metadata.{key}", expected)
        result = item if result is None else result & item
    return result


class JigenVectorStore(VectorStore):
    """LangChain VectorStore backed by a Jigen collection.

    When ``embedding`` is omitted, text is sent to Jigen and embedded by the
    configured server-side ONNX model. Supplying a LangChain ``Embeddings``
    object keeps embedding generation client-side.
    """

    def __init__(
        self, collection: VectorCollection[dict[str, Any]], embedding: Embeddings | None = None
    ) -> None:
        self.collection = collection
        self._embedding = embedding

    @property
    def embeddings(self) -> Embeddings | None:
        return self._embedding

    def add_texts(
        self,
        texts: Iterable[str],
        metadatas: list[dict[str, Any]] | None = None,
        *,
        ids: list[str] | None = None,
        **_: Any,
    ) -> list[str]:
        values = list(texts)
        metadatas = metadatas or [{} for _ in values]
        ids = ids or [str(uuid.uuid4()) for _ in values]
        if not (len(values) == len(metadatas) == len(ids)):
            raise ValueError("texts, metadatas, and ids must have the same length")
        payloads = [
            {"text": text, "metadata": metadata}
            for text, metadata in zip(values, metadatas, strict=True)
        ]
        if self._embedding is None:
            self.collection.upsert_texts(zip(ids, payloads, values, strict=True))
        else:
            vectors = self._embedding.embed_documents(values)
            self.collection.upsert_many(zip(ids, payloads, vectors, strict=True))
        return ids

    def delete(self, ids: list[str] | None = None, **_: Any) -> bool | None:
        if ids is None:
            return self.collection.clear()
        return all(self.collection.delete(item) for item in ids)

    def get_by_ids(self, ids: Sequence[str], /) -> list[Document]:
        documents: list[Document] = []
        for identifier in ids:
            payload = self.collection.get(identifier)
            if payload is not None:
                documents.append(self._document(identifier.encode(), payload))
        return documents

    def similarity_search(self, query: str, k: int = 4, **kwargs: Any) -> list[Document]:
        return [document for document, _ in self.similarity_search_with_score(query, k, **kwargs)]

    def similarity_search_with_score(
        self, query: str, k: int = 4, **kwargs: Any
    ) -> list[tuple[Document, float]]:
        filter_value = _metadata_filter(kwargs.get("filter"))
        min_score = kwargs.get("score_threshold")
        search_options = kwargs.get("options")
        if search_options is None and min_score is not None:
            from ..models import SearchOptions

            search_options = SearchOptions(min_score=float(min_score))
        search_query: str | list[float] = (
            query if self._embedding is None else self._embedding.embed_query(query)
        )
        return [
            (self._document(result.key, result.content or {}), result.score)
            for result in self.collection.search(
                search_query, top=k, filter=filter_value, options=search_options
            )
        ]

    def similarity_search_by_vector(
        self, embedding: list[float], k: int = 4, **kwargs: Any
    ) -> list[Document]:
        return [
            self._document(result.key, result.content or {})
            for result in self.collection.search(
                embedding,
                top=k,
                filter=_metadata_filter(kwargs.get("filter")),
                options=kwargs.get("options"),
            )
        ]

    @staticmethod
    def _document(key: bytes, payload: dict[str, Any]) -> Document:
        identifier = key.decode("utf-8", errors="replace")
        return Document(
            id=identifier,
            page_content=payload.get("text", ""),
            metadata=payload.get("metadata", {}),
        )

    @classmethod
    def from_texts(
        cls,
        texts: list[str],
        embedding: Embeddings | None,
        metadatas: list[dict[str, Any]] | None = None,
        **kwargs: Any,
    ) -> JigenVectorStore:
        collection = kwargs.pop("collection")
        store = cls(collection=collection, embedding=embedding)
        store.add_texts(texts, metadatas, ids=kwargs.pop("ids", None), **kwargs)
        return store
