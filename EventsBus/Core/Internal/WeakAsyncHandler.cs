using EventBusLib.Dispatching;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace EventBusLib.Core.Internal;


internal sealed class WeakAsyncHandler<T>
{
    private readonly WeakReference<object?> _target;
    private readonly MethodInfo _method;
    private readonly bool _isStatic;
    private readonly IDispatcherProvider? _dispatcher;
    private readonly bool _marshalToUiThread;
    private readonly Action<Exception>? _onError;

    public WeakAsyncHandler(Func<T, CancellationToken, Task> handler, bool marshalToUiThread, IDispatcherProvider? dispatcher, Action<Exception>? onError)
    {
        _target = new WeakReference<object?>(handler.Target);
        _method = handler.Method;
        _isStatic = handler.Target == null;
        _marshalToUiThread = marshalToUiThread;
        _dispatcher = marshalToUiThread ? dispatcher : null;
        _onError = onError;
    }

    public bool IsAlive => _isStatic || _target.TryGetTarget(out _);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task InvokeAsync(T payload, CancellationToken ct)
    {
        try
        {
            object? target = _isStatic ? null : null;
            if (!_isStatic && !_target.TryGetTarget(out target))
                return;

            var del = (Func<T, CancellationToken, Task>)Delegate.CreateDelegate(
                typeof(Func<T, CancellationToken, Task>), target, _method);

            if (_marshalToUiThread && _dispatcher is not null && !_dispatcher.IsUiThread)
            {
                await _dispatcher.RunOnUiThreadAsync(() => del(payload, ct), ct);
            }
            else
            {
                await del(payload, ct).WaitAsync(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _onError?.Invoke(ex);
        }
    }
}