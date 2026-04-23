using EventBusLib.Core;
using EventBusLib.Diagnostics;
using EventBusLib.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace EventBusLib.Extensions;

/// <summary>
/// Методы расширения для регистрации EventBus в DI контейнере
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Добавить EventBus как синглтон (глобальная шина для всего приложения)
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddEventBus(options =>
    /// {
    ///     options.MaxConcurrentHandlers = 8;
    ///     options.HandlerTimeoutMs = 10000;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddEventBus(
        this IServiceCollection services,
        Action<EventBusOptions>? configureOptions = null)
    {
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<EventBusOptions>(options => { });
        }

        services.AddSingleton<IEventBus, EventBus>();
        services.AddSingleton<EventBus>();

        // Добавляем health check
        services.AddHealthChecks()
            .AddCheck<EventBusHealthCheck>("event_bus");

        return services;
    }

    /// <summary>
    /// Добавить EventBus как scoped (отдельная шина для каждого scope)
    /// </summary>
    /// <remarks>Полезно для тестов или изолированных модулей</remarks>
    public static IServiceCollection AddScopedEventBus(
        this IServiceCollection services,
        Action<EventBusOptions>? configureOptions = null)
    {
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        services.AddScoped<IEventBus, EventBus>();
        services.AddScoped<EventBus>();

        return services;
    }

    /// <summary>
    /// Добавить EventBus как transient (новая шина при каждом запросе)
    /// </summary>
    public static IServiceCollection AddTransientEventBus(
        this IServiceCollection services,
        Action<EventBusOptions>? configureOptions = null)
    {
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        services.AddTransient<IEventBus, EventBus>();
        services.AddTransient<EventBus>();

        return services;
    }
}
