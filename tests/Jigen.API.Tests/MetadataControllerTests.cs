using Hikyaku;
using Jigen.API;
using Jigen.API.Dto;
using Jigen.Core.Command.metadata;
using Jigen.Core.Query.metadata;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jigen.API.Tests;

public class MetadataControllerTests
{
  private readonly Mock<IHikyaku> _mediatorMock = new();
  private readonly Mock<IDocumentSerializer> _serializerMock = new();
  private const string DbName = "testdb";
  private const string Collection = "testcoll";
  private static readonly byte[] StoredContent = "stored-content"u8.ToArray();
  private static readonly byte[] KeyBytes = "abc"u8.ToArray();

  public MetadataControllerTests()
  {
    _serializerMock
      .Setup(s => s.FromJson(It.IsAny<string>()))
      .Returns(StoredContent);
  }

  private MetadataController CreateController() =>
    new(_mediatorMock.Object, _serializerMock.Object);

  private static int ReadMetadataIndex(IActionResult result)
  {
    // OkObjectResult and ObjectResult both carry the anonymous payload.
    var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
    var property = objectResult.Value!.GetType().GetProperty("metadataIndex");
    Assert.NotNull(property);
    return (int)property!.GetValue(objectResult.Value)!;
  }

  // ── Create / Update ──

  [Fact]
  public async Task Create_ReturnsCreated_WithMetadataIndex()
  {
    _mediatorMock.Setup(m => m.Send(
        It.Is<CreateMetadata>(c => c.Database == DbName && c.Content.SequenceEqual(StoredContent)),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(7);

    var result = await CreateController().Create(DbName, new MetadataPayload { Payload = new { title = "a" } },
      CancellationToken.None);

    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);
    Assert.Equal(7, ReadMetadataIndex(result));
  }

  [Fact]
  public async Task Create_ReturnsBadRequest_WhenPayloadMissing()
  {
    var result = await CreateController().Create(DbName, new MetadataPayload(), CancellationToken.None);

    Assert.IsType<BadRequestObjectResult>(result);
    _mediatorMock.VerifyNoOtherCalls();
  }

  [Fact]
  public async Task Update_ReturnsOk_WhenMetadataExists()
  {
    _mediatorMock.Setup(m => m.Send(
        It.Is<UpdateMetadata>(c => c.MetadataIndex == 3 && c.Database == DbName),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(true);

    var result = await CreateController().Update(DbName, 3, new MetadataPayload { Payload = new { title = "b" } },
      CancellationToken.None);

    Assert.IsType<OkResult>(result);
  }

  [Fact]
  public async Task Update_ReturnsNotFound_WhenMetadataMissing()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<UpdateMetadata>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(false);

    var result = await CreateController().Update(DbName, 99, new MetadataPayload { Payload = new { title = "b" } },
      CancellationToken.None);

    Assert.IsType<NotFoundObjectResult>(result);
  }

  // ── Association ──

  [Fact]
  public async Task GetAssociation_ReturnsIndex()
  {
    _mediatorMock.Setup(m => m.Send(
        It.Is<GetMetadataIndex>(q => q.Collection == Collection && q.Key.SequenceEqual(KeyBytes)),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(5);

    var result = await CreateController().GetAssociation(DbName, Collection, "abc");

    var okResult = Assert.IsType<OkObjectResult>(result);
    Assert.Equal(5, ReadMetadataIndex(result));
    Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
  }

  [Fact]
  public async Task GetAssociation_ReturnsNotFound_WhenNotAssociated()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<GetMetadataIndex>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(-1);

    var result = await CreateController().GetAssociation(DbName, Collection, "abc");

    Assert.IsType<NotFoundResult>(result);
  }

  [Fact]
  public async Task GetAssociation_ReturnsBadRequest_WhenKeyInvalid()
  {
    var result = await CreateController().GetAssociation(DbName, Collection, "abc", keyType: "int");

    Assert.IsType<BadRequestObjectResult>(result);
    _mediatorMock.VerifyNoOtherCalls();
  }

  [Fact]
  public async Task Associate_SendsCommandWithResolvedKey()
  {
    AssociateEmbedding captured = null;
    _mediatorMock.Setup(m => m.Send(It.IsAny<AssociateEmbedding>(), It.IsAny<CancellationToken>()))
      .Callback<AssociateEmbedding, CancellationToken>((command, _) => captured = command)
      .Returns(Task.CompletedTask);

    var result = await CreateController().Associate(DbName, Collection, "abc",
      new MetadataAssociationPayload { MetadataIndex = 11 });

    Assert.IsType<OkResult>(result);
    Assert.NotNull(captured);
    Assert.Equal(DbName, captured.Database);
    Assert.Equal(Collection, captured.Collection);
    Assert.Equal(11, captured.MetadataIndex);
    Assert.Equal(KeyBytes, captured.Key);
  }

  [Fact]
  public async Task Associate_ReturnsBadRequest_WhenHandlerRejectsTheTarget()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<AssociateEmbedding>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new ArgumentException("Embedding not found"));

    var result = await CreateController().Associate(DbName, Collection, "abc",
      new MetadataAssociationPayload { MetadataIndex = 99 });

    Assert.IsType<BadRequestObjectResult>(result);
  }

  // ── Grouped search ──

  [Fact]
  public async Task Search_ReturnsGroups()
  {
    var expected = new[]
    {
      new SearchByMetadataResultItem { MetadataIndex = 4, Key = [1], Content = [9], Score = 0.9f }
    };
    _mediatorMock.Setup(m => m.Send(
        It.Is<SearchByMetadata>(q => q.Database == DbName && q.Collection == Collection &&
                                    q.Top == 5 && q.CandidateBudget == 50),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(expected);

    var result = await CreateController().Search(DbName, Collection,
      new MetadataSearchData { Embeddings = [0.1f], Top = 5, CandidateBudget = 50 }, CancellationToken.None);

    var objectResult = Assert.IsType<OkObjectResult>(result);
    Assert.Same(expected, objectResult.Value);
  }

  [Fact]
  public async Task Search_ReturnsBadRequest_WhenBudgetMissing()
  {
    var result = await CreateController().Search(DbName, Collection,
      new MetadataSearchData { Embeddings = [0.1f], Top = 5 }, CancellationToken.None);

    Assert.IsType<BadRequestObjectResult>(result);
    _mediatorMock.VerifyNoOtherCalls();
  }

  [Fact]
  public async Task Search_ReturnsBadRequest_WhenTopMissing()
  {
    var result = await CreateController().Search(DbName, Collection,
      new MetadataSearchData { Embeddings = [0.1f], Top = 0, CandidateBudget = 50 }, CancellationToken.None);

    Assert.IsType<BadRequestObjectResult>(result);
  }

  [Fact]
  public async Task Search_ReturnsBadRequest_WhenNoQueryProvided()
  {
    var result = await CreateController().Search(DbName, Collection,
      new MetadataSearchData { Top = 5, CandidateBudget = 50 }, CancellationToken.None);

    Assert.IsType<BadRequestObjectResult>(result);
  }
}
