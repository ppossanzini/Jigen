using Google.Protobuf;
using Grpc.Core;
using Hikyaku;
using Jigen.DataStructures;
using Jigen.Embedding.Core.Options;
using Jigen.Handlers;
using Jigen.Proto;
using Microsoft.Extensions.Options;
using ProtoSparseVector = Jigen.Proto.SparseVector;
using SparseVector = Jigen.DataStructures.SparseVector;

namespace Jigen.Grpc.Tests;

/// <summary>
/// Binary surface of the sparse slot. The cases mirror
/// CollectionsControllerSparseTests: the same three operations and the same
/// refusals, so the two surfaces cannot drift apart without a red test.
/// </summary>
public class ServiceSparseTests
{
  private readonly Mock<IHikyaku> _mediatorMock = new();
  private readonly Mock<IHikyaku> _hikyakuMock = new();
  private const string DbName = "testdb";
  private const string Collection = "testcoll";
  private static readonly byte[] KeyBytes = "k"u8.ToArray();

  private Server CreateServer() => new(_mediatorMock.Object, _hikyakuMock.Object,
    Options.Create(new PassageSplittingOptions()), Options.Create(new JigenServerSettings()));

  private static ServerCallContext CreateContext() => new TestServerCallContext();

  private static ProtoSparseVector Wire(int[] terms, float[] weights)
  {
    var message = new ProtoSparseVector();
    message.Terms.AddRange(terms);
    message.Weights.AddRange(weights);
    return message;
  }

  private static Vector VectorWithSparse(ProtoSparseVector sparse) => new()
  {
    Database = DbName,
    Collection = Collection,
    Key = ByteString.CopyFrom(KeyBytes),
    Content = ByteString.CopyFrom("doc"u8.ToArray()),
    Embeddings = { 0.1f, 0.2f },
    Sparse = sparse
  };

