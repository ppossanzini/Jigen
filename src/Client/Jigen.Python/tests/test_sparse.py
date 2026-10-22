"""Sparse slot from the Python client: one write with both vectors, read-back,
exact lexical search and fusion. The fake stub records the request, so the
assertions are on what leaves the client."""

import pytest

from jigen._proto import Jigen_pb2 as pb
from jigen.collection import AsyncVectorCollection, VectorCollection
from jigen.models import SearchOptions, SparseVector
from jigen.serialization import MessagePackSerializer


def vector(terms=(1, 5), weights=(0.5, 2.0)):
    return SparseVector(terms=terms, weights=weights)


class FakeStub:
    def __init__(self):
        self.request = None
        self.method = None

    def _result(self, method, request, response):
        self.method = method
        self.request = request
        return response

    def SetVector(self, request):
        return self._result("SetVector", request, pb.Result(Success=True))

    def SetDocument(self, request):
        return self._result("SetDocument", request, pb.Result(Success=True))

    def GetSparse(self, request):
        message = pb.SparseVector(Terms=[2, 9], Weights=[0.25, 3.0])
        return self._result("GetSparse", request, message)

    def SearchSparse(self, request):
        response = pb.SearchVectorResponse(
            Results=[
                pb.SearchVectorResult(
                    Key=b"one",
                    Content=MessagePackSerializer().serialize({"title": "One"}),
                    Score=4.0,
                    InSparse=True,
                    SparseScore=4.0,
                )
            ]
        )
        return self._result("SearchSparse", request, response)

    def SearchHybrid(self, request):
        response = pb.SearchVectorResponse(
            Results=[
                pb.SearchVectorResult(
                    Key=b"one",
                    Content=MessagePackSerializer().serialize({"title": "One"}),
                    Score=0.033,
                    InDense=True,
                    InSparse=True,
                    DenseScore=0.9,
                    SparseScore=3.0,
                )
            ]
        )
        return self._result("SearchHybrid", request, response)

    def GetCollectionInfo(self, request):
        info = pb.CollectionInfoResponse(
            Name="articles",
            Vectors=10,
            Dimensions=4,
            Sparse=pb.CollectionSparseInfoResponse(
                Terms=120,
                Postings=480,
                AveragePostingsPerTerm=4.0,
                MemoryBytes=8192,
                RetiredTerms=3,
                Built=True,
            ),
        )
        return self._result("GetCollectionInfo", request, info)


# ── Validation ──


def test_sparse_vector_rejects_unordered_terms():
    with pytest.raises(ValueError, match="strictly increasing"):
        SparseVector(terms=(5, 1), weights=(1.0, 1.0))


def test_sparse_vector_rejects_a_negative_weight():
    with pytest.raises(ValueError, match="negative"):
        SparseVector(terms=(4,), weights=(-1.0,))


def test_sparse_vector_rejects_all_weights_zero():
    with pytest.raises(ValueError, match="greater than zero"):
        SparseVector(terms=(1, 2), weights=(0.0, 0.0))


def test_sparse_vector_rejects_mismatched_lengths():
    with pytest.raises(ValueError, match="one weight per term"):
        SparseVector(terms=(1, 2), weights=(1.0,))


def test_sparse_vector_from_pairs_sorts_by_term():
    value = SparseVector.from_pairs([(98, 1.2), (12, 0.8)])
    assert value.terms == (12, 98)
    assert value.weights == (0.8, 1.2)


# ── Write ──


def test_upsert_sends_dense_and_sparse_in_one_call():
    stub = FakeStub()
    VectorCollection(stub, "demo", "articles").upsert(
        "one", {"title": "One"}, embedding=[0.1, 0.2], sparse=vector((12, 98), (0.8, 1.2))
    )

    assert stub.method == "SetVector"
    assert list(stub.request.Embeddings) == pytest.approx([0.1, 0.2])
    assert list(stub.request.Sparse.Terms) == [12, 98]
    assert list(stub.request.Sparse.Weights) == pytest.approx([0.8, 1.2])


def test_upsert_with_only_sparse_sends_no_embeddings():
    stub = FakeStub()
    VectorCollection(stub, "demo", "articles").upsert(
        "two", {"title": "Lexical"}, sparse=vector((7,), (1.0,))
    )

    assert stub.method == "SetVector"
    assert list(stub.request.Embeddings) == []
    assert list(stub.request.Sparse.Terms) == [7]


def test_upsert_with_text_and_sparse_keeps_both():
    stub = FakeStub()
    VectorCollection(stub, "demo", "articles").upsert(
        "three", {"title": "Doc"}, text="a sentence", sparse=vector()
    )

    assert stub.method == "SetDocument"
    assert stub.request.Sentence == "a sentence"
    assert list(stub.request.Sparse.Terms) == [1, 5]


def test_upsert_without_any_vector_is_rejected():
    stub = FakeStub()
    with pytest.raises(ValueError, match="sparse"):
        VectorCollection(stub, "demo", "articles").upsert("four", {"title": "x"})
    assert stub.method is None


# ── Read ──


