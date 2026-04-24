using System;
using System.Threading;
using System.Threading.Tasks;

namespace EventBusLib.Core;


/// <summary>
/// Типобезопасная шина событий с поддержкой слабых ссылок, асинхронных операций и паттерна Request-Response.
/// </summary>
public interface IEventBus
{
    /// <summary>Подписка на событие. Возвращает IDisposable для отписки.</summary>
    /// <param name="handler">Делегат обработчика.</param>
    /// <param name="useWeakReference">Если true, ссылка на подписчика будет слабой (не предотвращает GC).</param>
    /// <param name="marshalToUiThread">Если true, вызов будет перенаправлен в UI-поток через DispatcherQueue.</param>
    IDisposable Subscribe<T>(Action<T> handler, bool useWeakReference = true, bool marshalToUiThread = false);

    /// <summary>Асинхронная подписка на событие.</summary>
    /// <param name="handler">Асинхронный делегат. CancellationToken передаётся автоматически.</param>
    IDisposable SubscribeAsync<T>(Func<T, CancellationToken, Task> handler, bool useWeakReference = true, bool marshalToUiThread = false);

    /// <summary>Синхронная публикация. Асинхронные обработчики запускаются в фоне (Fire-and-Forget).</summary>
    void Publish<T>(T payload);

    /// <summary>Асинхронная публикация. Ожидает завершения всех обработчиков.</summary>
    Task PublishAsync<T>(T payload, CancellationToken cancellationToken = default);

    /// <summary>Запрос-ответ. Вызывает единственный зарегистрированный обработчик.</summary>
    /// <exception cref="InvalidOperationException">Если обработчик не зарегистрирован или зарегистрировано более одного.</exception>
    Task<TResponse> RequestAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default);

    /// <summary>Регистрация обработчика для Request-Response.</summary>
    IDisposable RegisterRequestHandler<TRequest, TResponse>(Func<TRequest, CancellationToken, Task<TResponse>> handler);

    /// <summary>Принудительная отписка всех подписчиков указанного типа.</summary>
    void UnsubscribeAll<T>();

    /// <summary>Очистка списков от "мёртвых" слабых ссылок. Рекомендуется вызывать периодически.</summary>
    void Cleanup();

    /// <summary>Событие возникает при исключении в любом обработчике. Не прерывает выполнение остальных.</summary>
    event Action<Exception>? OnHandlerError;
}
