import struct
import uuid

import pytest

from jigen._proto import Jigen_pb2 as pb
from jigen.collection import AsyncVectorCollection, VectorCollection
from jigen.filters import contains, equals
from jigen.models import SearchOptions, encode_key
from jigen.serialization import MessagePackSerializer


def _response():
    serializer = MessagePackSerializer()
    return pb.SearchVectorResponse(
        Results=[
            pb.SearchVectorResult(
                Key=b"one",
                Content=serializer.serialize({"title": "One"}),
                Score=0.9,
                DenseScore=0.8,
                SparseScore=1.5,
            )
        ]
    )


class FakeStub:
    def __init__(self):
        self.request = None
        self.embedding = None
        self.response = None
        self.info = pb.CollectionInfoResponse()

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

    def GetEmbeddings(self, request):
        return self.embedding or pb.StoredEmbeddingsResponse()

    def SearchVector(self, request):
        self.request = request
        return self.response or _response()

    def SearchDocument(self, request):
        self.request = request
        return self.response or _response()

    def GetCollectionInfo(self, request):
        return self.info


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
    assert list(stub.request.Vectors[0].Embeddings) == pytest.approx([0.1, 0.2])

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


def test_collection_upsert_maps_multiple_vectors():
    stub = FakeStub()
    collection = VectorCollection(stub, "demo", "articles")
    collection.upsert("one", {"title": "One"}, embedding=[[0.1, 0.2], [0.3, 0.4]])

    assert [list(vector.Embeddings) for vector in stub.request.Vectors] == [
        [pytest.approx(0.1), pytest.approx(0.2)],
        [pytest.approx(0.3), pytest.approx(0.4)],
    ]


def test_single_vector_is_stored_as_one_vector_list():
    stub = FakeStub()
    collection = VectorCollection(stub, "demo", "articles")
    collection.upsert("one", {"title": "One"}, embedding=[0.1, 0.2])

    assert len(stub.request.Vectors) == 1
    assert list(stub.request.Vectors[0].Embeddings) == pytest.approx([0.1, 0.2])


def test_mixed_embedding_shape_is_rejected():
    stub = FakeStub()
    collection = VectorCollection(stub, "demo", "articles")
    with pytest.raises(TypeError):
        collection.upsert("one", {"title": "One"}, embedding=[0.1, [0.2, 0.3]])


def test_get_embeddings_reads_every_vector():
    stub = FakeStub()
    stub.embedding = pb.StoredEmbeddingsResponse(
        Vectors=[
            pb.EmbeddingVector(Embeddings=[0.1, 0.2]),
            pb.EmbeddingVector(Embeddings=[0.3, 0.4]),
        ]
    )
    collection = VectorCollection(stub, "demo", "articles")

    assert collection.get_embeddings("one") == [
        pytest.approx([0.1, 0.2]),
        pytest.approx([0.3, 0.4]),
    ]


def test_bulk_upsert_accepts_multi_vector_entries():
    stub = FakeStub()
    collection = VectorCollection(stub, "demo", "articles")
    accepted = collection.upsert_many(
        [
            ("one", {"text": "one"}, [[0.1], [0.2]]),
            ("two", {"text": "two"}, [0.3]),
        ]
    )

    assert accepted == 2
    assert list(stub.request[0].Vectors[0].Embeddings) == pytest.approx([0.1])
    assert list(stub.request[1].Vectors[0].Embeddings) == pytest.approx([0.3])


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


def test_search_sparse_sends_only_the_sparse_query():
    stub = FakeStub()
    stub.response = pb.SearchVectorResponse(
        Results=[
            pb.SearchVectorResult(
                Key=b"one",
                Content=MessagePackSerializer().serialize({"title": "One"}),
                Score=1.5,
                SparseScore=1.5,
            )
        ]
    )
    collection = VectorCollection(stub, "demo", "articles")

    results = collection.search_sparse({7: 1.5, 11: 0.5}, top=3)

    assert list(stub.request.Embeddings) == []
    assert dict(stub.request.SparseQuery) == {7: pytest.approx(1.5), 11: pytest.approx(0.5)}
    assert stub.request.Top == 3
    assert results[0].dense_score is None
    assert results[0].sparse_score == pytest.approx(1.5)


