using Google.Protobuf;
using Grpc.Core;
using Hikyaku;
using Jigen.DataStructures;
using Jigen.Grpc;
using Jigen.Proto;

namespace Jigen.Grpc.Tests;

public class ServiceTests
{
  private readonly Mock<IHikyaku> _mediatorMock = new();
  private readonly Mock<IHikyaku> _hikyakuMock = new();
  private const string DbName = "testdb";
  private const string Collection = "testcoll";

  private Server CreateServer() => new(_mediatorMock.Object, _hikyakuMock.Object);

  private static ServerCallContext CreateContext() =>
    new TestServerCallContext();

  /// <summary>Minimal ServerCallContext for unit tests.</summary>
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

  // ── GetCollectionInfo ──

  [Fact]
  public async Task GetCollectionInfo_ReturnsFullInfo()
  {
    var info = new CollectionInfo
    {
      Name = Collection,
      Vectors = 100,
      Dimensions = 768,
      ContentSize = 1024,
      VectorSize = 2048,
      Index = new CollectionIndexInfo
      {
        IndexSizeBytes = 512,
        Nodes = 100,
        MaxLevel = 5,
        NodesPerLevel = [50, 25, 15, 7, 3],
        AverageDegree = 16.5,
        Quantization = "SQ8"
      }
    };
    _mediatorMock.Setup(m => m.Send(
      It.Is<Core.Query.collections.GetCollectionInfo>(q => q.Database == DbName && q.Collection == Collection),
      It.IsAny<CancellationToken>()))
      .ReturnsAsync(info);

    var response = await CreateServer().GetCollectionInfo(
      new CollectionKey { Database = DbName, Collection = Collection },
      CreateContext());

    Assert.Equal(Collection, response.Name);
    Assert.Equal(100, response.Vectors);
    Assert.Equal(768, response.Dimensions);
    Assert.Equal(1024, response.ContentSize);
    Assert.Equal(2048, response.VectorSize);
    Assert.NotNull(response.Index);
    Assert.Equal(512, response.Index.IndexSizeBytes);
    Assert.Equal(100, response.Index.Nodes);
    Assert.Equal(5, response.Index.MaxLevel);
    Assert.Equal(5, response.Index.NodesPerLevel.Count);
    Assert.Equal(16.5, response.Index.AverageDegree);
    Assert.Equal("SQ8", response.Index.Quantization);
  }

