using System.Diagnostics;
using Windows.System;

namespace EventBusLib.Dispatching;

public sealed class WinUIDispatcherProvider : IDispatcherProvider
{
    private readonly DispatcherQueue? _queue;
    private readonly SynchronizationContext? _fallbackContext;
    private readonly string? _debugTag;

    /// <summary>
    /// Создаёт провайдер. Если очередь недоступна, использует SynchronizationContext как резерв.
    /// </summary>
    /// <param name="debugTag">Опциональная метка для отладки (например, "MainPage")</param>
    public WinUIDispatcherProvider(string? debugTag = null)
    {
        _debugTag = debugTag;
        _queue = TryGetDispatcherQueue();

        // Fallback: если DispatcherQueue недоступен, пробуем захватить текущий контекст
        // Это работает, если код выполняется в потоке с установленным SynchronizationContext (WinUI обычно его ставит)
        _fallbackContext = _queue == null ? SynchronizationContext.Current : null;

        Debug.WriteLine($"[WinUIDispatcherProvider] Created: Queue={_queue != null}, Context={_fallbackContext != null}, Tag={_debugTag}");
    }

    private static DispatcherQueue? TryGetDispatcherQueue()
    {
        try
        {
            var queue = DispatcherQueue.GetForCurrentThread();
            return queue?.HasThreadAccess == true ? queue : null;
        }
        catch
        {
            return null;
        }
    }

    public bool IsUiThread => _queue?.HasThreadAccess == true || _fallbackContext == SynchronizationContext.Current;

    public async Task RunOnUiThreadAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        // ✅ Оптимизация: если уже в целевом потоке — выполняем напрямую
        if (IsUiThread)
        {
            await action().WaitAsync(cancellationToken);
            return;
        }

        // 🔹 Попытка 1: DispatcherQueue (нативный способ WinUI 3)
        if (_queue is not null && _queue.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
        {
            try
            {
                await action().WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Нормальная отмена — не логируем как ошибку
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WinUIDispatcherProvider {_debugTag}] Handler exception: {ex}");
                // Не пробрасываем дальше, чтобы не ронуть очередь
            }
        }))
        {
            return; // Задача будет выполнена в фоне; если нужно ожидание — см. вариант с TCS ниже
        }

        // 🔹 Попытка 2: SynchronizationContext (резервный путь)
        if (_fallbackContext is not null)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _fallbackContext.Post(async _ =>
            {
                try
                {
                    await action().WaitAsync(cancellationToken);
                    tcs.TrySetResult();
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    tcs.TrySetCanceled(cancellationToken);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }, null);

            await tcs.Task.WaitAsync(cancellationToken);
            return;
        }

        // ❌ Оба пути провалились — выбрасываем понятное исключение
        throw new InvalidOperationException(
            $"[{_debugTag}] Не удалось выполнить в UI-потоке: DispatcherQueue недоступен и SynchronizationContext не захвачен. " +
            $"Убедитесь, что подписка происходит после инициализации окна (OnLaunched/OnNavigatedTo).");
    }
}