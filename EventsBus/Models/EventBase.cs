using System;
using System.Collections.Generic;

namespace EventBusLib.Models;

/// <summary>
/// Базовый класс для всех событий в шине
/// </summary>
public abstract record EventBase
{
    /// <summary>
    /// Уникальный идентификатор события
    /// </summary>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Время возникновения события (UTC)
    /// </summary>
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Идентификатор для корреляции связанных событий
    /// </summary>
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Дополнительные метаданные события
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>
    /// Добавить метаданные
    /// </summary>
    public EventBase WithMetadata(string key, string value)
    {
        Metadata[key] = value;
        return this;
    }

    public override string ToString() => $"{GetType().Name} [{EventId}] at {OccurredAt:HH:mm:ss.fff}";
}