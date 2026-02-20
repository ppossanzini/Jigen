using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Newtonsoft.Json;

namespace Jigen.Client.Interceptors;

// Source - https://stackoverflow.com/a/67162282
// Posted by Guilherme Molin
// Retrieved 2026-02-20, License - CC BY-SA 4.0

public class GrpcClientExceptionInterceptor : Interceptor
{
  public override TResponse BlockingUnaryCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
  {
    try
    {
      return base.BlockingUnaryCall(request, context, continuation);
    }
    catch (RpcException exp)
    {
      TreatException(exp);
      throw;
    }
  }

  public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
  {
    AsyncUnaryCall<TResponse> chamada = continuation(request, context);
    return new AsyncUnaryCall<TResponse>(
      this.TreatResponseUnique(chamada.ResponseAsync),
      chamada.ResponseHeadersAsync,
      chamada.GetStatus,
      chamada.GetTrailers,
      chamada.Dispose);
  }

  public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(ClientInterceptorContext<TRequest, TResponse> context, AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
  {
    AsyncClientStreamingCall<TRequest, TResponse> chamada = continuation(context);
    return new AsyncClientStreamingCall<TRequest, TResponse>(
      chamada.RequestStream,
      this.TreatResponseUnique(chamada.ResponseAsync),
      chamada.ResponseHeadersAsync,
      chamada.GetStatus,
      chamada.GetTrailers,
      chamada.Dispose);
  }

  public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
  {
    AsyncServerStreamingCall<TResponse> chamada = continuation(request, context);
    return new AsyncServerStreamingCall<TResponse>(
      new TreatResponseStream<TResponse>(chamada.ResponseStream),
      chamada.ResponseHeadersAsync,
      chamada.GetStatus,
      chamada.GetTrailers,
      chamada.Dispose);
  }

  public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(ClientInterceptorContext<TRequest, TResponse> context, AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
  {
    AsyncDuplexStreamingCall<TRequest, TResponse> chamada = continuation(context);
    return new AsyncDuplexStreamingCall<TRequest, TResponse>(
      chamada.RequestStream,
      new TreatResponseStream<TResponse>(chamada.ResponseStream),
      chamada.ResponseHeadersAsync,
      chamada.GetStatus,
      chamada.GetTrailers,
      chamada.Dispose);
  }

  internal static void TreatException(RpcException exp)
  {
    // Check if there's a trailer that we defined in the server
    if (!exp.Trailers.Any(x => x.Key.Equals("exception-bin")))
    {
      return;
    }
    

    // Convert exception from byte[] to  string
    string exceptionString = Encoding.UTF8.GetString(exp.Trailers.GetValueBytes("exception-bin"));

    // Convert string to exception
    Exception exception = JsonConvert.DeserializeObject<Exception>(exceptionString, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });

    // Required to keep the original stacktrace (https://stackoverflow.com/questions/66707139/how-to-throw-a-deserialized-exception)
    exception.GetType().GetField("_remoteStackTraceString", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(exception, exception.StackTrace);

    // Throw the original exception
    ExceptionDispatchInfo.Capture(exception).Throw();
  }

  private async Task<TResponse> TreatResponseUnique<TResponse>(Task<TResponse> resposta)
  {
    try
    {
      return await resposta;
    }
    catch (RpcException exp)
    {
      TreatException(exp);
      throw;
    }
  }
}

public class TreatResponseStream<TResponse> : IAsyncStreamReader<TResponse>
{
  private readonly IAsyncStreamReader<TResponse> stream;

  public TreatResponseStream(IAsyncStreamReader<TResponse> stream)
  {
    this.stream = stream;
  }

  public TResponse Current => this.stream.Current;

  public async Task<bool> MoveNext(CancellationToken cancellationToken)
  {
    try
    {
      return await this.stream.MoveNext(cancellationToken).ConfigureAwait(false);
    }
    catch (RpcException exp)
    {
      GrpcClientExceptionInterceptor.TreatException(exp);
      throw;
    }
  }
}