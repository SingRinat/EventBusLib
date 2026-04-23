using EventBusLib.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EventBusLib.Core;

internal sealed class FilteredSyncSubscription<TEvent> : SubscriptionBase where TEvent : EventBase
{
    private readonly Action<TEvent> _handler;
    private readonly Func<TEvent, bool> _filter;

    public FilteredSyncSubscription(Action<TEvent> handler, Func<TEvent, bool> filter) : base(handler)
    {
        _handler = handler;
        _filter = filter;
    }

    public override Task HandleAsync(object @event, CancellationToken cancellationToken)
    {
        if (@event is TEvent typedEvent && OwnerRef.IsAlive && _filter(typedEvent))
        {
            _handler(typedEvent);
        }
        return Task.CompletedTask;
    }
}