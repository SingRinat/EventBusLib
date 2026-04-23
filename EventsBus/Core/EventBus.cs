using EventBusLib.Diagnostics;
using EventBusLib.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EventBusLib.Core;

/// <summary>
/// Реализация шины событий с поддержкой синхронных/асинхронных подписок и слабых ссылок
/// </summary>
public sealed class EventBus : IEventBus, IAsyncDisposable, IDisposable
{
    private readonly ILogger<EventBus> _logger;
    private readonly EventBusOptions _options;
    private readonly ConcurrentDictionary<Type, PriorityQueue<SubscriptionEntry, int>> _subscriptions;
    private readonly ConcurrentDictionary<Type, RequestHandlerWrapper> _requestHandlers;
    private readonly SemaphoreSlim _subscriptionLock;
    private readonly SemaphoreSlim _publishLock;
    private readonly Timer? _cleanupTimer;
    private readonly ObjectPool<List<ISubscription>> _subscriptionListPool;
    private readonly CancellationTokenSource _disposalCts;

    private long _totalEventsProcessed;
    private long _totalFailedEvents;
    private long _activePublishOperations;
    private int _isDisposed;
    private long _totalSubscriptions;
    private long _successfulHandlers;
    private long _failedHandlers;

    public event EventHandler<EventBusMetrics>? MetricsUpdated;

    public EventBus(ILogger<EventBus> logger, IOptions<EventBusOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        _subscriptions = new ConcurrentDictionary<Type, PriorityQueue<SubscriptionEntry, int>>();
        _requestHandlers = new ConcurrentDictionary<Type, RequestHandlerWrapper>();
        _subscriptionLock = new SemaphoreSlim(1, 1);
        _publishLock = new SemaphoreSlim(_options.MaxConcurrentHandlers, _options.MaxConcurrentHandlers);
        _disposalCts = new CancellationTokenSource();

        _subscriptionListPool = new ObjectPool<List<ISubscription>>(
            () => new List<ISubscription>(_options.MaxSubscriptionPerEvent),
            list => list.Clear(),
            _options.ObjectPoolMaxSize);

        if (_options.CleanupIntervalMs > 0)
        {
            _cleanupTimer = new Timer(CleanupDeadSubscriptions, null,
                _options.CleanupIntervalMs, _options.CleanupIntervalMs);
        }

        _logger.LogInformation("EventBus initialized with options: {Options}", _options);
    }

