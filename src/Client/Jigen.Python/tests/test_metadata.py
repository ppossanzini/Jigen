import pytest

from jigen._proto import Jigen_pb2 as pb
from jigen.collection import AsyncVectorCollection, VectorCollection
from jigen.filters import equals
from jigen.models import MetadataSearchResult, SearchOptions
from jigen.serialization import MessagePackSerializer


class FakeMetadataStub:
    """Records the last request of every shared-metadata RPC."""

    def __init__(self):
        self.request = None

    def CreateMetadata(self, request):
        self.request = request
        return pb.CreateMetadataResponse(MetadataIndex=17)

    def UpdateMetadata(self, request):
        self.request = request
        return pb.UpdateMetadataResponse(Updated=request.MetadataIndex == 17)

    def GetEmbeddingMetadata(self, request):
        self.request = request
        return pb.EmbeddingMetadataResponse(MetadataIndex=17)

    def AssociateEmbedding(self, request):
        self.request = request
        return pb.Result(Success=True)

    def SearchByMetadata(self, request):
        self.request = request
        return pb.SearchByMetadataResponse(
            Results=[
                pb.SearchByMetadataResult(
                    MetadataIndex=17,
                    Key=b"one",
                    Content=MessagePackSerializer().serialize({"title": "One"}),
                    Score=0.9,
                )
            ]
        )


def test_create_and_update_metadata_send_serialized_content():
    stub = FakeMetadataStub()
    collection = VectorCollection(stub, "demo", "articles")

    assert collection.create_metadata({"title": "One"}) == 17
    assert stub.request.Database == "demo"
    assert MessagePackSerializer().deserialize(stub.request.Content) == {"title": "One"}

    assert collection.update_metadata(17, {"title": "Two"}) is True
    assert stub.request.MetadataIndex == 17

    assert collection.update_metadata(99, {"title": "Two"}) is False


def test_association_round_trip():
    stub = FakeMetadataStub()
    collection = VectorCollection(stub, "demo", "articles")

    assert collection.get_metadata_index("one") == 17
    assert stub.request.Collection == "articles"
    assert stub.request.Key == b"one"

    assert collection.associate_metadata("one", 17) is True
    assert stub.request.MetadataIndex == 17


def test_search_by_metadata_requires_budget_and_maps_groups():
    stub = FakeMetadataStub()
    collection = VectorCollection(stub, "demo", "articles")

    with pytest.raises(ValueError):
        collection.search_by_metadata([0.1], candidate_budget=0)

    results = collection.search_by_metadata(
        [0.1, 0.2],
        top=3,
        candidate_budget=40,
        filter=equals("title", "One"),
        options=SearchOptions(ef_search=64, no_content=True),
    )

    assert stub.request.Top == 3
    assert stub.request.CandidateBudget == 40
    assert stub.request.Tuning.EfSearch == 64
    assert stub.request.Tuning.NoContent is True
    assert stub.request.Filter.Equals.PropertyPath == "title"
    assert list(stub.request.Embeddings) == pytest.approx([0.1, 0.2])

    result = results[0]
    assert isinstance(result, MetadataSearchResult)
    assert result.metadata_index == 17
    assert result.content == {"title": "One"}


def test_search_by_metadata_accepts_a_sentence():
    stub = FakeMetadataStub()
    collection = VectorCollection(stub, "demo", "articles")

    collection.search_by_metadata("carbonara", candidate_budget=10)

    assert stub.request.Sentence == "carbonara"
    assert len(stub.request.Embeddings) == 0


def test_async_collection_exposes_the_same_metadata_surface():
    for method in (
        "create_metadata",
        "update_metadata",
        "get_metadata_index",
        "associate_metadata",
        "search_by_metadata",
    ):
        assert hasattr(AsyncVectorCollection, method)
