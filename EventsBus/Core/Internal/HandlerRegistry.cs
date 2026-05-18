using EventBusLib.Dispatching;
using System;
using System.Collections.Generic;
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
        var wrapper = new WeakSyncHandler<T>(handler, marshalToUiThread, dispatcher, onError);
        var disposable = new Unsubscriber(() => { lock (_lock) _syncHandlers.Remove(wrapper); });
        
        lock (_lock)
        {
            _syncHandlers.Add(wrapper);
            CompactIfNeededLocked();
        }
        
        return disposable;
    }

    public IDisposable SubscribeAsync(Func<T, CancellationToken, Task> handler, bool marshalToUiThread, IDispatcherProvider? dispatcher, Action<Exception>? onError)
    {
        var wrapper = new WeakAsyncHandler<T>(handler, marshalToUiThread, dispatcher, onError);
        var disposable = new Unsubscriber(() => { lock (_lock) _asyncHandlers.Remove(wrapper); });
        
        lock (_lock)
        {
            _asyncHandlers.Add(wrapper);
            CompactIfNeededLocked();
        }
        
        return disposable;
    }

    public void Publish(T payload, Action<Exception>? onError)
    {
        WeakSyncHandler<T>[] syncSnapshot;
        WeakAsyncHandler<T>[] asyncSnapshot;

        lock (_lock)
        {
            syncSnapshot = _syncHandlers.ToArray();
            asyncSnapshot = _asyncHandlers.ToArray();
        }

        // Синхронные вызовы
        foreach (var h in syncSnapshot)
        {
            try
            {
                h.Invoke(payload);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
            }
        }

        // Асинхронные вызовы (fire-and-forget с полной изоляцией исключений)
        if (asyncSnapshot.Length > 0)
        {
            _ = Task.Run(async () =>
            {
                foreach (var h in asyncSnapshot)
                {
                    try
                    {
                        await h.InvokeAsync(payload, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        onError?.Invoke(ex);
                    }
                }
            });
        }
    }

    public async Task PublishAsync(T payload, CancellationToken ct, Action<Exception>? onError)
    {
        WeakSyncHandler<T>[] syncSnapshot;
        WeakAsyncHandler<T>[] asyncSnapshot;

        lock (_lock)
        {
            syncSnapshot = _syncHandlers.ToArray();
            asyncSnapshot = _asyncHandlers.ToArray();
        }

        var tasks = new List<Task>(syncSnapshot.Length + asyncSnapshot.Length);

        // Синхронные обработчики запускаем в пуле потоков
        foreach (var h in syncSnapshot)
        {
            try
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        h.Invoke(payload);
                    }
                    catch (Exception ex)
                    {
                        onError?.Invoke(ex);
                    }
                }, ct));
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
            }
        }

        // Асинхронные обработчики
        foreach (var h in asyncSnapshot)
        {
            try
            {
                tasks.Add(h.InvokeAsync(payload, ct));
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
            }
        }

        if (tasks.Count > 0)
        {
            try
            {
                await Task.WhenAll(tasks).WaitAsync(ct);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                onError?.Invoke(ex);
            }
        }
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
        lock (_lock) CompactIfNeededLocked();
    }

    private void CompactIfNeededLocked()
    {
        // Предполагается, что вызывается из блока lock
        _operationCount++;
        if (_operationCount < CleanupThreshold) return;

        _operationCount = 0;
        _syncHandlers.RemoveAll(h => !h.IsAlive);
        _asyncHandlers.RemoveAll(h => !h.IsAlive);
    }
}