def test_search_hybrid_sends_both_halves_and_score_cuts():
    stub = FakeStub()
    collection = VectorCollection(stub, "demo", "articles")

    results = collection.search_hybrid(
        [0.1, 0.2],
        {7: 1.0},
        top=5,
        options=SearchOptions(min_dense_score=0.3, min_sparse_score=0.1),
    )

    assert list(stub.request.Embeddings) == pytest.approx([0.1, 0.2])
    assert dict(stub.request.SparseQuery) == {7: pytest.approx(1.0)}
    assert stub.request.Tuning.MinDenseScore == pytest.approx(0.3)
    assert stub.request.Tuning.MinSparseScore == pytest.approx(0.1)
    assert results[0].score == pytest.approx(0.9)
    assert results[0].dense_score == pytest.approx(0.8)
    assert results[0].sparse_score == pytest.approx(1.5)


def test_search_hybrid_with_text_uses_the_document_endpoint():
    stub = FakeStub()
    collection = VectorCollection(stub, "demo", "articles")

    collection.search_hybrid("what is rag?", {7: 1.0})

    assert isinstance(stub.request, pb.SearchDocumentRequest)
    assert stub.request.Sentence == "what is rag?"
    assert dict(stub.request.SparseQuery) == {7: pytest.approx(1.0)}


def test_search_hybrid_requires_at_least_one_half():
    collection = VectorCollection(FakeStub(), "demo", "articles")

    with pytest.raises(ValueError):
        collection.search_hybrid()


def test_search_dense_is_the_default_entry_point():
    stub = FakeStub()
    collection = VectorCollection(stub, "demo", "articles")

    collection.search([0.1, 0.2])
    dense = collection.search_dense([0.1, 0.2])

    assert list(stub.request.Embeddings) == pytest.approx([0.1, 0.2])
    assert dict(stub.request.SparseQuery) == {}
    assert dense[0].key == b"one"


class AsyncFakeStub:
    def __init__(self):
        self.request = None

    async def SearchVector(self, request):
        self.request = request
        return _response()

    async def SearchDocument(self, request):
        self.request = request
        return _response()


async def test_async_search_hybrid_sends_both_halves():
    stub = AsyncFakeStub()
    collection = AsyncVectorCollection(stub, "demo", "articles")

    results = await collection.search_hybrid([0.1], {7: 1.0}, top=2)

    assert list(stub.request.Embeddings) == pytest.approx([0.1])
    assert dict(stub.request.SparseQuery) == {7: pytest.approx(1.0)}
    assert stub.request.Top == 2
    assert results[0].sparse_score == pytest.approx(1.5)


async def test_async_search_sparse_sends_only_the_sparse_query():
    stub = AsyncFakeStub()
    collection = AsyncVectorCollection(stub, "demo", "articles")

    await collection.search_sparse({7: 1.0})

    assert list(stub.request.Embeddings) == []
    assert dict(stub.request.SparseQuery) == {7: pytest.approx(1.0)}


def test_upsert_sends_the_sparse_terms_of_a_one_vector_entry():
    stub = FakeStub()
    collection = VectorCollection(stub, "demo", "articles")

    collection.upsert("one", {"title": "One"}, embedding=[0.1, 0.2], sparse={7: 1.5, 11: 0.5})

    assert len(stub.request.Sparse) == 1
    assert dict(stub.request.Sparse[0].Terms) == {7: pytest.approx(1.5), 11: pytest.approx(0.5)}


def test_upsert_pairs_one_sparse_list_per_vector():
    stub = FakeStub()
    collection = VectorCollection(stub, "demo", "articles")

    collection.upsert(
        "one",
        {"title": "One"},
        embedding=[[0.1], [0.2]],
        sparse=[{7: 1.0}, {9: 2.0}],
    )

    assert [dict(terms.Terms) for terms in stub.request.Sparse] == [
        {7: pytest.approx(1.0)},
        {9: pytest.approx(2.0)},
    ]


def test_single_sparse_mapping_on_a_multi_vector_entry_is_refused():
    collection = VectorCollection(FakeStub(), "demo", "articles")

    with pytest.raises(ValueError):
        collection.upsert("one", {"title": "One"}, embedding=[[0.1], [0.2]], sparse={7: 1.0})


