using System;
using System.Runtime.CompilerServices;

namespace EventBusLib.Core.Internal;

internal sealed class WeakSyncHandler<T>
{
    private readonly WeakReference<Action<T>> _ref;

    public WeakSyncHandler(Action<T> handler) => _ref = new WeakReference<Action<T>>(handler);
    public bool IsAlive => _ref.TryGetTarget(out _);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Invoke(T payload, Action<Exception>? onError)
    {
        if (!_ref.TryGetTarget(out var handler)) return false;

        try
        {
            handler(payload);
            return true;
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex);
            return false;
        }
    }
}