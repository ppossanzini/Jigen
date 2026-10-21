using Hikyaku;
using Jigen.API;
using Jigen.API.Dto;
using Jigen.Core.Command.collections;
using Jigen.Core.Query.collections;
using Jigen.DataStructures;
using Microsoft.AspNetCore.Mvc;

namespace Jigen.API.Tests;

/// <summary>
/// REST surface of the sparse slot. The cases mirror ServiceSparseTests: the
/// same three operations and the same refusals, so the two surfaces cannot
/// drift apart without a red test.
/// </summary>
public class CollectionsControllerSparseTests
{
  private readonly Mock<IHikyaku> _mediatorMock = new();
  private readonly Mock<IDocumentSerializer> _serializerMock = new();
  private const string DbName = "testdb";
  private const string Collection = "testcoll";

  private CollectionsController CreateController() =>
    new(_mediatorMock.Object, _serializerMock.Object);

  private static SparsePayload Payload(int[] terms, float[] weights) =>
    new() { Terms = terms, Weights = weights };

  // ── Write ──

  [Fact]
  public async Task SetVector_CarriesSparseSlotToTheCommand()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<SetVector>(), It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var payload = new VectorPayload
    {
      Embeddings = [0.1f, 0.2f],
      Sparse = Payload([3, 7], [0.5f, 1.5f])
    };

    var result = await CreateController().SetVector(DbName, Collection, "k", payload, "string");

