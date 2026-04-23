using EventBusLib.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EventBusLib.Core;

internal sealed class FilteredAsyncSubscription<TEvent> : SubscriptionBase where TEvent : EventBase
{
    private readonly Func<TEvent, CancellationToken, Task> _handler;
    private readonly Func<TEvent, bool> _filter;

    public FilteredAsyncSubscription(Func<TEvent, CancellationToken, Task> handler, Func<TEvent, bool> filter) : base(handler)
    {
        _handler = handler;
        _filter = filter;
    }

    public override async Task HandleAsync(object @event, CancellationToken cancellationToken)
    {
        if (@event is TEvent typedEvent && OwnerRef.IsAlive && _filter(typedEvent))
        {
            await _handler(typedEvent, cancellationToken);
        }
    }
}

