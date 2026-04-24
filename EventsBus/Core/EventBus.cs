using EventBusLib.Core.Internal;
using EventBusLib.Dispatching;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace EventBusLib.Core;

public sealed class EventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, object> _registries = new();
    private readonly ConcurrentDictionary<Type, object> _requestRegistries = new();
    private readonly IDispatcherProvider? _dispatcher;

    public event Action<Exception>? OnHandlerError;

    public EventBus(IDispatcherProvider? dispatcher = null) => _dispatcher = dispatcher;

    public IDisposable Subscribe<T>(Action<T> handler, bool useWeakReference = true, bool marshalToUiThread = false) =>
        GetRegistry<T>().Subscribe(handler, marshalToUiThread, _dispatcher, OnHandlerError);

    public IDisposable SubscribeAsync<T>(Func<T, CancellationToken, Task> handler, bool useWeakReference = true, bool marshalToUiThread = false) =>
        GetRegistry<T>().SubscribeAsync(handler, marshalToUiThread, _dispatcher, OnHandlerError);

    public void Publish<T>(T payload) => GetRegistry<T>().Publish(payload, OnHandlerError);

    public Task PublishAsync<T>(T payload, CancellationToken cancellationToken = default) =>
        GetRegistry<T>().PublishAsync(payload, cancellationToken, OnHandlerError);

    public Task<TResponse> RequestAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default) =>
        GetRequestRegistry<TRequest, TResponse>().InvokeAsync(request, cancellationToken, OnHandlerError);

    public IDisposable RegisterRequestHandler<TRequest, TResponse>(Func<TRequest, CancellationToken, Task<TResponse>> handler) =>
        GetRequestRegistry<TRequest, TResponse>().Register(handler);

    public void UnsubscribeAll<T>() => GetRegistry<T>().UnsubscribeAll();

    public void Cleanup()
    {
        foreach (var registry in _registries.Values)
            ((IRegistryCleanup)registry).Cleanup();
    }

    private HandlerRegistry<T> GetRegistry<T>() =>
        (HandlerRegistry<T>)_registries.GetOrAdd(typeof(T), _ => new HandlerRegistry<T>());

    private RequestRegistry<TReq, TRes> GetRequestRegistry<TReq, TRes>() =>
        (RequestRegistry<TReq, TRes>)_requestRegistries.GetOrAdd(typeof(TReq), _ => new RequestRegistry<TReq, TRes>());
}