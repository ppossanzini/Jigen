using System.Text;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Newtonsoft.Json;

namespace Jigen.Grpc.Interceptors;

// Source - https://stackoverflow.com/a/67162282
// Posted by Guilherme Molin
// Retrieved 2026-02-20, License - CC BY-SA 4.0

public class GrpcServerExceptionInterceptor : Interceptor
{
  public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
  {
    try
    {
      return await base.UnaryServerHandler(request, context, continuation);
    }
    catch (Exception exp)
    {
      throw this.TreatException(exp);
    }
  }

  public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, ServerCallContext context, ClientStreamingServerMethod<TRequest, TResponse> continuation)
  {
    try
    {
      return await base.ClientStreamingServerHandler(requestStream, context, continuation);
    }
    catch (Exception exp)
    {
      throw this.TreatException(exp);
    }
  }

  public override async Task ServerStreamingServerHandler<TRequest, TResponse>(TRequest request, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, ServerStreamingServerMethod<TRequest, TResponse> continuation)
  {
    try
    {
      await base.ServerStreamingServerHandler(request, responseStream, context, continuation);
    }
    catch (Exception exp)
    {
      throw this.TreatException(exp);
    }
  }

  public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, DuplexStreamingServerMethod<TRequest, TResponse> continuation)
  {
    try
    {
      await base.DuplexStreamingServerHandler(requestStream, responseStream, context, continuation);
    }
    catch (Exception exp)
    {
      throw this.TreatException(exp);
    }
  }

  private RpcException TreatException(Exception exp)
  {
    // Convert exp to Json
    string exception = JsonConvert.SerializeObject(exp, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });

    // Convert Json to byte[]
    byte[] exceptionByteArray = Encoding.UTF8.GetBytes(exception);

    // Add Trailer with the exception as byte[]
    Metadata metadata = new Metadata
    {
      { "exception-bin", exceptionByteArray }
    };

    // New RpcException with original exception
    return new RpcException(new Status(StatusCode.Internal, "Error"), metadata);
  }
}