using System;
using System.Threading;

namespace EventBusLib.Core.Internal;

internal sealed class Unsubscriber : IDisposable
{
    private Action? _onDispose;
    private int _disposed;

    public Unsubscriber(Action onDispose) => _onDispose = onDispose;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            Interlocked.Exchange(ref _onDispose, null)?.Invoke();
        }
    }
}

