using System;
using System.Threading;
using System.Threading.Tasks;

namespace EventBusLib.Core.Internal;

internal sealed class RequestRegistry<TReq, TRes>
{
    private Func<TReq, CancellationToken, Task<TRes>>? _handler;
    private readonly ReaderWriterLockSlim _lock = new();

    public IDisposable Register(Func<TReq, CancellationToken, Task<TRes>> handler)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_handler is not null)
                throw new InvalidOperationException($"Handler for {typeof(TReq).Name} -> {typeof(TRes).Name} already registered.");

            _handler = handler;
            return new Unsubscriber(() =>
            {
                _lock.EnterWriteLock();
                try { _handler = null; }
                finally { _lock.ExitWriteLock(); }
            });
        }
        finally { _lock.ExitWriteLock(); }
    }

    public async Task<TRes> InvokeAsync(TReq request, CancellationToken ct, Action<Exception>? onError)
    {
        _lock.EnterReadLock();
        try
        {
            if (_handler is null)
                throw new InvalidOperationException($"No handler registered for {typeof(TReq).Name} -> {typeof(TRes).Name}");

            try
            {
                return await _handler(request, ct).WaitAsync(ct);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                throw;
            }
        }
        finally { _lock.ExitReadLock(); }
    }
}