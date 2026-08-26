import struct
import uuid

import pytest

from jigen._proto import Jigen_pb2 as pb
from jigen.collection import VectorCollection
from jigen.filters import contains, equals
from jigen.models import SearchOptions, encode_key
from jigen.serialization import MessagePackSerializer


class FakeStub:
    def __init__(self):
        self.request = None

    def SetVector(self, request):
        self.request = request
        return pb.Result(Success=True)

    def SetDocument(self, request):
        self.request = request
        return pb.Result(Success=True)

    def SetVectors(self, requests):
        values = list(requests)
        self.request = values
        return pb.IngestResult(Success=True, Accepted=len(values))

    def SearchVector(self, request):
        self.request = request
        return pb.SearchVectorResponse(
            Results=[
                pb.SearchVectorResult(
                    Key=b"one",
                    Content=MessagePackSerializer().serialize({"title": "One"}),
                    Score=0.9,
                )
            ]
        )


def test_key_encoding_matches_dotnet_conventions():
    assert encode_key("hello") == b"hello"
    assert encode_key(42) == struct.pack("<q", 42)
    value = uuid.UUID("00112233-4455-6677-8899-aabbccddeeff")
    assert encode_key(value) == value.bytes_le


def test_messagepack_round_trip():
    serializer = MessagePackSerializer()
    document = {"text": "hello", "metadata": {"tags": ["rag", "dotnet"], "rank": 3}}
    assert serializer.deserialize(serializer.serialize(document)) == document


def test_filter_ast_composition():
    value = equals("metadata.kind", "guide") & contains("metadata.tags", "rag")
    assert value.node.HasField("And")
    assert value.node.And.Left.Equals.PropertyPath == "metadata.kind"
    assert value.node.And.Right.CollectionAny.PropertyPath == "metadata.tags"


def test_collection_upsert_and_search_request_mapping():
    stub = FakeStub()
    collection = VectorCollection(stub, "demo", "articles")
    collection.upsert("one", {"title": "One"}, embedding=[0.1, 0.2])
    assert stub.request.Database == "demo"
    assert stub.request.Collection == "articles"
    assert list(stub.request.Embeddings) == pytest.approx([0.1, 0.2])

    results = collection.search(
        [0.1, 0.2],
        top=4,
        filter=equals("title", "One"),
        options=SearchOptions(ef_search=128, min_score=0.4),
    )
    assert stub.request.Top == 4
    assert stub.request.Tuning.EfSearch == 128
    assert stub.request.Tuning.MinScore == pytest.approx(0.4)
    assert results[0].content == {"title": "One"}
    assert results[0].score == pytest.approx(0.9)


def test_bulk_upsert_uses_one_streaming_call():
    stub = FakeStub()
    collection = VectorCollection(stub, "demo", "articles")
    accepted = collection.upsert_many(
        [
            ("one", {"text": "one"}, [0.1]),
            ("two", {"text": "two"}, [0.2]),
        ]
    )
    assert accepted == 2
    assert [item.Key for item in stub.request] == [b"one", b"two"]
