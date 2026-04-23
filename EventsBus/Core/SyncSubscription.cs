using EventsBus.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EventsBus.Core;

internal sealed class SyncSubscription<TEvent> : SubscriptionBase where TEvent : EventBase
{
    private readonly Action<TEvent> _handler;

    public SyncSubscription(Action<TEvent> handler) : base(handler)
    {
        _handler = handler;
    }

    public override Task HandleAsync(object @event, CancellationToken cancellationToken)
    {
        if (@event is TEvent typedEvent && OwnerRef.IsAlive)
        {
            _handler(typedEvent);
        }
        return Task.CompletedTask;
    }
}