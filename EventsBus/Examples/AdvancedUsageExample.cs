using EventsBus.Core;
using EventsBus.Extensions;
using EventsBus.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using static EventsBus.Examples.BasicUsageExample;

namespace EventsBus.Examples;

/// <summary>
/// Продвинутый пример использования EventBus с DI и паттерном Mediator
/// </summary>
public class AdvancedUsageExample
{
    // Определение запроса
    public record GetUserQuery : RequestBase<UserDto>
    {
        public int UserId { get; init; }
    }

    public record UserDto
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
    }

    // Сервис для работы с пользователями
    public class UserService
    {
        private readonly IEventBus _eventBus;
        private readonly ILogger<UserService> _logger;

        public UserService(IEventBus eventBus, ILogger<UserService> logger)
        {
            _eventBus = eventBus;
            _logger = logger;

            // Регистрируем обработчик запроса
            _eventBus.RegisterRequestHandler<GetUserQuery, UserDto>(HandleGetUserAsync);

            // Подписываемся на события
            _eventBus.SubscribeAsync<UserLoggedInEvent>(OnUserLoggedInAsync, priority: 10);
            _eventBus.Subscribe<OrderCreatedEvent>(OnOrderCreated, filter: e => e.Amount > 1000);
        }

        private async Task<UserDto> HandleGetUserAsync(GetUserQuery query, CancellationToken ct)
        {
            _logger.LogInformation("Getting user {UserId}", query.UserId);

            // Имитация загрузки из БД
            await Task.Delay(50, ct);

            return new UserDto
            {
                Id = query.UserId,
                Name = "John Doe",
                Email = "john@example.com"
            };
        }

        private async Task OnUserLoggedInAsync(UserLoggedInEvent evt, CancellationToken ct)
        {
            _logger.LogInformation("User {UserName} logged in", evt.UserName);

            // Можно делать запросы к шине из обработчика
            var userData = await _eventBus.QueryAsync<GetUserQuery, UserDto>(
                new GetUserQuery { UserId = evt.UserId }, ct);

            _logger.LogInformation("User data loaded: {UserEmail}", userData.Email);
        }

        private void OnOrderCreated(OrderCreatedEvent evt)
        {
            _logger.LogInformation("Large order created: {OrderId} for {Amount:C}",
                evt.OrderId, evt.Amount);
        }

        public async Task CreateOrderAsync(int orderId, decimal amount)
        {
            await _eventBus.PublishAsync(new OrderCreatedEvent
            {
                OrderId = orderId,
                Amount = amount
            });
        }
    }

    // Настройка DI в приложении
    public static async Task RunWithDIAsync()
    {
        var services = new ServiceCollection();

        // Регистрируем EventBus
        services.AddEventBus(options =>
        {
            options.MaxConcurrentHandlers = 4;
            options.HandlerTimeoutMs = 5000;
            options.EnableMetrics = true;
            options.SlowHandlerWarningThresholdMs = 500;
        });

        // Регистрируем сервисы
        services.AddScoped<UserService>();
        services.AddLogging(builder => builder.AddConsole());

        var serviceProvider = services.BuildServiceProvider();

        // Получаем сервисы
        var eventBus = serviceProvider.GetRequiredService<IEventBus>();
        var userService = serviceProvider.GetRequiredService<UserService>();

        // Подписываемся на метрики
        eventBus.MetricsUpdated += (sender, metrics) =>
        {
            Console.WriteLine($"Metrics: {metrics}");
        };

        // Используем
        var userData = await eventBus.QueryAsync<GetUserQuery, UserDto>(
            new GetUserQuery { UserId = 123 });

        Console.WriteLine($"User: {userData.Name}");

        await userService.CreateOrderAsync(1001, 1500m);

        // Ожидаем завершения
        await eventBus.WaitForIdleAsync();
    }
}