from __future__ import annotations

import math
import struct
import uuid
from collections.abc import Iterable, Sequence
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


def _validate_terms(terms: Sequence[int], weights: Sequence[float]) -> None:
    """Repeats the server's rules so an invalid vector never reaches the network.

    The server validates again, and its configured term cap is the one that
    decides: this checks the shape only.
    """
    if len(terms) != len(weights):
        raise ValueError(
            f"a sparse vector needs one weight per term; received {len(terms)} terms "
            f"and {len(weights)} weights"
        )
    if not terms:
        raise ValueError("a sparse vector needs at least one term")

    any_positive = False
    for position, (term, weight) in enumerate(zip(terms, weights)):
        if term < 0:
            raise ValueError(f"term {term} is negative; terms are non-negative integers")
        if position and term <= terms[position - 1]:
            raise ValueError(
                f"term {term} at position {position} is not greater than the previous "
                f"term {terms[position - 1]}; terms must be unique and strictly increasing"
            )
        if not math.isfinite(weight):
            raise ValueError(f"weight of term {term} is not a finite number")
        if weight < 0:
            raise ValueError(f"weight of term {term} is negative; weights are non-negative")
        if weight > 0:
            any_positive = True

    if not any_positive:
        raise ValueError("a sparse vector needs at least one weight greater than zero")


@dataclass(slots=True)
class SparseVector:
    """A sparse lexical vector: caller-defined terms and weights.

    Terms are never derived from the text by the server, so the caller supplies
    both. An absent value (``None``) means "no lexical slot"; an empty one is
    rejected, which keeps "absent" and "empty" distinguishable.
    """

    terms: tuple[int, ...]
    weights: tuple[float, ...]

    def __post_init__(self) -> None:
        self.terms = tuple(self.terms)
        self.weights = tuple(self.weights)
        _validate_terms(self.terms, self.weights)

    @classmethod
    def from_pairs(cls, pairs: Iterable[tuple[int, float]]) -> SparseVector:
        """Builds a vector from (term, weight) pairs, sorted by term.

        Duplicate terms are rejected: summing them silently would store a vector
        different from the one the caller described.
        """
        ordered = sorted(pairs)
        return cls(
            terms=tuple(term for term, _ in ordered),
            weights=tuple(weight for _, weight in ordered),
        )


@dataclass(slots=True)
class VectorEntry(Generic[DocumentT]):
    key: bytes
    content: DocumentT
    embedding: list[float] | None = None
    sparse: SparseVector | None = None


@dataclass(slots=True)
class SearchResult(Generic[DocumentT]):
    key: bytes
    content: DocumentT | None
    #: The ranking score: the modality score on a single-modality search, the
    #: fused score on a hybrid one.
    score: float
    in_dense: bool = False
    in_sparse: bool = False
    dense_score: float = 0.0
    sparse_score: float = 0.0


@dataclass(slots=True)
class MetadataSearchResult(Generic[DocumentT]):
    """One result per shared metadata record.

    ``metadata_index`` is the opaque, stable metadata identity; ``key`` and
    ``score`` describe the winning embedding of the group.
    """

    metadata_index: int
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
class CollectionSparseInfo:
    """Observation of the term index: distinct terms, list lengths, memory."""

    terms: int
    postings: int
    average_postings_per_term: float
    memory_bytes: int
    retired_terms: int
    built: bool


@dataclass(slots=True, frozen=True)
class CollectionInfo:
    name: str
    vectors: int
    dimensions: int
    content_size: int
    vector_size: int
    index: CollectionIndexInfo | None = None
    sparse: CollectionSparseInfo | None = None


LlmDocument = dict[str, Any]
