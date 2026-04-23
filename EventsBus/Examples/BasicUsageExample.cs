using EventsBus.Core;
using EventsBus.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventsBus.Examples;

/// <summary>
/// Простой пример использования EventBus
/// </summary>
public class BasicUsageExample
{
    public static async Task RunAsync()
    {
        // 1. Создаем экземпляр EventBus (без DI)
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<EventBus>();
        var options = Options.Create(new EventBusOptions());
        var eventBus = new EventBus(logger, options);

        // 2. Определяем события
        public record UserLoggedInEvent : EventBase
    {
        public int UserId { get; init; }
        public string UserName { get; init; } = string.Empty;
    }

    public record OrderCreatedEvent : EventBase
    {
        public int OrderId { get; init; }
        public decimal Amount { get; init; }
    }

    // 3. Подписываемся на события
    var subscription1 = eventBus.Subscribe<UserLoggedInEvent>(evt =>
    {
        Console.WriteLine($"[Sync] User {evt.UserName} logged in at {evt.OccurredAt}");
    });

    var subscription2 = eventBus.SubscribeAsync<UserLoggedInEvent>(async (evt, ct) =>
    {
        await Task.Delay(100, ct);
        Console.WriteLine($"[Async] Welcome email sent to {evt.UserName}");
    });

    // 4. Публикуем события
    await eventBus.PublishAsync(new UserLoggedInEvent
        {
        UserId = 1,
            UserName = "John Doe"
        });
        
        // 5. Отписываемся
        subscription1.Dispose();
        subscription2.Dispose();
        
        // 6. Очистка
        await eventBus.DisposeAsync();
}
}