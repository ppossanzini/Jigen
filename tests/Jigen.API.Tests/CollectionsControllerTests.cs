using Hikyaku;
using Jigen.API;
using Jigen.API.Dto;
using Jigen.Core.Command.collections;
using Jigen.Core.Query.collections;
using Jigen.DataStructures;
using Microsoft.AspNetCore.Mvc;

namespace Jigen.API.Tests;

public class CollectionsControllerTests
{
  private readonly Mock<IHikyaku> _mediatorMock = new();
  private readonly Mock<IDocumentSerializer> _serializerMock = new();
  private const string DbName = "testdb";
  private const string Collection = "testcoll";

  private CollectionsController CreateController() =>
    new(_mediatorMock.Object, _serializerMock.Object);

  // ── GetCollectionsInfo ──

  [Fact]
  public async Task GetCollectionsInfo_ReturnsAllCollections()
  {
    var expected = new List<CollectionInfo>
    {
      new() { Name = "col1", Vectors = 10, Dimensions = 1536 },
      new() { Name = "col2", Vectors = 5, Dimensions = 768 }
    };
    _mediatorMock.Setup(m => m.Send(It.Is<GetCollectionsInfo>(q => q.Database == DbName), It.IsAny<CancellationToken>()))
      .ReturnsAsync(expected);

    var result = await CreateController().GetCollectionsInfo(DbName);

    var okResult = Assert.IsType<OkObjectResult>(result);
    Assert.Equal(expected, okResult.Value);
  }

  // ── Count ──

  [Fact]
  public async Task Count_ReturnsVectorCount()
  {
    _mediatorMock.Setup(m => m.Send(It.Is<Count>(c => c.Database == DbName && c.Collection == Collection), It.IsAny<CancellationToken>()))
      .ReturnsAsync(42);

    var result = await CreateController().Count(DbName, Collection);

    var okResult = Assert.IsType<OkObjectResult>(result);
    Assert.Equal(42, okResult.Value);
  }

  // ── Clear ──

  [Fact]
  public async Task Clear_SendsClearCommand()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<Clear>(), It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var result = await CreateController().Clear(DbName, Collection);

