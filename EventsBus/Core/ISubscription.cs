using System.Threading;
using System.Threading.Tasks;

namespace EventsBus.Core;

internal interface ISubscription
{
    object Owner { get; }
    Task HandleAsync(object @event, CancellationToken cancellationToken);
}