using EventsBus.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EventsBus.Core;

internal sealed class AsyncSubscription<TEvent> : SubscriptionBase where TEvent : EventBase
{
    private readonly Func<TEvent, CancellationToken, Task> _handler;

    public AsyncSubscription(Func<TEvent, CancellationToken, Task> handler) : base(handler)
    {
        _handler = handler;
    }

    public override async Task HandleAsync(object @event, CancellationToken cancellationToken)
    {
        if (@event is TEvent typedEvent && OwnerRef.IsAlive)
        {
            await _handler(typedEvent, cancellationToken);
        }
    }
}

