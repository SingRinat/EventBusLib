using EventBusLib.Dispatching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EventBusLib.Core.Internal;

internal interface IRegistryCleanup { void Cleanup(); }

internal sealed class HandlerRegistry<T> : IRegistryCleanup
{
    private readonly List<WeakSyncHandler<T>> _syncHandlers = new();
    private readonly List<WeakAsyncHandler<T>> _asyncHandlers = new();
    private readonly object _lock = new();
    private int _operationCount;
    private const int CleanupThreshold = 50;

    public IDisposable Subscribe(Action<T> handler, bool marshalToUiThread, IDispatcherProvider? dispatcher, Action<Exception>? onError)
    {
        var wrapper = new WeakSyncHandler<T>(handler);
        var disposable = new Unsubscriber(() =>
        {
            lock (_lock) _syncHandlers.Remove(wrapper);
        });

        lock (_lock) _syncHandlers.Add(wrapper);
        return disposable;
    }

    public IDisposable SubscribeAsync(Func<T, CancellationToken, Task> handler, bool marshalToUiThread, IDispatcherProvider? dispatcher, Action<Exception>? onError)
    {
        var wrapper = new WeakAsyncHandler<T>(handler, marshalToUiThread, dispatcher);
        var disposable = new Unsubscriber(() =>
        {
            lock (_lock) _asyncHandlers.Remove(wrapper);
        });

        lock (_lock) _asyncHandlers.Add(wrapper);
        return disposable;
    }

    public void Publish(T payload, Action<Exception>? onError)
    {
        List<WeakSyncHandler<T>> syncSnapshot;
        List<WeakAsyncHandler<T>> asyncSnapshot;

        lock (_lock)
        {
            syncSnapshot = _syncHandlers.ToList();
            asyncSnapshot = _asyncHandlers.ToList();
            CompactIfNeeded();
        }

        foreach (var h in syncSnapshot) h.Invoke(payload, onError);

        // Fire-and-forget для асинхронных в синхронном Publish
        _ = Task.WhenAll(asyncSnapshot.Select(h => h.InvokeAsync(payload, CancellationToken.None, onError)));
    }

    public async Task PublishAsync(T payload, CancellationToken ct, Action<Exception>? onError)
    {
        List<WeakSyncHandler<T>> syncSnapshot;
        List<WeakAsyncHandler<T>> asyncSnapshot;

        lock (_lock)
        {
            syncSnapshot = _syncHandlers.ToList();
            asyncSnapshot = _asyncHandlers.ToList();
            CompactIfNeeded();
        }

        var tasks = new List<Task>(syncSnapshot.Count + asyncSnapshot.Count);

        // Синхронные заворачиваем в Task.Run чтобы не блокировать вызывающий поток
        foreach (var h in syncSnapshot)
            tasks.Add(Task.Run(() => h.Invoke(payload, onError), ct));

        foreach (var h in asyncSnapshot)
            tasks.Add(h.InvokeAsync(payload, ct, onError));

        await Task.WhenAll(tasks);
    }

    public void UnsubscribeAll()
    {
        lock (_lock)
        {
            _syncHandlers.Clear();
            _asyncHandlers.Clear();
        }
    }

    public void Cleanup()
    {
        lock (_lock) CompactIfNeeded();
    }

    private void CompactIfNeeded()
    {
        _operationCount++;
        if (_operationCount < CleanupThreshold) return;

        _operationCount = 0;
        _syncHandlers.RemoveAll(h => !h.IsAlive);
        _asyncHandlers.RemoveAll(h => !h.IsAlive);
    }
}