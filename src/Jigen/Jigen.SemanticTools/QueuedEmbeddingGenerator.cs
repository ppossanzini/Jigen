using System.Threading.Channels;

namespace Jigen.SemanticTools;

public sealed class QueuedEmbeddingGenerator : IEmbeddingGenerator, IDisposable
{
  private readonly IEmbeddingGenerator _inner;
  private readonly Channel<EmbeddingRequest> _queue;
  private readonly CancellationTokenSource _stoppingTokenSource = new();
  private readonly Task[] _workers;
  private readonly TimeSpan _enqueueTimeout;

  private volatile bool _disposed;

  public QueuedEmbeddingGenerator(
    IEmbeddingGenerator inner,
    int maxConcurrency,
    int queueCapacity,
    TimeSpan enqueueTimeout)
  {
    maxConcurrency = Math.Max(maxConcurrency, 1);
    queueCapacity = Math.Max(queueCapacity, 1);

    if (enqueueTimeout <= TimeSpan.Zero)
      throw new ArgumentOutOfRangeException(nameof(enqueueTimeout), "Enqueue timeout must be greater than zero.");

    _inner = inner;
    _enqueueTimeout = enqueueTimeout;

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

  public float[] GenerateEmbedding(string input)
  {
    if (_disposed)
      throw new ObjectDisposedException(nameof(QueuedEmbeddingGenerator), "Cannot generate embedding after the generator has been disposed.");

    if (string.IsNullOrWhiteSpace(input))
      throw new ArgumentException("Input text cannot be null or empty.", nameof(input));

    var completion = new TaskCompletionSource<float[]>(TaskCreationOptions.RunContinuationsAsynchronously);
    var request = new EmbeddingRequest(input, completion);

    using var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_stoppingTokenSource.Token);
    timeoutTokenSource.CancelAfter(_enqueueTimeout);

    try
    {
      _queue.Writer.WriteAsync(request, timeoutTokenSource.Token).AsTask().GetAwaiter().GetResult();
    }
    catch (OperationCanceledException) when (!_stoppingTokenSource.IsCancellationRequested)
    {
      throw new TimeoutException($"Embedding queue is full. Request enqueue timed out after {_enqueueTimeout.TotalSeconds:0} seconds.");
    }

    return completion.Task.GetAwaiter().GetResult();
  }

  public float[] GenerateEmbedding(string task, string input) =>
    GenerateEmbedding(!string.IsNullOrWhiteSpace(task) ? $"{task}: {input}" : input);

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
    try
    {
      while (true)
      {
        if (await _queue.Reader.WaitToReadAsync(_stoppingTokenSource.Token))
          if (_queue.Reader.TryRead(out var request))
            try
            {
              var result = _inner.GenerateEmbedding(request.Input);
              request.Completion.TrySetResult(result);
            }
            catch (Exception ex)
            {
              request.Completion.TrySetException(ex);
            }
      }
    }
    catch (OperationCanceledException)
    {
    }
  }

  private sealed record EmbeddingRequest(string Input, TaskCompletionSource<float[]> Completion);
}