    /// <inheritdoc/>
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler, int priority = 0) where TEvent : EventBase
    {
        ArgumentNullException.ThrowIfNull(handler);
        return SubscribeInternal<TEvent>(new SyncSubscription<TEvent>(handler), priority);
    }

    /// <inheritdoc/>
    public IDisposable SubscribeAsync<TEvent>(Func<TEvent, CancellationToken, Task> handler, int priority = 0) where TEvent : EventBase
    {
        ArgumentNullException.ThrowIfNull(handler);
        return SubscribeInternal<TEvent>(new AsyncSubscription<TEvent>(handler), priority);
    }

    /// <inheritdoc/>
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler, Func<TEvent, bool> filter, int priority = 0) where TEvent : EventBase
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(filter);
        return SubscribeInternal<TEvent>(new FilteredSyncSubscription<TEvent>(handler, filter), priority);
    }

    /// <inheritdoc/>
    public IDisposable SubscribeAsync<TEvent>(Func<TEvent, CancellationToken, Task> handler, Func<TEvent, bool> filter, int priority = 0) where TEvent : EventBase
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(filter);
        return SubscribeInternal<TEvent>(new FilteredAsyncSubscription<TEvent>(handler, filter), priority);
    }

    private IDisposable SubscribeInternal<TEvent>(ISubscription subscription, int priority) where TEvent : EventBase
    {
        var eventType = typeof(TEvent);

        _subscriptionLock.Wait(_disposalCts.Token);
        try
        {
            var queue = _subscriptions.GetOrAdd(eventType, _ => new PriorityQueue<SubscriptionEntry, int>());
            var entry = new SubscriptionEntry(subscription, priority);
            queue.Enqueue(entry, -priority);
            Interlocked.Increment(ref _totalSubscriptions);

            _logger.LogDebug("Subscribed to {EventType} with priority {Priority}", eventType.Name, priority);
            return new SubscriptionToken(this, eventType, entry);
        }
        finally
        {
            _subscriptionLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default, bool ensureDelivery = false) where TEvent : EventBase
    {
        if (Interlocked.CompareExchange(ref _isDisposed, 0, 0) != 0)
            throw new ObjectDisposedException(nameof(EventBus));

        ArgumentNullException.ThrowIfNull(@event);

        var eventType = typeof(TEvent);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _publishLock.WaitAsync(cancellationToken);
            Interlocked.Increment(ref _activePublishOperations);

            var subscriptions = GetActiveSubscriptions(eventType);
            if (subscriptions.Count == 0)
            {
                _logger.LogDebug("No subscribers for event {EventType}", eventType.Name);
                return true;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposalCts.Token);
            cts.CancelAfter(_options.HandlerTimeoutMs);

            var tasks = new List<Task>(subscriptions.Count);
            var exceptions = new ConcurrentBag<Exception>();
            var slowHandlers = new ConcurrentBag<string>();

            foreach (var subscription in subscriptions)
            {
                if (cts.Token.IsCancellationRequested)
                    break;

                var handlerStopwatch = Stopwatch.StartNew();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await subscription.HandleAsync(@event, cts.Token);
                        handlerStopwatch.Stop();
                        Interlocked.Increment(ref _successfulHandlers);

                        if (handlerStopwatch.ElapsedMilliseconds > _options.SlowHandlerWarningThresholdMs)
                        {
                            slowHandlers.Add($"{subscription.GetType().Name}: {handlerStopwatch.ElapsedMilliseconds}ms");
                        }
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref _failedHandlers);
                        exceptions.Add(ex);
                        _logger.LogError(ex, "Error in handler for event {EventType}", eventType.Name);

                        if (_options.ThrowOnHandlerException)
                            throw;
                    }
                }, cts.Token));
            }

            await Task.WhenAll(tasks);

            if (slowHandlers.Any())
            {
                _logger.LogWarning("Slow handlers detected for event {EventType}: {Handlers}",
                    eventType.Name, string.Join(", ", slowHandlers));
            }

            if (!exceptions.IsEmpty && _options.ThrowOnHandlerException)
                throw new AggregateException("One or more event handlers failed", exceptions);

            if (exceptions.Count > 0)
            {
                Interlocked.Increment(ref _totalFailedEvents);
                if (ensureDelivery)
                {
                    _logger.LogWarning("Event {EventId} partially failed. {SuccessCount}/{TotalCount} handlers succeeded",
                        @event.EventId, subscriptions.Count - exceptions.Count, subscriptions.Count);
                    return false;
                }
            }
            else
            {
                Interlocked.Increment(ref _totalEventsProcessed);
            }

            stopwatch.Stop();
            UpdateMetrics(stopwatch.ElapsedMilliseconds, subscriptions.Count);

            _logger.LogDebug("Published event {EventType} in {ElapsedMs}ms to {HandlerCount} handlers",
                eventType.Name, stopwatch.ElapsedMilliseconds, subscriptions.Count);

            return exceptions.IsEmpty;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Publishing event {EventType} was cancelled", eventType.Name);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventType}", eventType.Name);
            Interlocked.Increment(ref _totalFailedEvents);
            throw;
        }
        finally
        {
            Interlocked.Decrement(ref _activePublishOperations);
            _publishLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<TResponse> QueryAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : RequestBase<TResponse>
    {
        if (Interlocked.CompareExchange(ref _isDisposed, 0, 0) != 0)
            throw new ObjectDisposedException(nameof(EventBus));

        ArgumentNullException.ThrowIfNull(request);

        var requestType = typeof(TRequest);

        if (!_requestHandlers.TryGetValue(requestType, out var handlerWrapper))
        {
            throw new InvalidOperationException($"No handler registered for request type {requestType.Name}");
        }

        var typedHandler = handlerWrapper as RequestHandlerWrapper<TRequest, TResponse>;
        if (typedHandler == null)
        {
            throw new InvalidOperationException($"Invalid handler type for request {requestType.Name}");
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposalCts.Token);
        cts.CancelAfter(request.TimeoutMs);

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await typedHandler.Handler(request, cts.Token);
            stopwatch.Stop();

            _logger.LogDebug("Query {RequestType} completed in {ElapsedMs}ms",
                requestType.Name, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Query {RequestType} was cancelled", requestType.Name);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Query {RequestType} timed out after {Timeout}ms",
                requestType.Name, request.TimeoutMs);
            throw new TimeoutException($"Query {requestType.Name} timed out after {request.TimeoutMs}ms");
        }
    }

    /// <inheritdoc/>
    public async Task PublishBatchAsync<TEvent>(IEnumerable<TEvent> events, CancellationToken cancellationToken = default) where TEvent : EventBase
    {
        ArgumentNullException.ThrowIfNull(events);

        var eventList = events.ToList();
        if (eventList.Count == 0) return;

        _logger.LogInformation("Publishing batch of {Count} events of type {EventType}",
            eventList.Count, typeof(TEvent).Name);

        var results = await Task.WhenAll(eventList.Select(e =>
            PublishAsync(e, cancellationToken, false)));

        var failedCount = results.Count(r => !r);
        if (failedCount > 0)
        {
            _logger.LogWarning("Batch publishing completed with {FailedCount}/{TotalCount} failures",
                failedCount, eventList.Count);
        }
    }

    /// <inheritdoc/>
    public void RegisterRequestHandler<TRequest, TResponse>(
        Func<TRequest, CancellationToken, Task<TResponse>> handler, bool overwrite = false)
        where TRequest : RequestBase<TResponse>
    {
        ArgumentNullException.ThrowIfNull(handler);

        var wrapper = new RequestHandlerWrapper<TRequest, TResponse>(handler);

        if (!overwrite && _requestHandlers.ContainsKey(typeof(TRequest)))
        {
            throw new InvalidOperationException($"Handler already registered for {typeof(TRequest).Name}");
        }

        _requestHandlers[typeof(TRequest)] = wrapper;
        _logger.LogDebug("Registered request handler for {RequestType}", typeof(TRequest).Name);
    }

    /// <inheritdoc/>
    public void UnsubscribeAll(object subscriber)
    {
        if (subscriber == null) return;

        _subscriptionLock.Wait();
        try
        {
            var removedCount = 0;
            foreach (var kvp in _subscriptions)
            {
                var newQueue = new PriorityQueue<SubscriptionEntry, int>();
                while (kvp.Value.TryDequeue(out var entry, out var priority))
                {
                    if (entry.Subscription.TryGetTarget(out var subscription) &&
                        subscription.Owner != subscriber)
                    {
                        newQueue.Enqueue(entry, priority);
                    }
                    else
                    {
                        removedCount++;
                    }
                }

                while (newQueue.TryDequeue(out var entry, out var priority))
                {
                    kvp.Value.Enqueue(entry, priority);
                }
            }

            if (removedCount > 0)
            {
                Interlocked.Add(ref _totalSubscriptions, -removedCount);
                _logger.LogDebug("Unsubscribed {Count} handlers for subscriber {Subscriber}",
                    removedCount, subscriber.GetType().Name);
            }
        }
        finally
        {
            _subscriptionLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task WaitForIdleAsync(CancellationToken cancellationToken = default)
    {
        while (Interlocked.Read(ref _activePublishOperations) > 0)
        {
            await Task.Delay(10, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public Task<int> GetActiveSubscriptionCountAsync()
    {
        return Task.FromResult((int)Interlocked.Read(ref _totalSubscriptions));
    }

    private List<ISubscription> GetActiveSubscriptions(Type eventType)
    {
        var activeSubscriptions = _subscriptionListPool.Get();

        if (_subscriptions.TryGetValue(eventType, out var queue))
        {
            var tempList = new List<(SubscriptionEntry Entry, int Priority)>();

            while (queue.TryDequeue(out var entry, out var priority))
            {
                tempList.Add((entry, priority));
            }

            foreach (var (entry, priority) in tempList)
            {
                if (entry.Subscription.TryGetTarget(out var subscription))
                {
                    activeSubscriptions.Add(subscription);
                    queue.Enqueue(entry, priority);
                }
                else
                {
                    Interlocked.Decrement(ref _totalSubscriptions);
                }
            }
        }

        return activeSubscriptions;
    }

    private void CleanupDeadSubscriptions(object? state)
    {
        try
        {
            var cleanupCount = 0;
            foreach (var kvp in _subscriptions)
            {
                var tempList = new List<(SubscriptionEntry Entry, int Priority)>();

                while (kvp.Value.TryDequeue(out var entry, out var priority))
                {
                    if (entry.Subscription.TryGetTarget(out _))
                    {
                        tempList.Add((entry, priority));
                    }
                    else
                    {
                        cleanupCount++;
                    }
                }

                foreach (var (entry, priority) in tempList)
                {
                    kvp.Value.Enqueue(entry, priority);
                }
            }

            if (cleanupCount > 0)
            {
                Interlocked.Add(ref _totalSubscriptions, -cleanupCount);
                _logger.LogDebug("Cleaned up {Count} dead subscriptions", cleanupCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during subscription cleanup");
        }
    }

    private void UpdateMetrics(long processingTimeMs, int handlerCount)
    {
        if (!_options.EnableMetrics) return;

        var metrics = new EventBusMetrics
        {
            TotalEventsProcessed = Interlocked.Read(ref _totalEventsProcessed),
            TotalFailedEvents = Interlocked.Read(ref _totalFailedEvents),
            ActiveSubscriptions = (int)Interlocked.Read(ref _totalSubscriptions),
            ActivePublishOperations = Interlocked.Read(ref _activePublishOperations),
            LastProcessingTimeMs = processingTimeMs,
            HandlersExecuted = handlerCount,
            SuccessfulHandlers = Interlocked.Read(ref _successfulHandlers),
            FailedHandlers = Interlocked.Read(ref _failedHandlers),
            Timestamp = DateTime.UtcNow
        };

        MetricsUpdated?.Invoke(this, metrics);
    }

    private void RemoveSubscription(Type eventType, SubscriptionEntry entry)
    {
        if (_subscriptions.TryGetValue(eventType, out var queue))
        {
            _subscriptionLock.Wait();
            try
            {
                var tempList = new List<(SubscriptionEntry Entry, int Priority)>();
                while (queue.TryDequeue(out var e, out var priority))
                {
                    if (e != entry)
                    {
                        tempList.Add((e, priority));
                    }
                }

                foreach (var (e, priority) in tempList)
                {
                    queue.Enqueue(e, priority);
                }

                Interlocked.Decrement(ref _totalSubscriptions);
            }
            finally
            {
                _subscriptionLock.Release();
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _disposalCts.Cancel();
        await WaitForIdleAsync(CancellationToken.None);

        _cleanupTimer?.Dispose();
        _subscriptionLock?.Dispose();
        _publishLock?.Dispose();
        _disposalCts?.Dispose();
        _subscriptionListPool?.Dispose();

        _subscriptions.Clear();
        _requestHandlers.Clear();

        _logger.LogInformation("EventBus disposed. Total events processed: {TotalEvents}",
            Interlocked.Read(ref _totalEventsProcessed));

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        DisposeAsync().AsTask().Wait();
    }

    private class SubscriptionEntry
    {
        public WeakReference<ISubscription> Subscription { get; }
        public int Priority { get; }

        public SubscriptionEntry(ISubscription subscription, int priority)
        {
            Subscription = new WeakReference<ISubscription>(subscription);
            Priority = priority;
        }
    }

    private class SubscriptionToken : IDisposable
    {
        private readonly EventBus _eventBus;
        private readonly Type _eventType;
        private readonly SubscriptionEntry _entry;
        private bool _disposed;

        public SubscriptionToken(EventBus eventBus, Type eventType, SubscriptionEntry entry)
        {
            _eventBus = eventBus;
            _eventType = eventType;
            _entry = entry;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _eventBus.RemoveSubscription(_eventType, _entry);
                _disposed = true;
            }
        }
    }
}