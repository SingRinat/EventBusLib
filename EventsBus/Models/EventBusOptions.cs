using System;

namespace EventBusLib.Models;

/// <summary>
/// Настройки шины событий
/// </summary>
public class EventBusOptions
{
    /// <summary>
    /// Максимальное количество одновременно выполняемых обработчиков
    /// </summary>
    public int MaxConcurrentHandlers { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Таймаут выполнения обработчика в миллисекундах
    /// </summary>
    public int HandlerTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Включить сбор метрик
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Включить диагностическую информацию
    /// </summary>
    public bool EnableDiagnostics { get; set; } = false;

    /// <summary>
    /// Максимальное количество подписок на одно событие
    /// </summary>
    public int MaxSubscriptionPerEvent { get; set; } = 1000;

    /// <summary>
    /// Максимальный размер пула объектов
    /// </summary>
    public int ObjectPoolMaxSize { get; set; } = 1024;

    /// <summary>
    /// Бросать исключение при ошибке в обработчике
    /// </summary>
    public bool ThrowOnHandlerException { get; set; } = false;

    /// <summary>
    /// Интервал очистки мертвых подписок (мс), 0 - отключено
    /// </summary>
    public int CleanupIntervalMs { get; set; } = 60000;

    /// <summary>
    /// Логировать предупреждения о медленных обработчиках (мс)
    /// </summary>
    public int SlowHandlerWarningThresholdMs { get; set; } = 1000;

    public override string ToString()
    {
        return $"[MaxConcurrent={MaxConcurrentHandlers}, Timeout={HandlerTimeoutMs}ms, Cleanup={CleanupIntervalMs}ms]";
    }
}