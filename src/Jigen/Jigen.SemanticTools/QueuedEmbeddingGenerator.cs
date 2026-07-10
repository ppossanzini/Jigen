using System.Threading.Channels;

namespace Jigen.SemanticTools;

public sealed class QueuedEmbeddingGenerator : IEmbeddingGenerator, IDisposable
{
  private readonly IEmbeddingGenerator _inner;
  private readonly Channel<EmbeddingRequest> _queue;
  private readonly CancellationTokenSource _stoppingTokenSource = new();
  private readonly Task[] _workers;
  private readonly TimeSpan _enqueueTimeout;
  private readonly int _maxBatchSize;

  private volatile bool _disposed;

  public QueuedEmbeddingGenerator(
    IEmbeddingGenerator inner,
    int maxConcurrency,
    int queueCapacity,
    TimeSpan enqueueTimeout,
    int maxBatchSize = 1)
  {
    maxConcurrency = Math.Max(maxConcurrency, 1);
    queueCapacity = Math.Max(queueCapacity, 1);

    if (enqueueTimeout <= TimeSpan.Zero)
      throw new ArgumentOutOfRangeException(nameof(enqueueTimeout), "Enqueue timeout must be greater than zero.");

    _inner = inner;
    _enqueueTimeout = enqueueTimeout;
    _maxBatchSize = Math.Max(maxBatchSize, 1);

    _queue = Channel.CreateBounded<EmbeddingRequest>(new BoundedChannelOptions(queueCapacity)
    {
      FullMode = BoundedChannelFullMode.Wait,
      SingleReader = false,
      SingleWriter = false
    });

    _workers = Enumerable.Range(0, maxConcurrency)
      .Select(_ => Task.Run(ProcessQueueAsync))
      .ToArray();
  }

  public float[] GenerateEmbedding(string input) =>
    GenerateEmbeddingAsync(input).GetAwaiter().GetResult();

  public float[] GenerateEmbedding(string task, string input) =>
    GenerateEmbedding(!string.IsNullOrWhiteSpace(task) ? $"{task}: {input}" : input);

  public float[][] GenerateEmbeddings(IReadOnlyList<string> inputs) =>
    GenerateEmbeddingsAsync(inputs).GetAwaiter().GetResult();

  public Task<float[]> GenerateEmbeddingAsync(string input, CancellationToken cancellationToken = default) =>
    EnqueueAsync(input, cancellationToken);

  public Task<float[]> GenerateEmbeddingAsync(string task, string input, CancellationToken cancellationToken = default) =>
    GenerateEmbeddingAsync(!string.IsNullOrWhiteSpace(task) ? $"{task}: {input}" : input, cancellationToken);

  public async Task<float[][]> GenerateEmbeddingsAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(inputs);

    var tasks = new Task<float[]>[inputs.Count];
    for (var i = 0; i < inputs.Count; i++)
      tasks[i] = EnqueueAsync(inputs[i], cancellationToken);

    return await Task.WhenAll(tasks).ConfigureAwait(false);
  }

  private async Task<float[]> EnqueueAsync(string input, CancellationToken cancellationToken)
  {
    if (_disposed)
      throw new ObjectDisposedException(nameof(QueuedEmbeddingGenerator), "Cannot generate embedding after the generator has been disposed.");

    if (string.IsNullOrWhiteSpace(input))
      throw new ArgumentException("Input text cannot be null or empty.", nameof(input));

    var completion = new TaskCompletionSource<float[]>(TaskCreationOptions.RunContinuationsAsynchronously);
    var request = new EmbeddingRequest(input, completion, cancellationToken);

    using (var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_stoppingTokenSource.Token, cancellationToken))
    {
      timeoutTokenSource.CancelAfter(_enqueueTimeout);

      try
      {
        await _queue.Writer.WriteAsync(request, timeoutTokenSource.Token).ConfigureAwait(false);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        throw new OperationCanceledException("Embedding request was cancelled.", cancellationToken);
      }
      catch (OperationCanceledException) when (!_stoppingTokenSource.IsCancellationRequested)
      {
        throw new TimeoutException($"Embedding queue is full. Request enqueue timed out after {_enqueueTimeout.TotalSeconds:0} seconds.");
      }
    }

    // Unblock the caller immediately on cancellation; the worker discards
    // already-cancelled requests before running inference on them.
    using var cancellationRegistration = cancellationToken.Register(
      static state => ((TaskCompletionSource<float[]>)state).TrySetCanceled(),
      completion);

    return await completion.Task.ConfigureAwait(false);
  }

  public void Dispose()
  {
    _disposed = true;
    _queue.Writer.TryComplete();
    _stoppingTokenSource.Cancel();

    try
    {
      Task.WaitAll(_workers);
    }
    catch
    {
      // Ignore worker cancellation/faults during shutdown.
    }

    _stoppingTokenSource.Dispose();

    if (_inner is IDisposable disposable)
      disposable.Dispose();
  }

  private async Task ProcessQueueAsync()
  {
    var batch = new List<EmbeddingRequest>(_maxBatchSize);

    try
    {
      while (await _queue.Reader.WaitToReadAsync(_stoppingTokenSource.Token))
      {
        // Coalescing: drain the requests already queued up to MaxBatchSize
        // and fuse them into a single batched inference run.
        batch.Clear();
        while (batch.Count < _maxBatchSize && _queue.Reader.TryRead(out var request))
        {
          if (request.CancellationToken.IsCancellationRequested)
            request.Completion.TrySetCanceled(request.CancellationToken);
          else
            batch.Add(request);
        }

        if (batch.Count == 0)
          continue;

        try
        {
          if (batch.Count == 1)
          {
            batch[0].Completion.TrySetResult(_inner.GenerateEmbedding(batch[0].Input));
          }
          else
          {
            var inputs = new string[batch.Count];
            for (var i = 0; i < batch.Count; i++)
              inputs[i] = batch[i].Input;

            var results = _inner.GenerateEmbeddings(inputs);
            for (var i = 0; i < batch.Count; i++)
              batch[i].Completion.TrySetResult(results[i]);
          }
        }
        catch (Exception ex)
        {
          foreach (var request in batch)
            request.Completion.TrySetException(ex);
        }
      }
    }
    catch (OperationCanceledException)
    {
      foreach (var request in batch)
        request.Completion.TrySetCanceled();
    }
  }

  private sealed record EmbeddingRequest(
    string Input,
    TaskCompletionSource<float[]> Completion,
    CancellationToken CancellationToken);
}