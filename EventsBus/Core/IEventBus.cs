using EventBusLib.Diagnostics;
using EventBusLib.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EventBusLib.Core;


/// <summary>
/// Основной интерфейс шины событий
/// </summary>
public interface IEventBus : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Подписаться на событие с синхронным обработчиком
    /// </summary>
    /// <example>
    /// <code>
    /// eventBus.Subscribe<UserLoggedInEvent>(evt => Console.WriteLine($"User {evt.UserName} logged in"));
    /// </code>
    /// </example>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler, int priority = 0) where TEvent : EventBase;

    /// <summary>
    /// Подписаться на событие с асинхронным обработчиком
    /// </summary>
    /// <example>
    /// <code>
    /// eventBus.SubscribeAsync<UserLoggedInEvent>(async (evt, ct) => 
    /// {
    ///     await _emailService.SendWelcomeEmailAsync(evt.UserId, ct);
    /// });
    /// </code>
    /// </example>
    IDisposable SubscribeAsync<TEvent>(Func<TEvent, CancellationToken, Task> handler, int priority = 0) where TEvent : EventBase;

    /// <summary>
    /// Подписаться на событие с фильтром
    /// </summary>
    /// <example>
    /// <code>
    /// eventBus.Subscribe<DataChangedEvent>(OnDataChanged, 
    ///     filter: e => e.EntityType == "User" && e.UserId == currentUserId);
    /// </code>
    /// </example>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler, Func<TEvent, bool> filter, int priority = 0) where TEvent : EventBase;

    /// <summary>
    /// Подписаться на событие с асинхронным обработчиком и фильтром
    /// </summary>
    IDisposable SubscribeAsync<TEvent>(Func<TEvent, CancellationToken, Task> handler, Func<TEvent, bool> filter, int priority = 0) where TEvent : EventBase;

    /// <summary>
    /// Опубликовать событие
    /// </summary>
    /// <example>
    /// <code>
    /// await eventBus.PublishAsync(new UserLoggedInEvent { UserId = 123, UserName = "john" });
    /// </code>
    /// </example>
    Task<bool> PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default, bool ensureDelivery = false) where TEvent : EventBase;

    /// <summary>
    /// Выполнить запрос и получить ответ (паттерн Mediator)
    /// </summary>
    /// <example>
    /// <code>
    /// var userData = await eventBus.QueryAsync<GetUserDataQuery, UserData>(
    ///     new GetUserDataQuery { UserId = 123 });
    /// </code>
    /// </example>
    Task<TResponse> QueryAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : RequestBase<TResponse>;

    /// <summary>
    /// Опубликовать несколько событий пакетно (оптимизировано для больших объемов)
    /// </summary>
    Task PublishBatchAsync<TEvent>(IEnumerable<TEvent> events, CancellationToken cancellationToken = default) where TEvent : EventBase;

    /// <summary>
    /// Зарегистрировать обработчик для запросов
    /// </summary>
    /// <example>
    /// <code>
    /// eventBus.RegisterRequestHandler<GetUserDataQuery, UserData>(async (query, ct) =>
    /// {
    ///     return await _db.Users.FindAsync(query.UserId, ct);
    /// });
    /// </code>
    /// </example>
    void RegisterRequestHandler<TRequest, TResponse>(Func<TRequest, CancellationToken, Task<TResponse>> handler, bool overwrite = false)
        where TRequest : RequestBase<TResponse>;

    /// <summary>
    /// Отписать все подписки объекта
    /// </summary>
    void UnsubscribeAll(object subscriber);

    /// <summary>
    /// Дождаться завершения всех активных операций публикации
    /// </summary>
    Task WaitForIdleAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить количество активных подписок
    /// </summary>
    Task<int> GetActiveSubscriptionCountAsync();

    /// <summary>
    /// Событие обновления метрик
    /// </summary>
    event EventHandler<EventBusMetrics> MetricsUpdated;
}
