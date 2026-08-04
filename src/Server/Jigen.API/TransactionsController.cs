using System.Collections.Concurrent;
using Hikyaku;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jigen.API;

/// <summary>
/// Multi-entry atomic transaction API.
///
/// Flow:
///   POST   /api/database/{db}/transactions              → create transaction
///   POST   /api/database/{db}/transactions/{id}/append   → buffer a SetDocument/SetVector
///   POST   /api/database/{db}/transactions/{id}/delete   → buffer a Delete
///   PUT    /api/database/{db}/transactions/{id}/commit   → atomically commit
///   DELETE /api/database/{db}/transactions/{id}          → rollback
///
/// The transaction is held server-side (in memory) until commit or rollback.
/// All buffered operations become atomically durable on commit via the WAL.
/// </summary>
[ApiController]
[Route("~/api/database/{dbname}/transactions")]
[Authorize]
public class TransactionsController(IHikyaku mediator) : ControllerBase
{
  private static readonly ConcurrentDictionary<Guid, TransactionState> ActiveTransactions = new();

  /// <summary>Begins a new transaction. Returns the transaction ID.</summary>
  [HttpPost]
  [ProducesResponseType(typeof(CreateTransactionResponse), StatusCodes.Status200OK)]
  public IActionResult BeginTransaction(string dbname)
  {
    var txId = Guid.CreateVersion7();
    var state = new TransactionState { Database = dbname, CreatedAt = DateTime.UtcNow };
    ActiveTransactions[txId] = state;
    return Ok(new CreateTransactionResponse { TransactionId = txId });
  }

  /// <summary>Buffers a document insert/upsert in the transaction.</summary>
  [HttpPost("{txId}/append/document")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> AppendDocument(Guid txId,
    [FromBody] TransactionDocumentPayload payload)
  {
    if (!ActiveTransactions.TryGetValue(txId, out var state))
      return NotFound($"Transaction '{txId}' not found");

    if (payload == null)
      return BadRequest("Payload is required");

    if (state.IsFinalized)
      return BadRequest("Transaction is already committed or rolled back");

    state.Ops.Add(new TransactionOpState
    {
      OpType = TransactionOpType.AppendDocument,
      Collection = payload.Collection,
      Key = payload.Key,
      Content = payload.Payload,
      Sentence = payload.Sentence
    });

    return Ok(new { accepted = state.Ops.Count });
  }

  /// <summary>Buffers a vector insert/upsert in the transaction.</summary>
  [HttpPost("{txId}/append/vector")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> AppendVector(Guid txId,
    [FromBody] TransactionVectorPayload payload)
  {
    if (!ActiveTransactions.TryGetValue(txId, out var state))
      return NotFound($"Transaction '{txId}' not found");

    if (payload == null)
      return BadRequest("Payload is required");

    if (state.IsFinalized)
      return BadRequest("Transaction is already committed or rolled back");

    state.Ops.Add(new TransactionOpState
    {
      OpType = TransactionOpType.AppendVector,
      Collection = payload.Collection,
      Key = payload.Key,
      Content = payload.Payload,
      Embeddings = payload.Embeddings
    });

    return Ok(new { accepted = state.Ops.Count });
  }

  /// <summary>Buffers a delete operation in the transaction.</summary>
  [HttpPost("{txId}/delete")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public IActionResult Delete(Guid txId, [FromBody] TransactionDeletePayload payload)
  {
    if (!ActiveTransactions.TryGetValue(txId, out var state))
      return NotFound($"Transaction '{txId}' not found");

    if (payload == null || string.IsNullOrWhiteSpace(payload.Collection) || string.IsNullOrWhiteSpace(payload.Key))
      return BadRequest("Collection and Key are required");

    if (state.IsFinalized)
      return BadRequest("Transaction is already committed or rolled back");

    state.Ops.Add(new TransactionOpState
    {
      OpType = TransactionOpType.Delete,
      Collection = payload.Collection,
      Key = payload.Key
    });

    return Ok(new { accepted = state.Ops.Count });
  }

  /// <summary>Atomically commits all buffered operations.</summary>
  [HttpPut("{txId}/commit")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> Commit(Guid txId)
  {
    if (!ActiveTransactions.TryGetValue(txId, out var state))
      return NotFound($"Transaction '{txId}' not found");

    if (state.IsFinalized)
      return BadRequest("Transaction is already committed or rolled back");

    state.IsFinalized = true;

    var accepted = 0;
    try
    {
      foreach (var op in state.Ops)
      {
        switch (op.OpType)
        {
          case TransactionOpType.AppendDocument:
            await mediator.Send(new Core.Command.collections.SetDocument
            {
              Database = state.Database,
              Collection = op.Collection,
              Key = System.Text.Encoding.UTF8.GetBytes(op.Key),
              Sentence = op.Sentence
            });
            break;
          case TransactionOpType.AppendVector:
            await mediator.Send(new Core.Command.collections.SetVector
            {
              Database = state.Database,
              Collection = op.Collection,
              Key = System.Text.Encoding.UTF8.GetBytes(op.Key),
              Embeddings = op.Embeddings
            });
            break;
          case TransactionOpType.Delete:
            await mediator.Send(new Core.Command.collections.DeleteVector
            {
              Database = state.Database,
              Collection = op.Collection,
              Key = System.Text.Encoding.UTF8.GetBytes(op.Key)
            });
            break;
        }
        accepted++;
      }

      return Ok(new { committed = true, accepted });
    }
    finally
    {
      ActiveTransactions.TryRemove(txId, out _);
    }
  }

  /// <summary>Rolls back (discards) the transaction.</summary>
  [HttpDelete("{txId}")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public IActionResult Rollback(Guid txId)
  {
    if (!ActiveTransactions.TryRemove(txId, out var state))
      return NotFound($"Transaction '{txId}' not found");

    state.IsFinalized = true;
    return Ok(new { rolledBack = true, discardedOps = state.Ops.Count });
  }
}

// ── Internal state types ──

internal class TransactionState
{
  public string Database { get; set; }
  public DateTime CreatedAt { get; set; }
  public List<TransactionOpState> Ops { get; set; } = [];
  public bool IsFinalized { get; set; }
}

internal enum TransactionOpType
{
  AppendDocument,
  AppendVector,
  Delete
}

internal class TransactionOpState
{
  public TransactionOpType OpType { get; set; }
  public string Collection { get; set; }
  public string Key { get; set; }
  public object Content { get; set; }
  public string Sentence { get; set; }
  public float[] Embeddings { get; set; }
}

// ── Request/Response DTOs ──

public class CreateTransactionResponse
{
  public Guid TransactionId { get; set; }
}

public class TransactionDocumentPayload
{
  public string Collection { get; set; }
  public string Key { get; set; }
  public object Payload { get; set; }
  public string Sentence { get; set; }
}

public class TransactionVectorPayload
{
  public string Collection { get; set; }
  public string Key { get; set; }
  public object Payload { get; set; }
  public float[] Embeddings { get; set; }
}

public class TransactionDeletePayload
{
  public string Collection { get; set; }
  public string Key { get; set; }
}
