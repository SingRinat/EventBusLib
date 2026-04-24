using EventBusLib.Dispatching;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace EventBusLib.Core.Internal;

internal sealed class WeakSyncHandler<T>
{
    private readonly WeakReference<object?> _target;
    private readonly MethodInfo _method;
    private readonly bool _isStatic;
    private readonly IDispatcherProvider? _dispatcher;
    private readonly bool _marshalToUiThread;
    private readonly Action<Exception>? _onError;

    public WeakSyncHandler(Action<T> handler, bool marshalToUiThread, IDispatcherProvider? dispatcher, Action<Exception>? onError)
    {
        // ✅ Храним слабую ссылку на ОБЪЕКТ, а не на делегат
        _target = new WeakReference<object?>(handler.Target);
        _method = handler.Method;
        _isStatic = handler.Target == null;
        _marshalToUiThread = marshalToUiThread;
        _dispatcher = marshalToUiThread ? dispatcher : null;
        _onError = onError;
    }

    public bool IsAlive => _isStatic || _target.TryGetTarget(out _);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Invoke(T payload)
    {
        try
        {
            object? target = _isStatic ? null : null;
            if (!_isStatic && !_target.TryGetTarget(out target))
                return false; // Объект собран, подписка мертва

            // Восстанавливаем делегат "на лету" (занимает ~100-200нс, для UI-событий незаметно)
            var del = (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), target, _method);

            if (_marshalToUiThread && _dispatcher is not null && !_dispatcher.IsUiThread)
            {
                _ = SafeRunOnUiThread(() => del(payload));
                return true;
            }

            del(payload);
            return true;
        }
        catch (Exception ex)
        {
            _onError?.Invoke(ex);
            return false;
        }
    }

    private async Task SafeRunOnUiThread(Action action)
    {
        try
        {
            if (_dispatcher is not null)
                await _dispatcher.RunOnUiThreadAsync(() => { action(); return Task.CompletedTask; });
            else
                action();
        }
        catch (Exception ex) { _onError?.Invoke(ex); }
    }
}