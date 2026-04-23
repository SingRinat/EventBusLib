using System;
using System.Threading;
using System.Threading.Tasks;

namespace EventBusLib.Core;

internal abstract class SubscriptionBase : ISubscription
{
    protected readonly object Handler;
    protected readonly WeakReference OwnerRef;

    protected SubscriptionBase(object handler)
    {
        Handler = handler;
        OwnerRef = new WeakReference(handler?.GetType().GetProperty("Target")?.GetValue(handler) ?? handler);
    }

    public object Owner => OwnerRef.Target ?? throw new ArgumentNullException(nameof(OwnerRef.Target), "Target OwnerRef is null");

    public abstract Task HandleAsync(object @event, CancellationToken cancellationToken);
}