  private class TestServerCallContext : ServerCallContext
  {
    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) => null!;
    protected override string MethodCore => "test";
    protected override string HostCore => "localhost";
    protected override string PeerCore => "localhost";
    protected override DateTime DeadlineCore => DateTime.MaxValue;
    protected override Metadata RequestHeadersCore => new();
    protected override CancellationToken CancellationTokenCore => CancellationToken.None;
    protected override Metadata ResponseTrailersCore => new();
    protected override Status StatusCore { get; set; }
    protected override WriteOptions? WriteOptionsCore { get; set; }
    protected override AuthContext AuthContextCore => null!;
  }

  // ── Write ──

  [Fact]
  public async Task SetVector_CarriesSparseSlotToTheCommand()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<Core.Command.collections.SetVector>(), It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var result = await CreateServer().SetVector(VectorWithSparse(Wire([3, 7], [0.5f, 1.5f])), CreateContext());

    Assert.True(result.Success);
    _mediatorMock.Verify(m => m.Send(
      It.Is<Core.Command.collections.SetVector>(c =>
        c.Sparse.Terms.ToArray().SequenceEqual(new[] { 3, 7 })
        && c.Sparse.Weights.ToArray().SequenceEqual(new[] { 0.5f, 1.5f })),
      It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task SetVector_WithoutSparseMessage_LeavesTheSlotEmpty()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<Core.Command.collections.SetVector>(), It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    await CreateServer().SetVector(VectorWithSparse(null), CreateContext());

    _mediatorMock.Verify(m => m.Send(
      It.Is<Core.Command.collections.SetVector>(c => c.Sparse == null), It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task SetVector_WithUnorderedTerms_IsInvalidArgument()
  {
    var ex = await Assert.ThrowsAsync<RpcException>(() =>
      CreateServer().SetVector(VectorWithSparse(Wire([7, 3], [1f, 1f])), CreateContext()));

    Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    Assert.Contains("Term 3", ex.Status.Detail);
  }

  [Fact]
  public async Task SetVector_WithNegativeWeight_IsInvalidArgument()
  {
    var ex = await Assert.ThrowsAsync<RpcException>(() =>
      CreateServer().SetVector(VectorWithSparse(Wire([4], [-1f])), CreateContext()));

    Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    Assert.Contains("term 4", ex.Status.Detail);
  }

  [Fact]
  public async Task SetVector_WithAllWeightsZero_IsInvalidArgument()
  {
    var ex = await Assert.ThrowsAsync<RpcException>(() =>
      CreateServer().SetVector(VectorWithSparse(Wire([1, 2], [0f, 0f])), CreateContext()));

    Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
  }

  [Fact]
  public async Task SetVector_WhenTheStoreRejectsTheTermCount_IsInvalidArgument()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<Core.Command.collections.SetVector>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new ArgumentException("A sparse vector carries at most 4 terms; received 9."));

    var ex = await Assert.ThrowsAsync<RpcException>(() =>
      CreateServer().SetVector(VectorWithSparse(Wire(
        [1, 2, 3, 4, 5, 6, 7, 8, 9], [1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f])), CreateContext()));

    Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    Assert.Contains("at most 4 terms", ex.Status.Detail);
  }

  [Fact]
  public async Task SetVector_WithoutTheWal_IsFailedPrecondition()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<Core.Command.collections.SetVector>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("Writing a sparse vector requires the write-ahead log"));

    var ex = await Assert.ThrowsAsync<RpcException>(() =>
      CreateServer().SetVector(VectorWithSparse(Wire([1], [1f])), CreateContext()));

    Assert.Equal(StatusCode.FailedPrecondition, ex.StatusCode);
  }

  // ── Read ──

  [Fact]
  public async Task GetSparse_ReturnsTheStoredPairs()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<Core.Query.collections.GetSparse>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(SparseVector.Create([2, 9], [0.25f, 3f]));

    var result = await CreateServer().GetSparse(new ItemKey
    {
      Database = DbName,
      Collection = Collection,
      Key = ByteString.CopyFrom(KeyBytes)
    }, CreateContext());

    Assert.Equal(new[] { 2, 9 }, result.Terms.ToArray());
    Assert.Equal(new[] { 0.25f, 3f }, result.Weights.ToArray());
  }

  [Fact]
  public async Task GetSparse_ReturnsAnEmptyMessage_WhenTheEntryHasNoSlot()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<Core.Query.collections.GetSparse>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((SparseVector)null);

    var result = await CreateServer().GetSparse(new ItemKey
    {
      Database = DbName,
      Collection = Collection,
      Key = ByteString.CopyFrom(KeyBytes)
    }, CreateContext());

    Assert.Empty(result.Terms);
    Assert.Empty(result.Weights);
  }

  // ── Search ──

  [Fact]
  public async Task SearchSparse_ReturnsRankedKeysWithProvenance()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<Core.Query.collections.SearchSparse>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new[]
      {
        new Core.Query.collections.SearchVectorResultItem
        {
          Key = "a"u8.ToArray(), Score = 2f, InDense = false, InSparse = true, SparseScore = 2f
        }
      });

    var result = await CreateServer().SearchSparse(new SearchSparseRequest
    {
      Database = DbName,
      Collection = Collection,
      Sparse = Wire([1], [1f]),
      Top = 5
    }, CreateContext());

    var item = Assert.Single(result.Results);
    Assert.True(item.InSparse);
    Assert.False(item.InDense);
    Assert.Equal(2f, item.SparseScore);
  }

  [Fact]
  public async Task SearchSparse_WithAnEmptySparseMessage_IsInvalidArgument()
  {
    var ex = await Assert.ThrowsAsync<RpcException>(() =>
      CreateServer().SearchSparse(new SearchSparseRequest
      {
        Database = DbName,
        Collection = Collection,
        Sparse = new ProtoSparseVector(),
        Top = 5
      }, CreateContext()));

    Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    Assert.Contains("at least one term", ex.Status.Detail);
  }

  [Fact]
  public async Task SearchSparse_WithoutSparseMessage_IsInvalidArgument()
  {
    var ex = await Assert.ThrowsAsync<RpcException>(() =>
      CreateServer().SearchSparse(new SearchSparseRequest
      {
        Database = DbName,
        Collection = Collection,
        Top = 5
      }, CreateContext()));

    Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
  }

  [Fact]
  public async Task SearchSparseByMetadata_ReturnsGroups()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<Core.Query.collections.SearchSparseByMetadata>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new[]
      {
        new Core.Query.collections.SearchSparseByMetadataResultItem
        {
          MetadataIndex = 12, Key = "a"u8.ToArray(), Content = "doc"u8.ToArray(), Score = 3f
        }
      });

    var result = await CreateServer().SearchSparseByMetadata(new SearchSparseByMetadataRequest
    {
      Database = DbName,
      Collection = Collection,
      Sparse = Wire([1], [1f]),
      Top = 5,
      CandidateBudget = 100
    }, CreateContext());

    var item = Assert.Single(result.Results);
    Assert.Equal(12, item.MetadataIndex);
  }

  [Fact]
  public async Task SearchHybrid_ReturnsProvenanceAndOriginalScores()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<Core.Query.collections.SearchHybrid>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new[]
      {
        new Core.Query.collections.SearchVectorResultItem
        {
          Key = "a"u8.ToArray(), Score = 0.03f,
          InDense = true, InSparse = true, DenseScore = 0.9f, SparseScore = 4f
        }
      });

    var result = await CreateServer().SearchHybrid(new SearchHybridRequest
    {
      Database = DbName,
      Collection = Collection,
      Embeddings = { 0.1f },
      Sparse = Wire([1], [1f]),
      Top = 5,
      DenseWeight = 1f,
      SparseWeight = 1f
    }, CreateContext());

    var item = Assert.Single(result.Results);
    Assert.True(item.InDense);
    Assert.True(item.InSparse);
    Assert.Equal(0.9f, item.DenseScore);
    Assert.Equal(4f, item.SparseScore);
  }

  [Fact]
  public async Task SearchHybrid_WithBothVectorsAndNoWeights_IsInvalidArgument()
  {
    var ex = await Assert.ThrowsAsync<RpcException>(() =>
      CreateServer().SearchHybrid(new SearchHybridRequest
      {
        Database = DbName,
        Collection = Collection,
        Embeddings = { 0.1f },
        Sparse = Wire([1], [1f]),
        Top = 5
      }, CreateContext()));

    Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    Assert.Contains("SparseWeight", ex.Status.Detail);
  }

  [Fact]
  public async Task SearchHybrid_WithBothWeightsZero_IsInvalidArgument()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<Core.Query.collections.SearchHybrid>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new ArgumentException("At least one fusion weight must be greater than zero."));

    var ex = await Assert.ThrowsAsync<RpcException>(() =>
      CreateServer().SearchHybrid(new SearchHybridRequest
      {
        Database = DbName,
        Collection = Collection,
        Embeddings = { 0.1f },
        Sparse = Wire([1], [1f]),
        Top = 5,
        DenseWeight = 0f,
        SparseWeight = 0f
      }, CreateContext()));

    Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
  }

  [Fact]
  public async Task SearchHybrid_WithoutAnyVector_IsInvalidArgument()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<Core.Query.collections.SearchHybrid>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new ArgumentException("A hybrid search needs at least one of the two vectors."));

    var ex = await Assert.ThrowsAsync<RpcException>(() =>
      CreateServer().SearchHybrid(new SearchHybridRequest
      {
        Database = DbName,
        Collection = Collection,
        Top = 5
      }, CreateContext()));

    Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
  }

  [Fact]
  public async Task SearchHybrid_WithOnlyTheSparseVector_IsAccepted()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<Core.Query.collections.SearchHybrid>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(Array.Empty<Core.Query.collections.SearchVectorResultItem>());

    var result = await CreateServer().SearchHybrid(new SearchHybridRequest
    {
      Database = DbName,
      Collection = Collection,
      Sparse = Wire([1], [1f]),
      Top = 5
    }, CreateContext());

    Assert.Empty(result.Results);
    _mediatorMock.Verify(m => m.Send(
      It.Is<Core.Query.collections.SearchHybrid>(q => q.Embeddings.Length == 0 && q.Sparse != null),
      It.IsAny<CancellationToken>()), Times.Once);
  }
}
