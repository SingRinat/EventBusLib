using EventBusLib.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EventBusLib.Core;

internal abstract class RequestHandlerWrapper { }

internal sealed class RequestHandlerWrapper<TRequest, TResponse> : RequestHandlerWrapper
    where TRequest : RequestBase<TResponse>
{
    public Func<TRequest, CancellationToken, Task<TResponse>> Handler { get; }

    public RequestHandlerWrapper(Func<TRequest, CancellationToken, Task<TResponse>> handler)
    {
        Handler = handler;
    }
}