  [Fact]
  public async Task GetCollectionInfo_ReturnsWithoutIndex_WhenIndexNull()
  {
    var info = new CollectionInfo
    {
      Name = Collection, Vectors = 10, Dimensions = 384, Index = null
    };
    _mediatorMock.Setup(m => m.Send(It.IsAny<Core.Query.collections.GetCollectionInfo>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(info);

    var response = await CreateServer().GetCollectionInfo(
      new CollectionKey { Database = DbName, Collection = Collection },
      CreateContext());

    Assert.Equal(Collection, response.Name);
    Assert.Null(response.Index);
  }

  // ── GetCollectionsInfo ──

  [Fact]
  public async Task GetCollectionsInfo_ReturnsAllCollections()
  {
    var infos = new List<CollectionInfo>
    {
      new() { Name = "col1", Vectors = 5 },
      new() { Name = "col2", Vectors = 10 }
    };
    _mediatorMock.Setup(m => m.Send(
      It.Is<Core.Query.collections.GetCollectionsInfo>(q => q.Database == DbName),
      It.IsAny<CancellationToken>()))
      .ReturnsAsync(infos);

    var response = await CreateServer().GetCollectionsInfo(
      new CollectionKey { Database = DbName, Collection = "" },
      CreateContext());

    Assert.Equal(2, response.Collections.Count);
    Assert.Equal("col1", response.Collections[0].Name);
    Assert.Equal("col2", response.Collections[1].Name);
  }

  // ── GetCollectionGraph ──

  [Fact]
  public async Task GetCollectionGraph_ReturnsGraphSnapshot()
  {
    var snapshot = new IndexGraphSnapshot
    {
      Collection = Collection,
      Dimensions = 2,
      TotalNodes = 100,
      LiveNodes = 90,
      DeletedNodes = 10,
      ReturnedNodes = 50,
      MaxLevel = 4,
      EntrypointPositionId = 1,
      Truncated = true,
      Nodes = new List<IndexGraphNode>
      {
        new() { PositionId = 1, Key = "a2V5MQ==", MaxLevel = 4, Degree = 16, Position = [0.1f, 0.2f] }
      },
      Edges = new List<IndexGraphEdge>
      {
        new() { Source = 1, Target = 2, Level = 0 }
      }
    };
    _mediatorMock.Setup(m => m.Send(It.IsAny<Core.Query.collections.GetCollectionGraph>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(snapshot);

    var response = await CreateServer().GetCollectionGraph(
      new GetGraphRequest { Database = DbName, Collection = Collection, Dimensions = 2, Limit = 2000 },
      CreateContext());

    Assert.Equal(Collection, response.Collection);
    Assert.Equal(2, response.Dimensions);
    Assert.Equal(100, response.TotalNodes);
    Assert.Equal(90, response.LiveNodes);
    Assert.Equal(10, response.DeletedNodes);
    Assert.Equal(50, response.ReturnedNodes);
    Assert.True(response.Truncated);
    Assert.Single(response.Nodes);
    Assert.Equal(1, response.Nodes[0].PositionId);
    Assert.Single(response.Edges);
    Assert.Equal(0, response.Edges[0].Level);
  }

  [Fact]
  public async Task GetCollectionGraph_UsesDefaultDimensions_WhenZero()
  {
    _mediatorMock.Setup(m => m.Send(
      It.Is<Core.Query.collections.GetCollectionGraph>(q => q.Dimensions == 2),
      It.IsAny<CancellationToken>()))
      .ReturnsAsync(new IndexGraphSnapshot { Collection = Collection });

    await CreateServer().GetCollectionGraph(
      new GetGraphRequest { Database = DbName, Collection = Collection, Dimensions = 0 },
      CreateContext());

    _mediatorMock.Verify(m => m.Send(
      It.Is<Core.Query.collections.GetCollectionGraph>(q => q.Dimensions == 2),
      It.IsAny<CancellationToken>()), Times.Once);
  }

  // ── SearchFilter ──

  [Fact]
  public async Task SearchFilter_SendsEmptyEmbeddingsAndMaxTop()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<Core.Query.collections.SearchVector>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(Array.Empty<Core.Query.collections.SearchVectorResultItem>());

    await CreateServer().SearchFilter(
      new SearchFilterRequest { Database = DbName, Collection = Collection },
      CreateContext());

    _mediatorMock.Verify(m => m.Send(
      It.Is<Core.Query.collections.SearchVector>(q =>
        q.Embeddings.Length == 0 && q.Top == int.MaxValue),
      It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task SearchFilter_ReturnsSearchResponse_WithResults()
  {
    var results = new List<Core.Query.collections.SearchVectorResultItem>
    {
      new() { Key = [1, 2, 3], Content = [4, 5, 6], Score = 0 }
    };
    _mediatorMock.Setup(m => m.Send(It.IsAny<Core.Query.collections.SearchVector>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(results);

    var response = await CreateServer().SearchFilter(
      new SearchFilterRequest { Database = DbName, Collection = Collection },
      CreateContext());

    Assert.Single(response.Results);
    Assert.Equal(ByteString.CopyFrom([1, 2, 3]), response.Results[0].Key);
  }

  // ── AppendDocument ──

  [Fact]
  public async Task AppendDocument_SendsAppendCommand()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<Core.Command.collections.AppendDocument>(), It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var response = await CreateServer().AppendDocument(
      new Document { Database = DbName, Collection = Collection, Key = ByteString.CopyFromUtf8("k"), Sentence = "hello" },
      CreateContext());

    Assert.True(response.Success);
    _mediatorMock.Verify(m => m.Send(
      It.Is<Core.Command.collections.AppendDocument>(c =>
        c.Database == DbName && c.Collection == Collection),
      It.IsAny<CancellationToken>()), Times.Once);
  }

  // ── AppendVector ──

  [Fact]
  public async Task AppendVector_SendsAppendCommand()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<Core.Command.collections.AppendVector>(), It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var response = await CreateServer().AppendVector(
      new Vector { Database = DbName, Collection = Collection, Key = ByteString.CopyFromUtf8("k"), Embeddings = { 0.5f } },
      CreateContext());

    Assert.True(response.Success);
    _mediatorMock.Verify(m => m.Send(
      It.Is<Core.Command.collections.AppendVector>(c =>
        c.Database == DbName && c.Collection == Collection),
      It.IsAny<CancellationToken>()), Times.Once);
  }

  // ── SetRawVector ──

  [Fact]
  public async Task SetRawVector_SendsSetRawCommand()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<Core.Command.collections.SetRawVector>(), It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var response = await CreateServer().SetRawVector(
      new Vector { Database = DbName, Collection = Collection, Key = ByteString.CopyFromUtf8("k"), Embeddings = { 0.5f } },
      CreateContext());

    Assert.True(response.Success);
    _mediatorMock.Verify(m => m.Send(
      It.Is<Core.Command.collections.SetRawVector>(c =>
        c.Database == DbName && c.Collection == Collection),
      It.IsAny<CancellationToken>()), Times.Once);
  }

  // ── Transaction (placeholder) ──

  [Fact]
  public async Task Transaction_ThrowsUnimplemented()
  {
    var ex = await Assert.ThrowsAsync<RpcException>(() =>
      CreateServer().Transaction(null!, null!, CreateContext()));

    Assert.Equal(StatusCode.Unimplemented, ex.StatusCode);
  }
}
