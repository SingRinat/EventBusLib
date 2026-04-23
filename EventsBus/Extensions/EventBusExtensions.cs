using EventBusLib.Core;
using EventBusLib.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EventBusLib.Extensions;

/// <summary>
/// Полезные методы расширения для работы с EventBus
/// </summary>
public static class EventBusExtensions
{
    /// <summary>
    /// Подписка с автоматической отпиской при уничтожении объекта
    /// </summary>
    public static IDisposable SubscribeSafe<TEvent>(
        this IEventBus eventBus,
        object subscriber,
        Action<TEvent> handler,
        int priority = 0) where TEvent : EventBase
    {
        var subscription = eventBus.Subscribe(handler, priority);

        if (subscriber is IDisposable disposable)
        {
            // Если объект поддерживает IDisposable, отпишемся автоматически
            var linkedDisposable = new LinkedDisposable(subscription, disposable);
            return linkedDisposable;
        }

        return subscription;
    }

    /// <summary>
    /// Публикация события с ожиданием завершения
    /// </summary>
    public static async Task PublishAndWaitAsync<TEvent>(
        this IEventBus eventBus,
        TEvent @event,
        CancellationToken cancellationToken = default) where TEvent : EventBase
    {
        await eventBus.PublishAsync(@event, cancellationToken);
        await eventBus.WaitForIdleAsync(cancellationToken);
    }

    /// <summary>
    /// Попытка публикации с повторными попытками
    /// </summary>
    public static async Task<bool> PublishWithRetryAsync<TEvent>(
        this IEventBus eventBus,
        TEvent @event,
        int maxRetries = 3,
        int retryDelayMs = 100,
        CancellationToken cancellationToken = default) where TEvent : EventBase
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return await eventBus.PublishAsync(@event, cancellationToken, true);
            }
            catch when (i < maxRetries - 1)
            {
                await Task.Delay(retryDelayMs * (i + 1), cancellationToken);
            }
        }

        return false;
    }

    private class LinkedDisposable : IDisposable
    {
        private readonly IDisposable _subscription;
        private readonly IDisposable _owner;
        private bool _disposed;

        public LinkedDisposable(IDisposable subscription, IDisposable owner)
        {
            _subscription = subscription;
            _owner = owner;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _subscription.Dispose();
                _owner.Dispose();
                _disposed = true;
            }
        }
    }
}