using EventBusLib.Core;
using EventBusLib.Dispatching;
using Microsoft.Extensions.DependencyInjection;

namespace EventBusLib.DI;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует IEventBus и WinUIDispatcherProvider в контейнере зависимостей.
    /// </summary>
    /// <param name="dispatcher">Опционально. Если не указан, будет создан на основе текущего потока при вызове.</param>
    public static IServiceCollection AddEventBus(this IServiceCollection services, IDispatcherProvider? dispatcher = null)
    {
        services.AddSingleton<IDispatcherProvider>(sp => dispatcher ?? new WinUIDispatcherProvider());
        services.AddSingleton<IEventBus>(sp => new EventBus(sp.GetRequiredService<IDispatcherProvider>()));
        return services;
    }
}