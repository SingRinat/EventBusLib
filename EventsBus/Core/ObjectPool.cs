using System;
using System.Collections.Concurrent;
using System.Threading;

namespace EventsBus.Core;

internal sealed class ObjectPool<T> : IDisposable where T : class
{
    private readonly ConcurrentBag<T> _objects;
    private readonly Func<T> _generator;
    private readonly Action<T>? _resetAction;
    private readonly int _maxSize;
    private int _currentSize;
    private int _disposed;

    public ObjectPool(Func<T> generator, Action<T>? resetAction = null, int maxSize = 1024)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _resetAction = resetAction;
        _maxSize = maxSize;
        _objects = [];
    }

    public T Get()
    {
        if (Volatile.Read(ref _disposed) == 1)
            throw new ObjectDisposedException(nameof(ObjectPool<T>));

        if (_objects.TryTake(out var item))
        {
            Interlocked.Decrement(ref _currentSize);
            return item;
        }

        return _generator();
    }

    public void Return(T item)
    {
        if (Volatile.Read(ref _disposed) == 1 || item == null)
            return;

        if (Interlocked.Increment(ref _currentSize) <= _maxSize)
        {
            _resetAction?.Invoke(item);
            _objects.Add(item);
        }
        else
        {
            Interlocked.Decrement(ref _currentSize);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        while (_objects.TryTake(out _)) { }
        _currentSize = 0;
    }
}