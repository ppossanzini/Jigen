from __future__ import annotations

from dataclasses import dataclass
from typing import Any, TypeVar

import grpc

from ._proto import Jigen_pb2 as pb
from ._proto import Jigen_pb2_grpc as pb_grpc
from .collection import AsyncVectorCollection, VectorCollection, _map_info
from .models import CollectionInfo
from .serialization import DocumentSerializer

T = TypeVar("T")


@dataclass(slots=True, frozen=True)
class ConnectionOptions:
    host: str = "localhost"
    port: int = 3223
    database: str = ""
    tls: bool = False
    root_certificates: bytes | None = None
    server_name_override: str | None = None
    max_message_length: int = 64 * 1024 * 1024

    @property
    def target(self) -> str:
        return f"{self.host}:{self.port}"

    def channel_options(self) -> list[tuple[str, Any]]:
        values: list[tuple[str, Any]] = [
            ("grpc.max_send_message_length", self.max_message_length),
            ("grpc.max_receive_message_length", self.max_message_length),
        ]
        if self.server_name_override:
            values.append(("grpc.ssl_target_name_override", self.server_name_override))
        return values


class Context:
    def __init__(self, options: ConnectionOptions, *, channel: grpc.Channel | None = None) -> None:
        if not options.database.strip():
            raise ValueError("ConnectionOptions.database is required")
        self.options = options
        self._owns_channel = channel is None
        if channel is None:
            if options.tls:
                credentials = grpc.ssl_channel_credentials(options.root_certificates)
                channel = grpc.secure_channel(
                    options.target, credentials, options.channel_options()
                )
            else:
                channel = grpc.insecure_channel(options.target, options=options.channel_options())
        self.channel = channel
        self.stub = pb_grpc.StoreCollectionServiceStub(channel)

    def collection(
        self, name: str, *, serializer: DocumentSerializer | None = None
    ) -> VectorCollection[Any]:
        return VectorCollection(self.stub, self.options.database, name, serializer)

    def list_collections(self) -> list[str]:
        request = pb.CollectionKey(Database=self.options.database)
        return list(self.stub.ListCollections(request).collections)

    def collections_info(self) -> list[CollectionInfo]:
        request = pb.CollectionKey(Database=self.options.database)
        return [_map_info(item) for item in self.stub.GetCollectionsInfo(request).Collections]

    def embed(self, text: str, *, task: str = "") -> list[float]:
        return list(
            self.stub.CalculateEmbeddings(pb.EmbeddingRequest(Message=text, Task=task)).Embeddings
        )

    def embed_many(self, texts: list[str], *, task: str = "") -> list[list[float]]:
        response = self.stub.CalculateEmbeddingsBatch(
            pb.EmbeddingBatchRequest(Messages=texts, Task=task)
        )
        return [list(item.Embeddings) for item in response.Results]

    def embed_image(self, image: bytes) -> list[float]:
        return list(
            self.stub.CalculateImageEmbedding(pb.ImageEmbeddingRequest(Image=image)).Embeddings
        )

    def close(self) -> None:
        if self._owns_channel:
            self.channel.close()

    def __enter__(self) -> Context:
        return self

    def __exit__(self, *_: object) -> None:
        self.close()


class AsyncContext:
    def __init__(
        self, options: ConnectionOptions, *, channel: grpc.aio.Channel | None = None
    ) -> None:
        if not options.database.strip():
            raise ValueError("ConnectionOptions.database is required")
        self.options = options
        self._owns_channel = channel is None
        if channel is None:
            if options.tls:
                credentials = grpc.ssl_channel_credentials(options.root_certificates)
                channel = grpc.aio.secure_channel(
                    options.target, credentials, options.channel_options()
                )
            else:
                channel = grpc.aio.insecure_channel(
                    options.target, options=options.channel_options()
                )
        self.channel = channel
        self.stub = pb_grpc.StoreCollectionServiceStub(channel)

    def collection(
        self, name: str, *, serializer: DocumentSerializer | None = None
    ) -> AsyncVectorCollection[Any]:
        return AsyncVectorCollection(self.stub, self.options.database, name, serializer)

    async def list_collections(self) -> list[str]:
        response = await self.stub.ListCollections(pb.CollectionKey(Database=self.options.database))
        return list(response.collections)

    async def embed(self, text: str, *, task: str = "") -> list[float]:
        response = await self.stub.CalculateEmbeddings(pb.EmbeddingRequest(Message=text, Task=task))
        return list(response.Embeddings)

    async def embed_many(self, texts: list[str], *, task: str = "") -> list[list[float]]:
        response = await self.stub.CalculateEmbeddingsBatch(
            pb.EmbeddingBatchRequest(Messages=texts, Task=task)
        )
        return [list(item.Embeddings) for item in response.Results]

    async def close(self) -> None:
        if self._owns_channel:
            await self.channel.close()

    async def __aenter__(self) -> AsyncContext:
        return self

    async def __aexit__(self, *_: object) -> None:
        await self.close()
