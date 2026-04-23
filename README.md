# EventBusLib

> **High-performance Event Bus для .NET приложений** с поддержкой синхронных/асинхронных подписок, слабых ссылок и оптимизацией памяти.

---

## Содержание

- [Особенности](#-особенности)
- [Быстрый старт](#-быстрый-старт)
- [Расширенное использование](#-расширенное-использование)
- [Конфигурация](#-конфигурация)
- [Мониторинг и метрики](#-мониторинг-и-метрики)
- [Best Practices](#-best-practices)
- [License](#-license)

---

## Особенности

| Функция | Описание |
|---------|----------|
| **Синхронные и асинхронные обработчики** | Поддержка обоих типов подписок с автоматическим определением контекста |
| **Слабые ссылки (Weak References)** | Автоматическая очистка подписчиков из памяти, предотвращение утечек |
| **Приоритеты подписок** | Контроль порядка выполнения обработчиков через приоритеты |
| **Фильтрация событий** | Гибкая фильтрация событий на уровне подписки |
| **Паттерн Request/Response** | Встроенная поддержка запросов с ответом (Mediator-like pattern) |
| **Пакетная публикация** | Эффективная публикация нескольких событий за один вызов |
| **Метрики и мониторинг** | Встроенные метрики для наблюдения за работой системы |
| **Health Checks** | Интеграция с ASP.NET Core Health Checks |
| **DI Ready** | Простая интеграция с `Microsoft.Extensions.DependencyInjection` |
| **Object Pooling** | Оптимизация выделения памяти через пулинг объектов |
| **Thread-Safe** | Полная потокобезопасность всех операций |

---

## Быстрый старт

### Определите события

```csharp
public record UserLoggedInEvent : EventBase
{
    public int UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
}
```

### Настройка в приложении

```csharp
// Program.cs (ASP.NET Core) или App.xaml.cs (WPF/MAUI)
var services = new ServiceCollection();

services.AddEventBus(options =>
{
    options.MaxConcurrentHandlers = 4;
    options.HandlerTimeoutMs = 5000;
    options.EnableMetrics = true;
    options.EnableDiagnostics = false;
});

var serviceProvider = services.BuildServiceProvider();
```

### Использование

```csharp
public class MyService
{
    private readonly IEventBus _eventBus;
    
    public MyService(IEventBus eventBus)
    {
        _eventBus = eventBus;
        
        // 🔹 Синхронная подписка
        _eventBus.Subscribe<UserLoggedInEvent>(OnUserLoggedIn);
        
        // 🔹 Асинхронная подписка
        _eventBus.SubscribeAsync<UserLoggedInEvent>(OnUserLoggedInAsync);
        
        // 🔹 Подписка с фильтром
        _eventBus.Subscribe<UserLoggedInEvent>(
            OnUserLoggedIn, 
            filter: e => e.UserId == currentUserId);
            
        // 🔹 Подписка с приоритетом
        _eventBus.Subscribe<UserLoggedInEvent>(
            OnUserLoggedIn, 
            priority: 50);
    }
    
    private void OnUserLoggedIn(UserLoggedInEvent evt)
    {
        Console.WriteLine($"User {evt.UserName} logged in");
    }
    
    private async Task OnUserLoggedInAsync(
        UserLoggedInEvent evt, 
        CancellationToken ct)
    {
        await _emailService.SendWelcomeEmailAsync(evt.UserId, ct);
    }
    
    public async Task LoginUserAsync(int userId, string userName)
    {
        // Публикация события
        await _eventBus.PublishAsync(new UserLoggedInEvent 
        { 
            UserId = userId, 
            UserName = userName 
        });
    }
}
```

---

## Расширенное использование

### Паттерн Request/Response

```csharp
// Определяем запрос и ответ
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

// Регистрируем обработчик
_eventBus.RegisterRequestHandler<GetUserQuery, UserDto>(
    async (query, ct) =>
    {
        return await _db.Users
            .Where(u => u.Id == query.UserId)
            .Select(u => new UserDto 
            { 
                Id = u.Id, 
                Name = u.Name,
                Email = u.Email 
            })
            .FirstOrDefaultAsync(ct);
    });

// Выполняем запрос
var user = await _eventBus.QueryAsync<GetUserQuery, UserDto>(
    new GetUserQuery { UserId = 123 });
```

### Приоритеты подписок

```csharp
// Высокий приоритет (выполняется первым)
_eventBus.Subscribe<OrderCreatedEvent>(
    OnOrderCreated, 
    priority: 100);

// Средний приоритет (по умолчанию: 0)
_eventBus.Subscribe<OrderCreatedEvent>(
    OnOrderCreated, 
    priority: 0);

// Низкий приоритет (выполняется последним)
_eventBus.Subscribe<OrderCreatedEvent>(
    OnOrderCreated, 
    priority: -100);
```

### Пакетная публикация

```csharp
var events = new List<OrderEvent>
{
    new OrderEvent { OrderId = 1, Status = "Created" },
    new OrderEvent { OrderId = 2, Status = "Created" },
    new OrderEvent { OrderId = 3, Status = "Created" }
};

// Публикуем все события одним вызовом
await _eventBus.PublishBatchAsync(events);
```

### Управление подписками

```csharp
// Отписка от события
_eventBus.Unsubscribe<UserLoggedInEvent>(OnUserLoggedIn);

// Отписка всех обработчиков для типа события
_eventBus.UnsubscribeAll<UserLoggedInEvent>();

// Использование WeakReference для автоматической очистки
_eventBus.SubscribeWeak<UserLoggedInEvent>(this, OnUserLoggedIn);
```

---

## Конфигурация

| Параметр | По умолчанию | Описание |
|----------|-------------|----------|
| `MaxConcurrentHandlers` | `Environment.ProcessorCount` | Максимальное количество одновременных обработчиков |
| `HandlerTimeoutMs` | `5000` | Таймаут выполнения обработчика (мс) |
| `EnableMetrics` | `true` | Включить сбор метрик |
| `EnableDiagnostics` | `false` | Включить диагностический режим (трассировка) |
| `MaxSubscriptionPerEvent` | `1000` | Максимальное количество подписок на одно событие |
| `ObjectPoolMaxSize` | `1024` | Размер пула объектов для оптимизации памяти |
| `ThrowOnHandlerException` | `false` | Бросать исключения при ошибке в обработчике |
| `CleanupIntervalMs` | `60000` | Интервал очистки слабых ссылок (мс) |
| `SlowHandlerWarningThresholdMs` | `1000` | Порог для предупреждения о медленном обработчике |

### Пример полной конфигурации

```csharp
services.AddEventBus(options =>
{
    // Производительность
    options.MaxConcurrentHandlers = Environment.ProcessorCount * 2;
    options.HandlerTimeoutMs = 10000;
    
    // Мониторинг
    options.EnableMetrics = true;
    options.EnableDiagnostics = Debugger.IsAttached;
    
    // Надёжность
    options.ThrowOnHandlerException = false;
    options.SlowHandlerWarningThresholdMs = 2000;
    
    // Память
    options.ObjectPoolMaxSize = 2048;
    options.CleanupIntervalMs = 30000;
});
```

---

## Мониторинг и метрики

### Подписка на обновления метрик

```csharp
_eventBus.MetricsUpdated += (sender, metrics) =>
{
    Console.WriteLine($"Processed: {metrics.TotalEventsProcessed}");
    Console.WriteLine($"Success rate: {metrics.SuccessRate:F1}%");
    Console.WriteLine($"Active subscriptions: {metrics.ActiveSubscriptions}");
    Console.WriteLine($"Avg handling time: {metrics.AverageHandlingTimeMs:F2}ms");
};
```

### Интеграция с Health Checks (ASP.NET Core)

```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddCheck<EventBusHealthCheck>("eventbus");

// Endpoint
app.MapHealthChecks("/health");
```

### Экспорт метрик в Prometheus

```csharp
// При включённом EnableMetrics
_eventBus.MetricsUpdated += (sender, metrics) =>
{
    Prometheus.Metrics
        .CreateCounter("eventbus_events_processed_total", "Total processed events")
        .Inc(metrics.TotalEventsProcessed);
        
    Prometheus.Metrics
        .CreateGauge("eventbus_active_subscriptions", "Active subscriptions count")
        .Set(metrics.ActiveSubscriptions);
};
```

---

## Best Practices

### Рекомендации

1. **Используйте `record` для событий** — неизменяемость упрощает отладку и тестирование
2. **Отписывайтесь в `Dispose`** — предотвращайте утечки памяти в долгоживущих объектах
3. **Фильтруйте на уровне подписки** — уменьшайте нагрузку на обработчики
4. **Настройте таймауты** — избегайте блокировок при сбоях в обработчиках
5. **Мониторьте медленные обработчики** — используйте `SlowHandlerWarningThresholdMs`

### Чего избегать

```csharp
// Не блокируйте асинхронные обработчики
_eventBus.SubscribeAsync<UserEvent>(async e => 
{
    // Плохо: .Result или .Wait()
    var result = SomeAsyncMethod().Result; 
});

// Используйте await с CancellationToken
_eventBus.SubscribeAsync<UserEvent>(async (e, ct) => 
{
    var result = await SomeAsyncMethod(ct);
});

// Не создавайте события с большими графами объектов
// Передавайте только необходимые данные (ID, а не всю сущность)
```

---

## 📄 License

Распространяется под лицензией **MIT**. См. файл [LICENSE](LICENSE) для деталей.

```
MIT License

Copyright (c) 2024 Your Name

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```


