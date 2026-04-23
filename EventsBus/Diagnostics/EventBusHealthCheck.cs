using EventsBus.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EventsBus.Diagnostics;

/// <summary>
/// Health check для шины событий
/// </summary>
public class EventBusHealthCheck : IHealthCheck
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<EventBusHealthCheck> _logger;

    public EventBusHealthCheck(IEventBus eventBus, ILogger<EventBusHealthCheck> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var subscriptionCount = await _eventBus.GetActiveSubscriptionCountAsync();

            if (subscriptionCount > 10000)
            {
                return HealthCheckResult.Degraded(
                    $"High number of subscriptions: {subscriptionCount}");
            }

            return HealthCheckResult.Healthy(
                $"EventBus is healthy. Active subscriptions: {subscriptionCount}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EventBus health check failed");
            return HealthCheckResult.Unhealthy("EventBus is unhealthy", ex);
        }
    }
}