def test_get_sparse_returns_the_stored_pairs():
    stub = FakeStub()
    value = VectorCollection(stub, "demo", "articles").get_sparse("one")

    assert stub.method == "GetSparse"
    assert value.terms == (2, 9)
    assert value.weights == (0.25, 3.0)


# ── Search ──


def test_search_sparse_uses_the_lexical_rpc_and_reports_provenance():
    stub = FakeStub()
    results = VectorCollection(stub, "demo", "articles").search_sparse(
        vector((1, 5), (1.0, 2.0)), top=5
    )

    assert stub.method == "SearchSparse"
    assert list(stub.request.Sparse.Terms) == [1, 5]
    assert stub.request.Top == 5
    assert results[0].in_sparse is True
    assert results[0].in_dense is False
    assert results[0].score == pytest.approx(4.0)


def test_search_hybrid_declares_both_weights():
    stub = FakeStub()
    results = VectorCollection(stub, "demo", "articles").search_hybrid(
        [0.4], vector((1,), (1.0,)), top=3, dense_weight=2.0, sparse_weight=1.0
    )

    assert stub.method == "SearchHybrid"
    assert list(stub.request.Embeddings) == pytest.approx([0.4])
    assert stub.request.HasField("DenseWeight")
    assert stub.request.HasField("SparseWeight")
    assert results[0].in_dense is True
    assert results[0].in_sparse is True
    assert results[0].dense_score == pytest.approx(0.9)
    assert results[0].sparse_score == pytest.approx(3.0)


def test_search_hybrid_from_a_sentence_sends_no_vectors():
    stub = FakeStub()
    VectorCollection(stub, "demo", "articles").search_hybrid(sentence="il parco è buio", top=5)

    assert stub.method == "SearchHybrid"
    assert stub.request.Sentence == "il parco è buio"
    assert list(stub.request.Embeddings) == []
    assert not stub.request.HasField("Sparse")
    # A sentence generates the second vector: the defaults weigh both equally.
    assert stub.request.HasField("DenseWeight")
    assert stub.request.HasField("SparseWeight")


def test_search_hybrid_from_a_sentence_keeps_an_explicit_sparse():
    stub = FakeStub()
    VectorCollection(stub, "demo", "articles").search_hybrid(
        sparse=vector(), sentence="il parco è buio"
    )

    assert stub.request.Sentence == "il parco è buio"
    assert list(stub.request.Sparse.Terms) == [1, 5]


def test_search_hybrid_with_a_blank_sentence_is_rejected_before_the_call():
    stub = FakeStub()
    with pytest.raises(ValueError, match="at least one"):
        VectorCollection(stub, "demo", "articles").search_hybrid(sentence="   ")
    assert stub.method is None


def test_search_hybrid_without_any_vector_is_rejected_before_the_call():
    stub = FakeStub()
    with pytest.raises(ValueError, match="at least one"):
        VectorCollection(stub, "demo", "articles").search_hybrid()
    assert stub.method is None


def test_search_hybrid_with_only_the_sparse_vector_declares_one_weight():
    stub = FakeStub()
    VectorCollection(stub, "demo", "articles").search_hybrid(sparse=vector())

    assert stub.method == "SearchHybrid"
    assert list(stub.request.Embeddings) == []
    assert stub.request.HasField("SparseWeight")
    assert not stub.request.HasField("DenseWeight")


# ── Observation ──


def test_info_reports_the_term_index():
    stub = FakeStub()
    info = VectorCollection(stub, "demo", "articles").info()

    assert info.sparse is not None
    assert info.sparse.terms == 120
    assert info.sparse.postings == 480
    assert info.sparse.memory_bytes == 8192
    assert info.sparse.retired_terms == 3
    assert info.sparse.built is True


# ── Async surface ──


class FakeAsyncStub:
    def __init__(self):
        self.method = None
        self.request = None

    async def SetVector(self, request):
        self.method = "SetVector"
        self.request = request
        return pb.Result(Success=True)

    async def SearchHybrid(self, request):
        self.method = "SearchHybrid"
        self.request = request
        return pb.SearchVectorResponse(
            Results=[
                pb.SearchVectorResult(
                    Key=b"one",
                    Content=MessagePackSerializer().serialize({"title": "One"}),
                    Score=0.03,
                    InDense=True,
                    InSparse=True,
                )
            ]
        )


async def test_async_upsert_and_hybrid_search_carry_the_sparse_slot():
    stub = FakeAsyncStub()
    collection = AsyncVectorCollection(stub, "demo", "articles")

    await collection.upsert("one", {"title": "One"}, embedding=[0.1], sparse=vector())
    assert list(stub.request.Sparse.Terms) == [1, 5]

    results = await collection.search_hybrid([0.1], vector(), top=3)
    assert stub.method == "SearchHybrid"
    assert results[0].in_dense is True
    assert results[0].in_sparse is True


def test_search_options_still_apply_to_sparse_queries():
    stub = FakeStub()
    VectorCollection(stub, "demo", "articles").search_sparse(
        vector(), options=SearchOptions(no_content=True, min_score=0.5)
    )

    assert stub.request.Tuning.NoContent is True
    assert stub.request.Tuning.MinScore == pytest.approx(0.5)