    Assert.IsType<OkResult>(result);
    _mediatorMock.Verify(m => m.Send(
      It.Is<SetVector>(c => c.Sparse.Terms.ToArray().SequenceEqual(new[] { 3, 7 })
                           && c.Sparse.Weights.ToArray().SequenceEqual(new[] { 0.5f, 1.5f })),
      It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task SetVector_WithoutSparsePayload_LeavesTheSlotEmpty()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<SetVector>(), It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var result = await CreateController().SetVector(DbName, Collection, "k",
      new VectorPayload { Embeddings = [0.1f] }, "string");

    Assert.IsType<OkResult>(result);
    _mediatorMock.Verify(m => m.Send(
      It.Is<SetVector>(c => c.Sparse == null), It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task SetVector_WithOnlyTheSparseVector_IsAccepted()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<SetVector>(), It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var result = await CreateController().SetVector(DbName, Collection, "k", new VectorPayload
    {
      Sparse = Payload([5], [2f])
    }, "string");

    Assert.IsType<OkResult>(result);
    _mediatorMock.Verify(m => m.Send(
      It.Is<SetVector>(c => c.Embeddings == null && c.Sparse != null),
      It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task SetVector_WithoutAnyVector_ReturnsBadRequest()
  {
    var result = await CreateController().SetVector(DbName, Collection, "k", new VectorPayload(), "string");

    var badRequest = Assert.IsType<BadRequestObjectResult>(result);
    Assert.Contains("sparse", badRequest.Value as string);
  }

  [Fact]
  public async Task SetVector_WithUnorderedTerms_ReturnsBadRequestNamingTheTerm()
  {
    var payload = new VectorPayload
    {
      Embeddings = [0.1f],
      Sparse = Payload([7, 3], [1f, 1f])
    };

    var result = await CreateController().SetVector(DbName, Collection, "k", payload, "string");

    var badRequest = Assert.IsType<BadRequestObjectResult>(result);
    Assert.Contains("Term 3", badRequest.Value as string);
    _mediatorMock.Verify(m => m.Send(It.IsAny<SetVector>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task SetVector_WithNegativeWeight_ReturnsBadRequestNamingTheTerm()
  {
    var payload = new VectorPayload
    {
      Embeddings = [0.1f],
      Sparse = Payload([4], [-1f])
    };

    var result = await CreateController().SetVector(DbName, Collection, "k", payload, "string");

    var badRequest = Assert.IsType<BadRequestObjectResult>(result);
    Assert.Contains("term 4", badRequest.Value as string);
  }

  [Fact]
  public async Task SetVector_WithAllWeightsZero_ReturnsBadRequest()
  {
    var payload = new VectorPayload
    {
      Embeddings = [0.1f],
      Sparse = Payload([1, 2], [0f, 0f])
    };

    var result = await CreateController().SetVector(DbName, Collection, "k", payload, "string");

    Assert.IsType<BadRequestObjectResult>(result);
  }

  [Fact]
  public async Task SetVector_WhenTheStoreRejectsTheTermCount_MapsToBadRequest()
  {
    // The cap belongs to the store, which knows its configuration: the surface
    // must relay its refusal instead of inventing a limit of its own.
    _mediatorMock.Setup(m => m.Send(It.IsAny<SetVector>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new ArgumentException("A sparse vector carries at most 4 terms; received 9."));

    var payload = new VectorPayload
    {
      Embeddings = [0.1f],
      Sparse = Payload([1, 2, 3, 4, 5, 6, 7, 8, 9], [1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f])
    };

    var result = await CreateController().SetVector(DbName, Collection, "k", payload, "string");

    var badRequest = Assert.IsType<BadRequestObjectResult>(result);
    Assert.Contains("at most 4 terms", badRequest.Value as string);
  }

  [Fact]
  public async Task SetVector_WithoutTheWal_ReturnsBadRequest()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<SetVector>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("Writing a sparse vector requires the write-ahead log"));

    var payload = new VectorPayload
    {
      Embeddings = [0.1f],
      Sparse = Payload([1], [1f])
    };

    var result = await CreateController().SetVector(DbName, Collection, "k", payload, "string");

    Assert.IsType<BadRequestObjectResult>(result);
  }

  [Fact]
  public async Task SetVectorsBulk_ReportsTheOffendingItem()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<SetVector>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new ArgumentException("Term 2 at position 1 is not greater than the previous term 5"));

    var items = new List<BulkVectorItem>
    {
      new() { Key = "bad", KeyType = "string", Embeddings = [1f], Sparse = Payload([5, 2], [1f, 1f]) }
    };

    var result = await CreateController().SetVectorsBulk(DbName, Collection, items);

    var badRequest = Assert.IsType<BadRequestObjectResult>(result);
    Assert.Contains("bad", badRequest.Value as string);
  }

  // ── Read ──

  [Fact]
  public async Task GetSparse_ReturnsTheStoredPairs()
  {
    _mediatorMock.Setup(m => m.Send(It.Is<GetSparse>(q => q.Database == DbName), It.IsAny<CancellationToken>()))
      .ReturnsAsync(SparseVector.Create([2, 9], [0.25f, 3f]));

    var result = await CreateController().GetSparse(DbName, Collection, "k", "string");

    var okResult = Assert.IsType<OkObjectResult>(result);
    var payload = Assert.IsType<SparsePayload>(okResult.Value);
    Assert.Equal(new[] { 2, 9 }, payload.Terms);
    Assert.Equal(new[] { 0.25f, 3f }, payload.Weights);
  }

  [Fact]
  public async Task GetSparse_ReturnsEmptyPairs_WhenTheEntryHasNoSlot()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<GetSparse>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((SparseVector)null);

    var result = await CreateController().GetSparse(DbName, Collection, "k", "string");

    var okResult = Assert.IsType<OkObjectResult>(result);
    var payload = Assert.IsType<SparsePayload>(okResult.Value);
    Assert.Empty(payload.Terms);
    Assert.Empty(payload.Weights);
  }

  // ── Search ──

  [Fact]
  public async Task SearchSparse_ReturnsRankedKeys()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<SearchSparse>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new[]
      {
        new SearchVectorResultItem { Key = "a"u8.ToArray(), Score = 2f, InSparse = true, SparseScore = 2f }
      });

    var result = await CreateController().SearchSparse(DbName, Collection,
      new SparseSearchData { Sparse = Payload([1], [1f]), Top = 5 }, CancellationToken.None);

    var okResult = Assert.IsType<OkObjectResult>(result);
    var items = Assert.IsAssignableFrom<IEnumerable<SearchVectorResultItem>>(okResult.Value).ToArray();
    Assert.Single(items);
    Assert.True(items[0].InSparse);
    Assert.False(items[0].InDense);
  }

  [Fact]
  public async Task SearchSparse_WithoutTerms_ReturnsBadRequest()
  {
    var result = await CreateController().SearchSparse(DbName, Collection,
      new SparseSearchData { Top = 5 }, CancellationToken.None);

    var badRequest = Assert.IsType<BadRequestObjectResult>(result);
    Assert.Contains("terms", badRequest.Value as string);
  }

  [Fact]
  public async Task SearchSparse_WhenTheQueryIsRefused_MapsToBadRequest()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<SearchSparse>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new ArgumentException("A lexical search needs at least one term."));

    var result = await CreateController().SearchSparse(DbName, Collection,
      new SparseSearchData { Sparse = Payload([1], [1f]) }, CancellationToken.None);

    Assert.IsType<BadRequestObjectResult>(result);
  }

  [Fact]
  public async Task SearchSparseByMetadata_RequiresACandidateBudget()
  {
    var result = await CreateController().SearchSparseByMetadata(DbName, Collection,
      new SparseByMetadataData { Sparse = Payload([1], [1f]), CandidateBudget = 0 }, CancellationToken.None);

    var badRequest = Assert.IsType<BadRequestObjectResult>(result);
    Assert.Contains("candidateBudget", badRequest.Value as string);
  }

  [Fact]
  public async Task SearchHybrid_ReturnsProvenanceAndOriginalScores()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<SearchHybrid>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new[]
      {
        new SearchVectorResultItem
        {
          Key = "a"u8.ToArray(), Score = 0.03f,
          InDense = true, InSparse = true, DenseScore = 0.9f, SparseScore = 4f
        }
      });

