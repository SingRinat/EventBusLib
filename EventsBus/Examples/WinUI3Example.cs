using EventsBus.Core;
using EventsBus.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EventsBus.Examples;

/// <summary>
/// Пример использования в WinUI 3 приложении
/// </summary>
public class WinUI3Example
{
    // ViewModel с поддержкой EventBus
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly IEventBus _eventBus;
        private readonly List<IDisposable> _subscriptions = new();
        private string _status = string.Empty;

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        public MainViewModel(IEventBus eventBus)
        {
            _eventBus = eventBus;

            // Подписка с автоматической отпиской при уничтожении ViewModel
            var sub = _eventBus.SubscribeAsync<DataLoadedEvent>(OnDataLoadedAsync);
            _subscriptions.Add(sub);
        }

        private async Task OnDataLoadedAsync(DataLoadedEvent evt, CancellationToken ct)
        {
            // Обновление UI должно быть в dispatcher потоке
            await DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
            {
                Status = $"Data loaded: {evt.RecordsCount} records";
            });
        }

        public async Task LoadDataAsync()
        {
            await _eventBus.PublishAsync(new DataLoadedEvent { RecordsCount = 100 });
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            foreach (var sub in _subscriptions)
                sub.Dispose();
            _subscriptions.Clear();
            _eventBus.UnsubscribeAll(this);
        }
    }

    public record DataLoadedEvent : EventBase
    {
        public int RecordsCount { get; init; }
    }
}