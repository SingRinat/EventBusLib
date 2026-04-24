using System;
using System.Threading;
using System.Threading.Tasks;

namespace EventBusLib.Dispatching;

/// <summary>
/// Абстракция над диспетчером потоков. В WinUI 3 реализуется через DispatcherQueue.
/// </summary>
public interface IDispatcherProvider
{
    bool IsUiThread { get; }
    Task RunOnUiThreadAsync(Func<Task> action, CancellationToken cancellationToken = default);
}