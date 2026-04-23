using System;

namespace EventsBus.Diagnostics;

/// <summary>
/// Метрики производительности шины событий
/// </summary>
public record EventBusMetrics
{
    /// <summary>Всего обработано событий</summary>
    public long TotalEventsProcessed { get; init; }

    /// <summary>Всего ошибок при обработке</summary>
    public long TotalFailedEvents { get; init; }

    /// <summary>Активных подписок</summary>
    public int ActiveSubscriptions { get; init; }

    /// <summary>Активных операций публикации</summary>
    public long ActivePublishOperations { get; init; }

    /// <summary>Время последней обработки (мс)</summary>
    public long LastProcessingTimeMs { get; init; }

    /// <summary>Количество выполненных обработчиков</summary>
    public int HandlersExecuted { get; init; }

    /// <summary>Успешных обработчиков</summary>
    public long SuccessfulHandlers { get; init; }

    /// <summary>Ошибочных обработчиков</summary>
    public long FailedHandlers { get; init; }

    /// <summary>Временная метка</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>Процент успешных операций</summary>
    public double SuccessRate => TotalEventsProcessed > 0
        ? (double)(TotalEventsProcessed - TotalFailedEvents) / TotalEventsProcessed * 100
        : 100;

    /// <summary>Среднее количество обработчиков на событие</summary>
    public double AverageHandlersPerEvent => TotalEventsProcessed > 0
        ? (double)SuccessfulHandlers / TotalEventsProcessed
        : 0;

    public override string ToString()
    {
        return $"Events: {TotalEventsProcessed}, Failed: {TotalFailedEvents}, " +
               $"Success: {SuccessRate:F1}%, Subs: {ActiveSubscriptions}, " +
               $"Handlers: {SuccessfulHandlers}/{FailedHandlers}";
    }
}