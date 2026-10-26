using System.Threading.Channels;

namespace Jigen.SemanticTools;

public sealed class QueuedEmbeddingGenerator : IEmbeddingGenerator, IDisposable
{
  private readonly IEmbeddingGenerator _inner;
  private readonly OnnxEmbeddingGenerator _learnedSparse;
  private readonly Channel<QueuedWork> _queue;
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
    // A lexical sparse vector needs no inference and keeps bypassing the queue;
    // a learned one runs the model, so it must respect the concurrency limit.
    _learnedSparse = inner is OnnxEmbeddingGenerator { HasLearnedSparseHead: true } onnx ? onnx : null;
    _enqueueTimeout = enqueueTimeout;
    _maxBatchSize = Math.Max(maxBatchSize, 1);

    _queue = Channel.CreateBounded<QueuedWork>(new BoundedChannelOptions(queueCapacity)
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

  public int CountTokens(string input) => _inner.CountTokens(input);

  public SparseEmbedding GenerateSparseEmbedding(string input) =>
    _learnedSparse == null
      ? _inner.GenerateSparseEmbedding(input)
      : GenerateSparseEmbeddingAsync(input).GetAwaiter().GetResult();

  public async Task<SparseEmbedding> GenerateSparseEmbeddingAsync(string input, CancellationToken cancellationToken = default)
  {
    if (_learnedSparse == null)
      return await _inner.GenerateSparseEmbeddingAsync(input, cancellationToken).ConfigureAwait(false);

    if (string.IsNullOrWhiteSpace(input))
      return SparseEmbedding.Empty;

    return await EnqueueAsync(new QueuedWork<SparseEmbedding>(
      () => _learnedSparse.GenerateSparseEmbedding(input), cancellationToken), cancellationToken).ConfigureAwait(false);
  }

  public async Task<(float[] Dense, SparseEmbedding Sparse)> GenerateEmbeddingWithSparseAsync(string input,
    CancellationToken cancellationToken = default)
  {
    if (_learnedSparse != null)
    {
      ValidateInput(input);
      return await EnqueueAsync(new QueuedWork<(float[], SparseEmbedding)>(
        () => _learnedSparse.GenerateEmbeddingWithSparse(input), cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    var dense = GenerateEmbeddingAsync(input, cancellationToken);
    var sparse = _inner.GenerateSparseEmbedding(input);
    return (await dense.ConfigureAwait(false), sparse);
  }

  public async Task<float[]> GenerateEmbeddingAsync(string input, CancellationToken cancellationToken = default)
  {
    ValidateInput(input);
    return await EnqueueAsync(new DenseWork(input, _inner, cancellationToken), cancellationToken).ConfigureAwait(false);
  }

  public Task<float[]> GenerateEmbeddingAsync(string task, string input, CancellationToken cancellationToken = default) =>
    GenerateEmbeddingAsync(!string.IsNullOrWhiteSpace(task) ? $"{task}: {input}" : input, cancellationToken);

  public async Task<float[][]> GenerateEmbeddingsAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(inputs);

    var tasks = new Task<float[]>[inputs.Count];
    for (var i = 0; i < inputs.Count; i++)
      tasks[i] = GenerateEmbeddingAsync(inputs[i], cancellationToken);

    return await Task.WhenAll(tasks).ConfigureAwait(false);
  }

  private void ValidateInput(string input)
  {
    if (_disposed)
      throw new ObjectDisposedException(nameof(QueuedEmbeddingGenerator), "Cannot generate embedding after the generator has been disposed.");

    if (string.IsNullOrWhiteSpace(input))
      throw new ArgumentException("Input text cannot be null or empty.", nameof(input));
  }

  private async Task<T> EnqueueAsync<T>(QueuedWork<T> work, CancellationToken cancellationToken)
  {
    if (_disposed)
      throw new ObjectDisposedException(nameof(QueuedEmbeddingGenerator), "Cannot generate embedding after the generator has been disposed.");

    using (var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_stoppingTokenSource.Token, cancellationToken))
    {
      timeoutTokenSource.CancelAfter(_enqueueTimeout);

      try
      {
        await _queue.Writer.WriteAsync(work, timeoutTokenSource.Token).ConfigureAwait(false);
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
      static state => ((QueuedWork)state).Cancel(),
      work);

    return await work.Completion.Task.ConfigureAwait(false);
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
    var batch = new List<QueuedWork>(_maxBatchSize);

    try
    {
      while (await _queue.Reader.WaitToReadAsync(_stoppingTokenSource.Token))
      {
        // Coalescing: drain the requests already queued up to MaxBatchSize;
        // the dense ones are fused into a single batched inference run.
        batch.Clear();
        while (batch.Count < _maxBatchSize && _queue.Reader.TryRead(out var request))
        {
          if (request.CancellationToken.IsCancellationRequested)
            request.Cancel();
          else
            batch.Add(request);
        }

        if (batch.Count == 0)
          continue;

        var dense = batch.OfType<DenseWork>().ToArray();
        if (dense.Length > 1)
          RunDenseBatch(dense);

        foreach (var work in batch)
          if (dense.Length <= 1 || work is not DenseWork)
            work.Run();
      }
    }
    catch (OperationCanceledException)
    {
      foreach (var request in batch)
        request.Cancel();
    }
  }

  private void RunDenseBatch(DenseWork[] dense)
  {
    try
    {
      var inputs = new string[dense.Length];
      for (var i = 0; i < dense.Length; i++)
        inputs[i] = dense[i].Input;

      var results = _inner.GenerateEmbeddings(inputs);
      for (var i = 0; i < dense.Length; i++)
        dense[i].Completion.TrySetResult(results[i]);
    }
    catch (Exception ex)
    {
      foreach (var request in dense)
        request.Completion.TrySetException(ex);
    }
  }

  private abstract class QueuedWork(CancellationToken cancellationToken)
  {
    public CancellationToken CancellationToken { get; } = cancellationToken;
    public abstract void Run();
    public abstract void Cancel();
  }

  private class QueuedWork<T>(Func<T> run, CancellationToken cancellationToken) : QueuedWork(cancellationToken)
  {
    public TaskCompletionSource<T> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override void Run()
    {
      try
      {
        Completion.TrySetResult(run());
      }
      catch (Exception ex)
      {
        Completion.TrySetException(ex);
      }
    }

    public override void Cancel() => Completion.TrySetCanceled(CancellationToken);
  }

  private sealed class DenseWork(string input, IEmbeddingGenerator inner, CancellationToken cancellationToken)
    : QueuedWork<float[]>(() => inner.GenerateEmbedding(input), cancellationToken)
  {
    public string Input { get; } = input;
  }
}
