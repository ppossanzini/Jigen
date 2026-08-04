using Hikyaku;
using Jigen.API;
using Microsoft.AspNetCore.Mvc;

namespace Jigen.API.Tests;

public class EmbeddingControllerTests
{
  private readonly Mock<IHikyaku> _mediatorMock = new();

  private EmbeddingController CreateController() => new(_mediatorMock.Object);

  // ── CalculateEmbeddings ──

  [Fact]
  public async Task CalculateEmbeddings_ReturnsEmbeddings()
  {
    var expected = new float[] { 0.1f, 0.2f, 0.3f };
    _mediatorMock.Setup(m => m.Send(
      It.Is<Jigen.TextEmbedding.Core.Commands.CalculateEmbeddings>(c => c.Sentence == "hello"),
      It.IsAny<CancellationToken>()))
      .ReturnsAsync(expected);

    var result = await CreateController().CalculateEmbeddings(
      new CalculateEmbeddingsRequest { Message = "hello" }, CancellationToken.None);

    var okResult = Assert.IsType<OkObjectResult>(result);
    Assert.Equal(expected, okResult.Value);
  }

  [Fact]
  public async Task CalculateEmbeddings_ReturnsBadRequest_WhenMessageEmpty()
  {
    var result = await CreateController().CalculateEmbeddings(
      new CalculateEmbeddingsRequest { Message = "" }, CancellationToken.None);

    Assert.IsType<BadRequestObjectResult>(result);
  }

  [Fact]
  public async Task CalculateEmbeddings_ReturnsBadRequest_WhenRequestNull()
  {
    var result = await CreateController().CalculateEmbeddings(null!, CancellationToken.None);

    Assert.IsType<BadRequestObjectResult>(result);
  }

  [Fact]
  public async Task CalculateEmbeddings_PassesTaskParameter()
  {
    const string task = "search_document";
    _mediatorMock.Setup(m => m.Send(
      It.Is<Jigen.TextEmbedding.Core.Commands.CalculateEmbeddings>(c => c.Task == task),
      It.IsAny<CancellationToken>()))
      .ReturnsAsync([0.5f]);

    await CreateController().CalculateEmbeddings(
      new CalculateEmbeddingsRequest { Message = "test", Task = task }, CancellationToken.None);

    _mediatorMock.Verify(m => m.Send(
      It.Is<Jigen.TextEmbedding.Core.Commands.CalculateEmbeddings>(c => c.Task == task),
      It.IsAny<CancellationToken>()), Times.Once);
  }

  // ── CalculateEmbeddingsBatch ──

  [Fact]
  public async Task CalculateEmbeddingsBatch_ReturnsResults()
  {
    var inputs = new[] { "hello", "world" };
    var vectors = new[] { new float[] { 1f, 2f }, new float[] { 3f, 4f } };
    _mediatorMock.Setup(m => m.Send(
      It.IsAny<Jigen.TextEmbedding.Core.Commands.CalculateEmbeddingsBatch>(),
      It.IsAny<CancellationToken>()))
      .ReturnsAsync(vectors);

    var result = await CreateController().CalculateEmbeddingsBatch(
      new CalculateEmbeddingsBatchRequest { Messages = inputs }, CancellationToken.None);

    var okResult = Assert.IsType<OkObjectResult>(result);
    var batchResult = Assert.IsType<EmbeddingBatchResult>(okResult.Value);
    Assert.Equal(2, batchResult.Results.Length);
    Assert.Equal(vectors[0], batchResult.Results[0]);
    Assert.Equal(vectors[1], batchResult.Results[1]);
  }

  [Fact]
  public async Task CalculateEmbeddingsBatch_PreservesBlankInputPositions()
  {
    var inputs = new[] { "valid", "", "also" };
    var vectors = new[] { new float[] { 1f }, new float[] { 3f } };
    _mediatorMock.Setup(m => m.Send(
      It.IsAny<Jigen.TextEmbedding.Core.Commands.CalculateEmbeddingsBatch>(),
      It.IsAny<CancellationToken>()))
      .ReturnsAsync(vectors);

    var result = await CreateController().CalculateEmbeddingsBatch(
      new CalculateEmbeddingsBatchRequest { Messages = inputs }, CancellationToken.None);

    var okResult = Assert.IsType<OkObjectResult>(result);
    var batchResult = Assert.IsType<EmbeddingBatchResult>(okResult.Value);
    Assert.Equal(3, batchResult.Results.Length);
    Assert.Equal(vectors[0], batchResult.Results[0]);  // "valid"
    Assert.Empty(batchResult.Results[1]);               // blank → empty
    Assert.Equal(vectors[1], batchResult.Results[2]);  // "also"
  }

  [Fact]
  public async Task CalculateEmbeddingsBatch_ReturnsBadRequest_WhenMessagesNull()
  {
    var result = await CreateController().CalculateEmbeddingsBatch(
      new CalculateEmbeddingsBatchRequest { Messages = null! }, CancellationToken.None);

    Assert.IsType<BadRequestObjectResult>(result);
  }

  [Fact]
  public async Task CalculateEmbeddingsBatch_ReturnsBadRequest_WhenMessagesEmpty()
  {
    var result = await CreateController().CalculateEmbeddingsBatch(
      new CalculateEmbeddingsBatchRequest { Messages = [] }, CancellationToken.None);

    Assert.IsType<BadRequestObjectResult>(result);
  }

  [Fact]
  public async Task CalculateEmbeddingsBatch_SkipsEmbedding_WhenAllBlank()
  {
    var inputs = new[] { "", "  ", null! };

    var result = await CreateController().CalculateEmbeddingsBatch(
      new CalculateEmbeddingsBatchRequest { Messages = inputs }, CancellationToken.None);

    var okResult = Assert.IsType<OkObjectResult>(result);
    var batchResult = Assert.IsType<EmbeddingBatchResult>(okResult.Value);
    Assert.Equal(3, batchResult.Results.Length);
    Assert.All(batchResult.Results, r => Assert.Empty(r));
  }
}
