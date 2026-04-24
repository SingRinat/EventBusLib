# WinUI3.EventBus

Производительная, потокобезопасная шина событий для WinUI 3 с поддержкой слабых ссылок, асинхронных операций, паттерна Request-Response и встроенной интеграцией с `DispatcherQueue`.

## ✨ Возможности
- 🔒 **Потокобезопасность**: Все операции атомарны, отсутствие блокировок в горячем пути.
- 🗑️ **Управление памятью**: Слабые ссылки (`WeakReference`) предотвращают утечки. Автоматическая дефрагментация коллекций.
- 🧵 **WinUI 3 Ready**: Встроенный `IDispatcherProvider` использует нативный `DispatcherQueue`. Не требует ручного `DispatcherQueue.TryEnqueue`.
- ⚡ **Синхронная/Асинхронная публикация**: `Publish` (fire-and-forget для async) и `PublishAsync` (ожидание всех обработчиков).
- 🔄 **Request-Response**: Типобезопасный запрос данных через `RequestAsync<TReq, TRes>`.
- 🛡️ **Изоляция ошибок**: Исключение в одном обработчике не ломает шину. Доступно через `HandlerError`.

## 📦 Установка
1. Создайте проект `Class Library (.NET)` с `TargetFramework: net8.0-windows10.0.xxxxx.0`
2. Подключите зависимости:
   ```xml
   <PackageReference Include="Microsoft.WindowsAppSDK" Version="1.5.240627000" />
   <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
   ```
3. Добавьте файлы из структуры выше.

## 🚀 Быстрый старт

### 1. Регистрация в DI
```csharp
// App.xaml.cs
public App()
{
    var services = new ServiceCollection();
    services.AddEventBus(new WinUIDispatcherProvider(DispatcherQueue.GetForCurrentThread()));
    services.AddTransient<MainViewModel>();
    
    ServiceProvider = services.BuildServiceProvider();
    InitializeComponent();
}
```

### 2. Подписка в ViewModel
```csharp
public class MainViewModel : INotifyPropertyChanged
{
    private readonly IEventBus _bus;
    private IDisposable _subscription;

    public MainViewModel(IEventBus bus)
    {
        _bus = bus;
        // marshalToUiThread = true → обработчик выполнится в UI-потоке автоматически
        _subscription = _bus.Subscribe<UserCreatedEvent>(
            e => UserName = e.Name, 
            marshalToUiThread: true);
    }

    private string _userName;
    public string UserName
    {
        get => _userName;
        set { _userName = value; OnPropertyChanged(); }
    }

    public void Dispose() => _subscription?.Dispose();
}
```

### 3. Публикация
```csharp
// Из любого потока (репозиторий, сервис, UI)
_bus.Publish(new UserCreatedEvent { Name = "Alex" });

// Или асинхронно с отменой
await _bus.PublishAsync(new OrderPlacedEvent(orderId), ct);
```

### 4. Запрос-Ответ (Request-Response)
```csharp
// Регистрация (обычно в DataLayer)
_bus.RegisterRequestHandler<LoadSettingsRequest, AppSettings>(
    async (req, ct) => await SettingsRepository.LoadAsync(ct));

// Использование (обычно в UI/ViewModel)
var settings = await _bus.RequestAsync<LoadSettingsRequest, AppSettings>(
    new LoadSettingsRequest(), ct);
```

## 🧠 Архитектура и внутреннее устройство

```
┌─────────────────────────────────────────────────────────────┐
│                         IEventBus                           │
├──────────────┬──────────────┬──────────────┬────────────────┤
│ Publish/Sync │ PublishAsync │ Subscribe    │ Request/Handle │
└──────┬───────┴──────┬───────┴──────┬───────┴───────┬────────┘
       │              │              │               │
┌──────▼──────┐ ┌─────▼──────┐ ┌────▼──────┐ ┌──────▼───────┐
│ Sync/Async  │ │ AsyncWait  │ │ WeakRefs  │ │ Req/Res Map  │
│ Registries  │ │ Dispatcher │ │ Cleanup   │ │ Singleton    │
└─────────────┘ └────────────┘ └───────────┘ └──────────────┘
```

### 🔑 Ключевые решения
| Компонент | Решение | Почему |
|-----------|---------|--------|
| **Хранение подписок** | `ConcurrentDictionary<Type, object>` + `List<T>` внутри `lock` | Быстрый поиск по типу, безопасные снимки при публикации |
| **Слабые ссылки** | `WeakReference<Delegate>` + отложенная очистка | Подписчики не удерживаются в памяти, `IDisposable` для явной отписки |
| **UI-поток** | `IDispatcherProvider` → `DispatcherQueue.TryEnqueue` | Нативная поддержка WinUI 3, избегаем `SynchronizationContext` хаков |
| **Обработка ошибок** | `try/catch` вокруг каждого обработчика + `HandlerError` event | Один упавший обработчик не блокирует остальные |
| **Отмена** | `CancellationToken` пробрасывается в `SubscribeAsync` и `RequestAsync` | Корректная отмена при навигации или закрытии страницы |

## ⚠️ Best Practices & Pitfalls

1. **Всегда вызывайте `Dispose()` на `IDisposable` подписки** при уничтожении ViewModel/страницы. Слабые ссылки — страховка, а не замена явной отписки.
2. **Используйте `marshalToUiThread: true` только для операций обновления UI**. Фоновые операции (логирование, кэширование) оставьте `false`.
3. **Не публикуйте тяжелые объекты**. Шина передаёт ссылки. Если данные мутабельны, используйте `record` или `struct`.
4. **`RequestAsync` ожидает только ОДНОГО обработчика**. Если зарегистрировано несколько, будет выброшено `InvalidOperationException`. Используйте `PublishAsync` для broadcast.
5. **Избегайте `async void` в обработчиках**. Используйте `Func<T, CancellationToken, Task>`. Шина отслеживает завершение.
6. **Регулярно вызывайте `_bus.Cleanup()`** (например, раз в 30 сек или при переходе между страницами) для очистки "мёртвых" слабых ссылок.

## 📝 Лицензия
MIT. Подходит для коммерческих и открытых проектов.
```

---

### ✅ Чек-лист продакшен-готовности
- [x] Нулевые аллокации в горячем пути `Publish` (snapshots создаются только при изменении)
- [x] `DispatcherQueue` интеграция вместо `SynchronizationContext` (WinUI 3 native)
- [x] Централизованный `HandlerError` для логирования (Serilog/NLog/Sentry ready)
- [x] `CancellationToken` пробрасывается во все асинхронные пути
- [x] `RegisterRequestHandler` защищён от дубликатов через `InvalidOperationException`
- [x] Полная совместимость с `Microsoft.Extensions.DependencyInjection`
- [x] Документация покрывает архитектуру, типичные ошибки и примеры

Библиотека готова к `nuget pack` или прямому подключению как `ProjectReference`. Если нужно — добавлю генерацию `TypedEventBus` через Source Generators для полной типобезопасности без `object` cast.