def test_sparse_with_more_lists_than_vectors_is_refused():
    collection = VectorCollection(FakeStub(), "demo", "articles")

    with pytest.raises(ValueError):
        collection.upsert("one", {"title": "One"}, embedding=[0.1], sparse=[{7: 1.0}, {9: 2.0}])


def test_negative_sparse_term_is_refused():
    collection = VectorCollection(FakeStub(), "demo", "articles")

    with pytest.raises(ValueError):
        collection.upsert("one", {"title": "One"}, embedding=[0.1], sparse={-1: 1.0})


def test_text_upsert_sends_the_document_sparse_terms():
    stub = FakeStub()
    collection = VectorCollection(stub, "demo", "articles")

    collection.upsert("one", {"title": "One"}, text="a sentence", sparse={7: 1.0})

    assert stub.request.Sentence == "a sentence"
    assert dict(stub.request.Sparse.Terms) == {7: pytest.approx(1.0)}


def test_bulk_upsert_accepts_sparse_terms():
    stub = FakeStub()
    collection = VectorCollection(stub, "demo", "articles")

    accepted = collection.upsert_many(
        [
            ("one", {"text": "one"}, [0.1], {7: 1.0}),
            ("two", {"text": "two"}, [0.2]),
        ]
    )

    assert accepted == 2
    assert dict(stub.request[0].Sparse[0].Terms) == {7: pytest.approx(1.0)}
    assert len(stub.request[1].Sparse) == 0


def test_search_with_a_sentence_can_ask_the_server_for_the_sparse_half():
    stub = FakeStub()
    collection = VectorCollection(stub, "demo", "articles")

    collection.search("hybrid from text", top=4, sparse_from_sentence=True)

    assert isinstance(stub.request, pb.SearchDocumentRequest)
    assert stub.request.Sentence == "hybrid from text"
    assert stub.request.SparseFromSentence is True
    assert dict(stub.request.SparseQuery) == {}


def test_upsert_with_text_can_let_the_server_derive_the_sparse_terms():
    stub = FakeStub()
    collection = VectorCollection(stub, "demo", "articles")

    collection.upsert("one", {"title": "One"}, text="a sentence", sparse_from_sentence=True)

    assert isinstance(stub.request, pb.Document)
    assert stub.request.Sentence == "a sentence"
    assert stub.request.SparseFromSentence is True


def test_sparse_from_sentence_needs_text():
    collection = VectorCollection(FakeStub(), "demo", "articles")

    with pytest.raises(ValueError):
        collection.upsert("one", {"title": "One"}, embedding=[0.1], sparse_from_sentence=True)


async def test_async_search_can_ask_for_the_sparse_half_of_a_sentence():
    stub = AsyncFakeStub()
    collection = AsyncVectorCollection(stub, "demo", "articles")

    await collection.search("hybrid from text", sparse_from_sentence=True)

    assert isinstance(stub.request, pb.SearchDocumentRequest)
    assert stub.request.SparseFromSentence is True


def test_collection_info_reports_the_sparse_index():
    stub = FakeStub()
    stub.info = pb.CollectionInfoResponse(
        Name="articles",
        Vectors=10,
        Dimensions=128,
        ContentSize=1024,
        VectorSize=5120,
        Sparse=pb.SparseCollectionInfoResponse(
            Segments=2, Terms=40, Blocks=80, BlockSize=64, Tombstones=1, SizeBytes=9000
        ),
    )
    collection = VectorCollection(stub, "demo", "articles")

    info = collection.info()

    assert info.sparse is not None
    assert info.sparse.segments == 2
    assert info.sparse.terms == 40
    assert info.sparse.blocks == 80
    assert info.sparse.block_size == 64
    assert info.sparse.tombstones == 1
    assert info.sparse.size_bytes == 9000


def test_collection_info_without_sparse_index_reports_none():
    stub = FakeStub()
    stub.info = pb.CollectionInfoResponse(Name="articles", Vectors=1, Dimensions=8)
    collection = VectorCollection(stub, "demo", "articles")

    assert collection.info().sparse is None