    Assert.IsType<OkResult>(result);
    _mediatorMock.Verify(m => m.Send(
      It.Is<Clear>(c => c.Database == DbName && c.Collection == Collection),
      It.IsAny<CancellationToken>()), Times.Once);
  }

  // ── GetAllKeys ──

  [Fact]
  public async Task GetAllKeys_ReturnsKeyList()
  {
    var keys = new List<VectorKey> { VectorKey.From("key1"), VectorKey.From("key2") };
    _mediatorMock.Setup(m => m.Send(It.Is<GetAllKeys>(q => q.Database == DbName && q.Collection == Collection), It.IsAny<CancellationToken>()))
      .ReturnsAsync(keys);

    var result = await CreateController().GetAllKeys(DbName, Collection);

    var okResult = Assert.IsType<OkObjectResult>(result);
    var returned = Assert.IsAssignableFrom<IEnumerable<byte[]>>(okResult.Value);
    Assert.Equal(2, returned.Count());
  }

  // ── Contains ──

  [Fact]
  public async Task ContainsDocument_ReturnsOk_WhenExists()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<Contains>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(true);

    var result = await CreateController().ContainsDocument(DbName, Collection, "testkey", "string");

    Assert.IsType<OkResult>(result);
  }

  [Fact]
  public async Task ContainsDocument_ReturnsNotFound_WhenMissing()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<Contains>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(false);

    var result = await CreateController().ContainsDocument(DbName, Collection, "missing", "string");

    Assert.IsType<NotFoundResult>(result);
  }

  [Fact]
  public async Task ContainsDocument_ReturnsBadRequest_ForInvalidKeyType()
  {
    var result = await CreateController().ContainsDocument(DbName, Collection, "not-a-number", "int");

    Assert.IsType<BadRequestObjectResult>(result);
  }

  // ── GetEmbedding ──

  [Fact]
  public async Task GetEmbedding_ReturnsEmbeddingArray()
  {
    var expected = new float[] { 0.1f, 0.2f, 0.3f };
    _mediatorMock.Setup(m => m.Send(It.Is<GetEmbedding>(q => q.Database == DbName && q.Collection == Collection), It.IsAny<CancellationToken>()))
      .ReturnsAsync(expected);

    var result = await CreateController().GetEmbedding(DbName, Collection, "testkey", "string");

    var okResult = Assert.IsType<OkObjectResult>(result);
    Assert.Equal(expected, okResult.Value);
  }

  [Fact]
  public async Task GetEmbedding_ReturnsEmptyArray_WhenKeyMissing()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<GetEmbedding>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((float[]?)null);

    var result = await CreateController().GetEmbedding(DbName, Collection, "missing", "string");

    var okResult = Assert.IsType<OkObjectResult>(result);
    var value = Assert.IsType<float[]>(okResult.Value);
    Assert.Empty(value);
  }

  // ── SetVector ──

  [Fact]
  public async Task SetVector_SendsCommand_WithValidPayload()
  {
    var payload = new VectorPayload { Embeddings = [0.1f, 0.2f] };
    _mediatorMock.Setup(m => m.Send(It.IsAny<SetVector>(), It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var result = await CreateController().SetVector(DbName, Collection, "testkey", payload, "string");

    Assert.IsType<OkResult>(result);
    _mediatorMock.Verify(m => m.Send(It.Is<SetVector>(
      c => c.Database == DbName && c.Collection == Collection),
      It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task SetVector_ReturnsBadRequest_WhenEmbeddingsMissing()
  {
    var payload = new VectorPayload { Embeddings = null! };

    var result = await CreateController().SetVector(DbName, Collection, "testkey", payload, "string");

    Assert.IsType<BadRequestObjectResult>(result);
  }

  [Fact]
  public async Task SetVector_ReturnsBadRequest_WhenEmbeddingsEmpty()
  {
    var payload = new VectorPayload { Embeddings = [] };

    var result = await CreateController().SetVector(DbName, Collection, "testkey", payload, "string");

    Assert.IsType<BadRequestObjectResult>(result);
  }

  // ── SetVectorsBulk ──

  [Fact]
  public async Task SetVectorsBulk_AcceptsAllValidItems()
  {
    var items = new List<BulkVectorItem>
    {
      new() { Key = "key1", KeyType = "string", Embeddings = [1f, 2f] },
      new() { Key = "key2", KeyType = "string", Embeddings = [3f, 4f] }
    };
    _mediatorMock.Setup(m => m.Send(It.IsAny<SetVector>(), It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var result = await CreateController().SetVectorsBulk(DbName, Collection, items);

    var okResult = Assert.IsType<OkObjectResult>(result);
    var bulkResult = Assert.IsType<BulkResult>(okResult.Value);
    Assert.Equal(2, bulkResult.Accepted);
  }

  [Fact]
  public async Task SetVectorsBulk_ReturnsBadRequest_WhenItemsNull()
  {
    var result = await CreateController().SetVectorsBulk(DbName, Collection, null!);

    Assert.IsType<BadRequestObjectResult>(result);
  }

  // ── SetDocumentsBulk ──

  [Fact]
  public async Task SetDocumentsBulk_AcceptsAllValidItems()
  {
    var items = new List<BulkDocumentItem>
    {
      new() { Key = "doc1", KeyType = "string", Sentence = "hello" },
      new() { Key = "doc2", KeyType = "string", Sentence = "world" }
    };
    _mediatorMock.Setup(m => m.Send(It.IsAny<Core.Command.collections.SetDocument>(), It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var result = await CreateController().SetDocumentsBulk(DbName, Collection, items);

    var okResult = Assert.IsType<OkObjectResult>(result);
    var bulkResult = Assert.IsType<BulkResult>(okResult.Value);
    Assert.Equal(2, bulkResult.Accepted);
  }

  // ── Search (single-collection) ──

  [Fact]
  public async Task SearchCollection_ReturnsBadRequest_WhenBothEmbeddingsAndSentenceEmpty()
  {
    var request = new SearchData { Embeddings = null!, Sentence = null! };

    var result = await CreateController().SearchCollection(DbName, Collection, request, CancellationToken.None);

    Assert.IsType<BadRequestObjectResult>(result);
  }
}
