using System;

namespace EventBusLib.Models;

/// <summary>
/// Базовый класс для запросов к шине (паттерн Request/Response)
/// </summary>
/// <typeparam name="TResponse">Тип ответа</typeparam>
public abstract record RequestBase<TResponse>
{
    /// <summary>
    /// Уникальный идентификатор запроса
    /// </summary>
    public Guid RequestId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Время создания запроса (UTC)
    /// </summary>
    public DateTime RequestedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Таймаут выполнения запроса в миллисекундах
    /// </summary>
    public int TimeoutMs { get; init; } = 30000;

    /// <summary>
    /// Создать запрос с таймаутом
    /// </summary>
    public RequestBase<TResponse> WithTimeout(int timeoutMs)
    {
        return this with { TimeoutMs = timeoutMs };
    }

    public override string ToString() => $"{GetType().Name} [{RequestId}]";
}