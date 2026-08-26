from __future__ import annotations

from collections.abc import Sequence
from typing import Any

try:
    from llama_index.core.schema import BaseNode, TextNode
    from llama_index.core.vector_stores.types import (
        BasePydanticVectorStore,
        FilterCondition,
        FilterOperator,
        MetadataFilter,
        MetadataFilters,
        VectorStoreQuery,
        VectorStoreQueryResult,
    )
except ImportError as exc:  # pragma: no cover - depends on optional package
    raise ImportError(
        "Install the LlamaIndex adapter with: pip install 'jigen-client[llama-index]'"
    ) from exc

from ..collection import VectorCollection
from ..filters import Filter, contains, equals


def _filters(value: MetadataFilters | None) -> Filter | None:
    if value is None or not value.filters:
        return None
    items: list[Filter] = []
    for item in value.filters:
        if isinstance(item, MetadataFilters):
            nested = _filters(item)
            if nested is not None:
                items.append(nested)
            continue
        if not isinstance(item, MetadataFilter):
            raise TypeError(f"Unsupported LlamaIndex filter: {type(item)!r}")
        path = f"metadata.{item.key}"
        if item.operator == FilterOperator.EQ:
            items.append(equals(path, item.value))
        elif item.operator in (FilterOperator.ANY, FilterOperator.CONTAINS):
            values = item.value if isinstance(item.value, list) else [item.value]
            if not values:
                raise ValueError("Jigen cannot translate an empty membership filter")
            membership = contains(path, values[0])
            for expected in values[1:]:
                membership = membership | contains(path, expected)
            items.append(membership)
        else:
            raise ValueError(f"Jigen does not support LlamaIndex operator {item.operator.value!r}")
    if not items:
        return None
    if value.condition not in (FilterCondition.AND, FilterCondition.OR):
        raise ValueError(f"Jigen does not support filter condition {value.condition!r}")
    result = items[0]
    for item in items[1:]:
        result = result & item if value.condition == FilterCondition.AND else result | item
    return result


class JigenVectorStore(BasePydanticVectorStore):
    """LlamaIndex vector-store adapter using precomputed node embeddings."""

    stores_text: bool = True
    is_embedding_query: bool = True
    flat_metadata: bool = False
    _collection: Any = None

    def __init__(self, collection: VectorCollection[dict[str, Any]], **kwargs: Any) -> None:
        super().__init__(**kwargs)
        object.__setattr__(self, "_collection", collection)

    @property
    def client(self) -> VectorCollection[dict[str, Any]]:
        return self._collection

    def add(self, nodes: Sequence[BaseNode], **_: Any) -> list[str]:
        entries = []
        ids = []
        for node in nodes:
            embedding = node.get_embedding()
            identifier = node.node_id
            ids.append(identifier)
            entries.append(
                (identifier, {"text": node.get_content(), "metadata": node.metadata}, embedding)
            )
        self._collection.upsert_many(entries)
        return ids

    async def async_add(self, nodes: Sequence[BaseNode], **kwargs: Any) -> list[str]:
        return self.add(nodes, **kwargs)

    def delete(self, ref_doc_id: str, **_: Any) -> None:
        self._collection.delete(ref_doc_id)

    def delete_nodes(self, node_ids: list[str] | None = None, **_: Any) -> None:
        for node_id in node_ids or []:
            self._collection.delete(node_id)

    def clear(self) -> None:
        self._collection.clear()

    def query(self, query: VectorStoreQuery, **_: Any) -> VectorStoreQueryResult:
        if query.query_embedding is None:
            raise ValueError("JigenVectorStore requires query_embedding")
        results = self._collection.search(
            query.query_embedding,
            top=query.similarity_top_k,
            filter=_filters(query.filters),
        )
        nodes = [
            TextNode(
                id_=result.key.decode("utf-8", errors="replace"),
                text=(result.content or {}).get("text", ""),
                metadata=(result.content or {}).get("metadata", {}),
            )
            for result in results
        ]
        return VectorStoreQueryResult(
            nodes=nodes,
            similarities=[result.score for result in results],
            ids=[node.node_id for node in nodes],
        )

    async def aquery(self, query: VectorStoreQuery, **kwargs: Any) -> VectorStoreQueryResult:
        return self.query(query, **kwargs)
