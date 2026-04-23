using System.Threading;
using System.Threading.Tasks;

namespace EventBusLib.Core;

internal interface ISubscription
{
    object Owner { get; }
    Task HandleAsync(object @event, CancellationToken cancellationToken);
}