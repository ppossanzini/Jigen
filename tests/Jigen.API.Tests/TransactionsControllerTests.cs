using Hikyaku;
using Jigen.API;
using Microsoft.AspNetCore.Mvc;

namespace Jigen.API.Tests;

public class TransactionsControllerTests
{
  private readonly Mock<IHikyaku> _mediatorMock = new();
  private const string DbName = "testdb";

  private TransactionsController CreateController() => new(_mediatorMock.Object);

  // ── BeginTransaction ──

  [Fact]
  public void BeginTransaction_ReturnsTransactionId()
  {
    var result = CreateController().BeginTransaction(DbName);

    var okResult = Assert.IsType<OkObjectResult>(result);
    var response = Assert.IsType<CreateTransactionResponse>(okResult.Value);
    Assert.NotEqual(Guid.Empty, response.TransactionId);
  }

  [Fact]
  public void BeginTransaction_CreatesUniqueIds()
  {
    var controller = CreateController();

    var result1 = ((OkObjectResult)controller.BeginTransaction(DbName)).Value as CreateTransactionResponse;
    var result2 = ((OkObjectResult)controller.BeginTransaction(DbName)).Value as CreateTransactionResponse;

    Assert.NotEqual(result1!.TransactionId, result2!.TransactionId);
  }

  // ── AppendDocument ──

  [Fact]
  public async Task AppendDocument_ReturnsAccepted_WhenValid()
  {
    var controller = CreateController();
    var beginResult = (OkObjectResult)controller.BeginTransaction(DbName);
    var txId = ((CreateTransactionResponse)beginResult.Value!).TransactionId;

    var payload = new TransactionDocumentPayload
    {
      Collection = "docs",
      Key = "key1",
      Sentence = "hello"
    };

    var result = await controller.AppendDocument(txId, payload);

    var okResult = Assert.IsType<OkObjectResult>(result);
    Assert.NotNull(okResult.Value);
  }

  [Fact]
  public async Task AppendDocument_ReturnsNotFound_ForInvalidTxId()
  {
    var result = await CreateController().AppendDocument(Guid.NewGuid(),
      new TransactionDocumentPayload { Collection = "c", Key = "k" });

    Assert.IsType<NotFoundObjectResult>(result);
  }

  [Fact]
  public async Task AppendDocument_ReturnsBadRequest_WhenPayloadNull()
  {
    var controller = CreateController();
    var beginResult = (OkObjectResult)controller.BeginTransaction(DbName);
    var txId = ((CreateTransactionResponse)beginResult.Value!).TransactionId;

    var result = await controller.AppendDocument(txId, null!);

    Assert.IsType<BadRequestObjectResult>(result);
  }

  // ── AppendVector ──

  [Fact]
  public async Task AppendVector_ReturnsAccepted_WhenValid()
  {
    var controller = CreateController();
    var beginResult = (OkObjectResult)controller.BeginTransaction(DbName);
    var txId = ((CreateTransactionResponse)beginResult.Value!).TransactionId;

    var payload = new TransactionVectorPayload
    {
      Collection = "vecs",
      Key = "v1",
      Embeddings = [0.1f, 0.2f]
    };

    var result = await controller.AppendVector(txId, payload);

    Assert.IsType<OkObjectResult>(result);
  }

  [Fact]
  public async Task AppendVector_ReturnsBadRequest_WhenPayloadNull()
  {
    var controller = CreateController();
    var beginResult = (OkObjectResult)controller.BeginTransaction(DbName);
    var txId = ((CreateTransactionResponse)beginResult.Value!).TransactionId;

    var result = await controller.AppendVector(txId, null!);

    Assert.IsType<BadRequestObjectResult>(result);
  }

  // ── Delete ──

  [Fact]
  public void Delete_ReturnsAccepted_WhenValid()
  {
    var controller = CreateController();
    var beginResult = (OkObjectResult)controller.BeginTransaction(DbName);
    var txId = ((CreateTransactionResponse)beginResult.Value!).TransactionId;

    var result = controller.Delete(txId,
      new TransactionDeletePayload { Collection = "c", Key = "k" });

    Assert.IsType<OkObjectResult>(result);
  }

  [Fact]
  public void Delete_ReturnsBadRequest_WhenCollectionMissing()
  {
    var controller = CreateController();
    var beginResult = (OkObjectResult)controller.BeginTransaction(DbName);
    var txId = ((CreateTransactionResponse)beginResult.Value!).TransactionId;

    var result = controller.Delete(txId,
      new TransactionDeletePayload { Collection = "", Key = "k" });

    Assert.IsType<BadRequestObjectResult>(result);
  }

  // ── Commit ──

  [Fact]
  public async Task Commit_DispatchesBufferedOps()
  {
    _mediatorMock.Setup(m => m.Send(It.IsAny<Core.Command.collections.SetDocument>(), It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var controller = CreateController();
    var beginResult = (OkObjectResult)controller.BeginTransaction(DbName);
    var txId = ((CreateTransactionResponse)beginResult.Value!).TransactionId;

    // Buffer an operation
    await controller.AppendDocument(txId, new TransactionDocumentPayload
      { Collection = "c", Key = "k", Sentence = "test" });

    // Commit
    var result = await controller.Commit(txId);

    var okResult = Assert.IsType<OkObjectResult>(result);
    _mediatorMock.Verify(m => m.Send(
      It.IsAny<Core.Command.collections.SetDocument>(),
      It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task Commit_ReturnsNotFound_ForInvalidTxId()
  {
    var result = await CreateController().Commit(Guid.NewGuid());

    Assert.IsType<NotFoundObjectResult>(result);
  }

  [Fact]
  public async Task Commit_ReturnsNotFound_WhenAlreadyFinalized()
  {
    var controller = CreateController();
    var beginResult = (OkObjectResult)controller.BeginTransaction(DbName);
    var txId = ((CreateTransactionResponse)beginResult.Value!).TransactionId;

    await controller.Commit(txId);
    var secondCommit = await controller.Commit(txId);

    // After commit, the transaction is removed — subsequent operations return NotFound.
    Assert.IsType<NotFoundObjectResult>(secondCommit);
  }

  // ── Rollback ──

  [Fact]
  public void Rollback_DiscardsTransaction()
  {
    var controller = CreateController();
    var beginResult = (OkObjectResult)controller.BeginTransaction(DbName);
    var txId = ((CreateTransactionResponse)beginResult.Value!).TransactionId;

    var result = controller.Rollback(txId);

    var okResult = Assert.IsType<OkObjectResult>(result);
    Assert.NotNull(okResult.Value);
  }

  [Fact]
  public void Rollback_ReturnsNotFound_ForInvalidTxId()
  {
    var result = CreateController().Rollback(Guid.NewGuid());

    Assert.IsType<NotFoundObjectResult>(result);
  }

  [Fact]
  public async Task CommitAfterRollback_ReturnsNotFound()
  {
    var controller = CreateController();
    var beginResult = (OkObjectResult)controller.BeginTransaction(DbName);
    var txId = ((CreateTransactionResponse)beginResult.Value!).TransactionId;

    controller.Rollback(txId);
    var result = await controller.Commit(txId);

    // After rollback, the transaction is removed — Commit returns NotFound.
    Assert.IsType<NotFoundObjectResult>(result);
  }
}
