using System.Threading.Channels;

namespace Jigen.SemanticTools;

/// <summary>
/// Bounded, coalescing queue wrapper for <see cref="IImageEmbeddingGenerator"/>,
/// mirroring <see cref="QueuedEmbeddingGenerator"/> on the text side: a bounded
/// request queue, a fixed number of worker tasks draining it, and fused batched
/// inference runs of up to <paramref name="maxBatchSize"/> already-queued
/// requests before handing the results back to their callers.
/// </summary>
public sealed class QueuedImageEmbeddingGenerator : IImageEmbeddingGenerator, IDisposable
{
  private readonly IImageEmbeddingGenerator _inner;
  private readonly Channel<ImageEmbeddingRequest> _queue;
  private readonly CancellationTokenSource _stoppingTokenSource = new();
  private readonly Task[] _workers;
  private readonly TimeSpan _enqueueTimeout;
  private readonly int _maxBatchSize;

  private volatile bool _disposed;

  public QueuedImageEmbeddingGenerator(
    IImageEmbeddingGenerator inner,
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

    _queue = Channel.CreateBounded<ImageEmbeddingRequest>(new BoundedChannelOptions(queueCapacity)
    {
      FullMode = BoundedChannelFullMode.Wait,
      SingleReader = false,
      SingleWriter = false
    });

    _workers = Enumerable.Range(0, maxConcurrency)
      .Select(_ => Task.Run(ProcessQueueAsync))
      .ToArray();
  }

  public float[] GenerateImageEmbedding(string imagePath) =>
    GenerateImageEmbeddingAsync(imagePath).GetAwaiter().GetResult();

  public float[] GenerateImageEmbedding(byte[] imageBytes) =>
    GenerateImageEmbeddingAsync(imageBytes).GetAwaiter().GetResult();

  public float[][] GenerateImageEmbeddings(IReadOnlyList<byte[]> images) =>
    GenerateImageEmbeddingsAsync(images).GetAwaiter().GetResult();

  public float[][] GenerateImageTileEmbeddings(byte[] imageBytes) =>
    GenerateImageTileEmbeddingsAsync(imageBytes).GetAwaiter().GetResult();

  public Task<float[]> GenerateImageEmbeddingAsync(string imagePath, CancellationToken cancellationToken = default) =>
    EnqueueAsync(File.ReadAllBytes(imagePath), cancellationToken);

  public Task<float[]> GenerateImageEmbeddingAsync(byte[] imageBytes, CancellationToken cancellationToken = default) =>
    EnqueueAsync(imageBytes, cancellationToken);

  public async Task<float[][]> GenerateImageEmbeddingsAsync(IReadOnlyList<byte[]> images, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(images);

    var tasks = new Task<float[]>[images.Count];
    for (var i = 0; i < images.Count; i++)
      tasks[i] = EnqueueAsync(images[i], cancellationToken);

    return await Task.WhenAll(tasks).ConfigureAwait(false);
  }

  public Task<float[][]> GenerateImageTileEmbeddingsAsync(byte[] imageBytes, CancellationToken cancellationToken = default) =>
    EnqueueTilesAsync(imageBytes, cancellationToken);

  private async Task<float[][]> EnqueueTilesAsync(byte[] imageBytes, CancellationToken cancellationToken)
  {
    if (_disposed)
      throw new ObjectDisposedException(nameof(QueuedImageEmbeddingGenerator), "Cannot generate embedding after the generator has been disposed.");

    if (imageBytes is null || imageBytes.Length == 0)
      throw new ArgumentException("Image data cannot be null or empty.", nameof(imageBytes));

    var completion = new TaskCompletionSource<float[][]>(TaskCreationOptions.RunContinuationsAsynchronously);
    var request = new ImageEmbeddingRequest(imageBytes, null, completion, cancellationToken);

    using (var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_stoppingTokenSource.Token, cancellationToken))
    {
      timeoutTokenSource.CancelAfter(_enqueueTimeout);

      try
      {
        await _queue.Writer.WriteAsync(request, timeoutTokenSource.Token).ConfigureAwait(false);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        throw new OperationCanceledException("Image embedding request was cancelled.", cancellationToken);
      }
      catch (OperationCanceledException) when (!_stoppingTokenSource.IsCancellationRequested)
      {
        throw new TimeoutException($"Embedding queue is full. Request enqueue timed out after {_enqueueTimeout.TotalSeconds:0} seconds.");
      }
    }

    using var cancellationRegistration = cancellationToken.Register(
      static state => ((TaskCompletionSource<float[][]>)state).TrySetCanceled(),
      completion);

    return await completion.Task.ConfigureAwait(false);
  }

  private async Task<float[]> EnqueueAsync(byte[] imageBytes, CancellationToken cancellationToken)
  {
    if (_disposed)
      throw new ObjectDisposedException(nameof(QueuedImageEmbeddingGenerator), "Cannot generate embedding after the generator has been disposed.");

    if (imageBytes is null || imageBytes.Length == 0)
      throw new ArgumentException("Image data cannot be null or empty.", nameof(imageBytes));

    var completion = new TaskCompletionSource<float[]>(TaskCreationOptions.RunContinuationsAsynchronously);
    var request = new ImageEmbeddingRequest(imageBytes, completion, null, cancellationToken);

    using (var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_stoppingTokenSource.Token, cancellationToken))
    {
      timeoutTokenSource.CancelAfter(_enqueueTimeout);

      try
      {
        await _queue.Writer.WriteAsync(request, timeoutTokenSource.Token).ConfigureAwait(false);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        throw new OperationCanceledException("Image embedding request was cancelled.", cancellationToken);
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
    var batch = new List<ImageEmbeddingRequest>(_maxBatchSize);

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
          {
            if (request.TileCompletion is not null)
              request.TileCompletion.TrySetCanceled(request.CancellationToken);
            else
              request.Completion.TrySetCanceled(request.CancellationToken);
          }
          else
            batch.Add(request);
        }

        if (batch.Count == 0)
          continue;

        try
        {
          // Tile requests are already internal mini-batches: run each one
          // individually. Plain requests are fused into a single batched run.
          var plain = new List<ImageEmbeddingRequest>(batch.Count);
          foreach (var request in batch)
          {
            if (request.TileCompletion is not null)
              request.TileCompletion.TrySetResult(_inner.GenerateImageTileEmbeddings(request.Image));
            else
              plain.Add(request);
          }

          if (plain.Count == 1)
          {
            plain[0].Completion.TrySetResult(_inner.GenerateImageEmbedding(plain[0].Image));
          }
          else if (plain.Count > 1)
          {
            var images = new byte[plain.Count][];
            for (var i = 0; i < plain.Count; i++)
              images[i] = plain[i].Image;

            var results = _inner.GenerateImageEmbeddings(images);
            for (var i = 0; i < plain.Count; i++)
              plain[i].Completion.TrySetResult(results[i]);
          }
        }
        catch (Exception ex)
        {
          foreach (var request in batch)
          {
            if (request.TileCompletion is not null)
              request.TileCompletion.TrySetException(ex);
            else
              request.Completion.TrySetException(ex);
          }
        }
      }
    }
    catch (OperationCanceledException)
    {
      foreach (var request in batch)
      {
        if (request.TileCompletion is not null)
          request.TileCompletion.TrySetCanceled();
        else
          request.Completion.TrySetCanceled();
      }
    }
  }

  private sealed record ImageEmbeddingRequest(
    byte[] Image,
    TaskCompletionSource<float[]> Completion,
    TaskCompletionSource<float[][]> TileCompletion,
    CancellationToken CancellationToken);
}
