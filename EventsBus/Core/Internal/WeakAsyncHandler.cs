using EventBusLib.Dispatching;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace EventBusLib.Core.Internal;


internal sealed class WeakAsyncHandler<T>
{
    private readonly WeakReference<Func<T, CancellationToken, Task>> _ref;
    private readonly IDispatcherProvider? _dispatcher;

    public WeakAsyncHandler(Func<T, CancellationToken, Task> handler, bool captureContext, IDispatcherProvider? dispatcher)
    {
        _ref = new WeakReference<Func<T, CancellationToken, Task>>(handler);
        _dispatcher = captureContext ? dispatcher : null;
    }

    public bool IsAlive => _ref.TryGetTarget(out _);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task InvokeAsync(T payload, CancellationToken ct, Action<Exception>? onError)
    {
        if (!_ref.TryGetTarget(out var handler)) return;

        try
        {
            if (_dispatcher is not null && !_dispatcher.IsUiThread)
                await _dispatcher.RunOnUiThreadAsync(() => handler(payload, ct), ct);
            else
                await handler(payload, ct).WaitAsync(ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            onError?.Invoke(ex);
        }
    }
}