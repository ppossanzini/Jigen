from __future__ import annotations

import json
from dataclasses import asdict, is_dataclass
from typing import Any, Protocol

import msgpack


class DocumentSerializer(Protocol):
    def serialize(self, document: Any) -> bytes: ...
    def deserialize(self, payload: bytes) -> Any: ...


class MessagePackSerializer:
    """Contractless MessagePack serializer compatible with Jigen.Client."""

    def serialize(self, document: Any) -> bytes:
        if is_dataclass(document):
            document = asdict(document)
        elif hasattr(document, "model_dump"):
            document = document.model_dump()
        return msgpack.packb(document, use_bin_type=True)

    def deserialize(self, payload: bytes) -> Any:
        return msgpack.unpackb(payload, raw=False, strict_map_key=False)


class JsonSerializer:
    def serialize(self, document: Any) -> bytes:
        if is_dataclass(document):
            document = asdict(document)
        elif hasattr(document, "model_dump"):
            document = document.model_dump()
        return json.dumps(document, separators=(",", ":"), ensure_ascii=False).encode()

    def deserialize(self, payload: bytes) -> Any:
        return json.loads(payload)
