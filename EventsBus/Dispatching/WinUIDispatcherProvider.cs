using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;

namespace EventBusLib.Dispatching;

/// <summary>
/// Реализация IDispatcherProvider для WinUI 3 на базе DispatcherQueue.
/// </summary>
public sealed class WinUIDispatcherProvider : IDispatcherProvider
{
    private readonly DispatcherQueue _queue;

    /// <summary>Создаёт провайдер, привязанный к текущему потоку.</summary>
    public WinUIDispatcherProvider() : this(DispatcherQueue.GetForCurrentThread()) { }

    /// <summary>Создаёт провайдер с явным указанием очереди.</summary>
    public WinUIDispatcherProvider(DispatcherQueue queue) => _queue = queue;

    public bool IsUiThread => _queue.HasThreadAccess;

    public Task RunOnUiThreadAsync(Func<Task> action, CancellationToken ct = default)
    {
        if (_queue.HasThreadAccess) return action();

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool enqueued = _queue.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
        {
            try
            {
                await action().WaitAsync(ct);
                tcs.TrySetResult();
            }
            catch (OperationCanceledException) { tcs.TrySetCanceled(ct); }
            catch (Exception ex) { tcs.TrySetException(ex); }
        });

        if (!enqueued)
        {
            tcs.TrySetException(new InvalidOperationException("DispatcherQueue недоступен или уничтожен."));
        }

        return tcs.Task;
    }
}