    var result = await CreateController().SearchHybrid(DbName, Collection,
      new HybridSearchData
      {
        Embeddings = [0.1f],
        Sparse = Payload([1], [1f]),
        DenseWeight = 1f,
        SparseWeight = 1f
      }, CancellationToken.None);

    var okResult = Assert.IsType<OkObjectResult>(result);
    var items = Assert.IsAssignableFrom<IEnumerable<SearchVectorResultItem>>(okResult.Value).ToArray();
    Assert.True(items[0].InDense);
    Assert.True(items[0].InSparse);
    Assert.Equal(0.9f, items[0].DenseScore);
  }

  [Fact]
  public async Task SearchHybrid_WithBothVectorsAndNoWeights_ReturnsBadRequest()
  {
    var result = await CreateController().SearchHybrid(DbName, Collection,
      new HybridSearchData { Embeddings = [0.1f], Sparse = Payload([1], [1f]) }, CancellationToken.None);

    var badRequest = Assert.IsType<BadRequestObjectResult>(result);
    Assert.Contains("sparseWeight", badRequest.Value as string);
    _mediatorMock.Verify(m => m.Send(It.IsAny<SearchHybrid>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task SearchHybrid_WithBothVectorsAndOnlyOneWeight_ReturnsBadRequest()
  {
    var result = await CreateController().SearchHybrid(DbName, Collection,
      new HybridSearchData
      {
        Embeddings = [0.1f],
        Sparse = Payload([1], [1f]),
        DenseWeight = 1f
      }, CancellationToken.None);

    var badRequest = Assert.IsType<BadRequestObjectResult>(result);
    Assert.Contains("sparseWeight", badRequest.Value as string);
    _mediatorMock.Verify(m => m.Send(It.IsAny<SearchHybrid>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task SearchHybrid_WithBothWeightsZero_ReturnsBadRequest()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<SearchHybrid>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new ArgumentException("At least one fusion weight must be greater than zero."));

    var result = await CreateController().SearchHybrid(DbName, Collection,
      new HybridSearchData
      {
        Embeddings = [0.1f],
        Sparse = Payload([1], [1f]),
        DenseWeight = 0f,
        SparseWeight = 0f
      }, CancellationToken.None);

    Assert.IsType<BadRequestObjectResult>(result);
  }

  [Fact]
  public async Task SearchHybrid_WithoutAnyVector_ReturnsBadRequest()
  {
    var result = await CreateController().SearchHybrid(DbName, Collection,
      new HybridSearchData(), CancellationToken.None);

    Assert.IsType<BadRequestObjectResult>(result);
  }

  [Fact]
  public async Task SearchHybrid_WithOnlyTheSparseVector_IsAccepted()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<SearchHybrid>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(Array.Empty<SearchVectorResultItem>());

    var result = await CreateController().SearchHybrid(DbName, Collection,
      new HybridSearchData { Sparse = Payload([1], [1f]) }, CancellationToken.None);

    Assert.IsType<OkObjectResult>(result);
    _mediatorMock.Verify(m => m.Send(
      It.Is<SearchHybrid>(q => q.Embeddings == null && q.Sparse != null),
      It.IsAny<CancellationToken>()), Times.Once);
  }
}
