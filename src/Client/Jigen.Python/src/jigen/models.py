from __future__ import annotations

import struct
import uuid
from dataclasses import dataclass
from typing import Any, Generic, TypeVar

DocumentT = TypeVar("DocumentT")
Key = bytes | bytearray | memoryview | str | int | uuid.UUID


def encode_key(value: Key) -> bytes:
    """Encode keys compatibly with Jigen.Client's VectorKey conversions.

    Python integers use the signed 64-bit representation. Use raw ``bytes``
    when interoperating with a .NET ``int``/``uint`` key that uses four bytes.
    """
    if isinstance(value, bytes):
        return value
    if isinstance(value, (bytearray, memoryview)):
        return bytes(value)
    if isinstance(value, str):
        return value.encode("utf-8")
    if isinstance(value, int):
        return struct.pack("<q", value)
    if isinstance(value, uuid.UUID):
        return value.bytes_le
    raise TypeError(f"Unsupported Jigen key type: {type(value)!r}")


@dataclass(slots=True, frozen=True)
class SearchOptions:
    ef_search: int = 0
    no_content: bool = False
    min_score: float | None = None


@dataclass(slots=True)
class VectorEntry(Generic[DocumentT]):
    key: bytes
    content: DocumentT
    embedding: list[float] | None = None


@dataclass(slots=True)
class SearchResult(Generic[DocumentT]):
    key: bytes
    content: DocumentT | None
    score: float


@dataclass(slots=True, frozen=True)
class CollectionIndexInfo:
    size_bytes: int
    nodes: int
    deleted_nodes: int
    max_level: int
    nodes_per_level: tuple[int, ...]
    average_degree: float
    quantization: str


@dataclass(slots=True, frozen=True)
class CollectionInfo:
    name: str
    vectors: int
    dimensions: int
    content_size: int
    vector_size: int
    index: CollectionIndexInfo | None = None


LlmDocument = dict[str, Any]
