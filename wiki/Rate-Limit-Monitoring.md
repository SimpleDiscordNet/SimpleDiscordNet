# Rate Limit Monitoring Examples

SimpleDiscordNet provides advanced rate limiting with comprehensive monitoring and event APIs.

## Features

- ✅ **Bucket-based rate limiting** - Tracks Discord's rate limit buckets using `X-RateLimit-Bucket` headers
- ✅ **Global rate limiting** - Respects Discord's 50 req/sec global limit
- ✅ **Pre-emptive waiting** - Avoids 429s by proactively waiting when limits are reached
- ✅ **Automatic retries** - Retries up to 5 times with exponential backoff for server errors
- ✅ **Zero message loss** - All requests are queued and retried, ensuring no messages are lost
- ✅ **Real-time events** - Subscribe to rate limit events for monitoring and alerting
- ✅ **Statistics API** - Query current rate limit state and historical statistics

## Basic Event Monitoring

Subscribe to rate limit events to monitor your bot's API usage:

```csharp
using SimpleDiscordNet;
using SimpleDiscordNet.Rest;

// Subscribe to rate limit events
RateLimitEventManager.BucketUpdated += (sender, e) =>
{
    Console.WriteLine($"Bucket updated: {e.BucketId} - {e.Remaining}/{e.Limit} remaining");
};

RateLimitEventManager.RateLimitHit += (sender, e) =>
{
    Console.WriteLine($"⚠️ Rate limit hit on {e.Route}!");
    Console.WriteLine($"   Bucket: {e.BucketId}");
    Console.WriteLine($"   Retry after: {e.RetryAfter.TotalSeconds}s");
    Console.WriteLine($"   Is global: {e.IsGlobal}");
    Console.WriteLine($"   Total 429s: {e.Total429Count}");
};

RateLimitEventManager.PreEmptiveWait += (sender, e) =>
{
    Console.WriteLine($"⏳ Pre-emptive wait for {e.Route}");
    Console.WriteLine($"   Waiting: {e.WaitDuration.TotalSeconds:F1}s");
    Console.WriteLine($"   Remaining: {e.Remaining}/{e.Limit}");
};
```

## Dashboard Monitoring

Create a real-time dashboard to monitor rate limit health:

```csharp
using SimpleDiscordNet.Core;
using SimpleDiscordNet.Rest;

public class RateLimitDashboard
{
    private readonly IRateLimitMonitor _monitor;
    private readonly Timer _updateTimer;

    public RateLimitDashboard(IDiscordBot bot)
    {
        _monitor = bot.RateLimitMonitor;

        // Update dashboard every 5 seconds
        _updateTimer = new Timer(_ => UpdateDashboard(), null,
            TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    private void UpdateDashboard()
    {
        Console.Clear();
        Console.WriteLine("=== Rate Limit Dashboard ===\n");

        var buckets = _monitor.GetAllBuckets();

        Console.WriteLine($"Total Buckets: {buckets.Count}");
        Console.WriteLine($"Total 429s: {_monitor.GetTotal429Count()}");
        Console.WriteLine($"Total Pre-emptive Waits: {_monitor.GetTotalPreEmptiveWaits()}");
        Console.WriteLine();

        // Show rate limited buckets
        var rateLimited = _monitor.GetRateLimitedBuckets();
        if (rateLimited.Count > 0)
        {
            Console.WriteLine("⚠️ Currently Rate Limited:");
            foreach (var bucket in rateLimited)
            {
                Console.WriteLine($"  - {bucket.Route}");
                Console.WriteLine($"    Resets in: {bucket.TimeUntilReset.TotalSeconds:F1}s");
            }
            Console.WriteLine();
        }

        // Show most utilized bucket
        var mostUtilized = _monitor.GetMostUtilizedBucket();
        if (mostUtilized != null)
        {
            Console.WriteLine("🔥 Most Utilized Bucket:");
            Console.WriteLine($"  Route: {mostUtilized.Route}");
            Console.WriteLine($"  Remaining: {mostUtilized.Remaining}/{mostUtilized.Limit} ({mostUtilized.RemainingPercentage:F1}%)");
            Console.WriteLine($"  Total Requests: {mostUtilized.TotalRequests}");
            Console.WriteLine($"  429s: {mostUtilized.Total429s}");
            Console.WriteLine();
        }

        // Show all buckets
        Console.WriteLine("All Buckets:");
        foreach (var bucket in buckets.OrderByDescending(b => b.TotalRequests).Take(10))
        {
            string status = bucket.IsRateLimited ? "🔴" :
                           bucket.RemainingPercentage < 20 ? "🟡" : "🟢";

            Console.WriteLine($"  {status} {bucket.Route}");
            Console.WriteLine($"     {bucket.Remaining}/{bucket.Limit} remaining ({bucket.RemainingPercentage:F0}%)");
            Console.WriteLine($"     Requests: {bucket.TotalRequests}, Waits: {bucket.TotalWaits}, 429s: {bucket.Total429s}");
        }
    }
}
```

## Alerting on High Usage

Set up alerts when rate limits are being stressed:

```csharp
public class RateLimitAlerting
{
    private const double WarningThreshold = 20.0; // 20% remaining
    private const double CriticalThreshold = 10.0; // 10% remaining

    public void SetupAlerts()
    {
        RateLimitEventManager.BucketUpdated += (sender, e) =>
        {
            if (e.Remaining == 0)
            {
                SendAlert($"🔴 CRITICAL: Bucket {e.BucketId} exhausted!");
            }
            else
            {
                double remainingPercent = (double)e.Remaining / e.Limit * 100;

                if (remainingPercent <= CriticalThreshold)
                {
                    SendAlert($"🔴 CRITICAL: Bucket {e.BucketId} at {remainingPercent:F1}%");
                }
                else if (remainingPercent <= WarningThreshold)
                {
                    SendAlert($"🟡 WARNING: Bucket {e.BucketId} at {remainingPercent:F1}%");
                }
            }
        };

        RateLimitEventManager.RateLimitHit += (sender, e) =>
        {
            if (e.IsGlobal)
            {
                SendAlert($"🔴 CRITICAL: Global rate limit hit! Retry after {e.RetryAfter.TotalSeconds}s");
            }
            else
            {
                SendAlert($"⚠️ Rate limit hit on {e.Route}. Total 429s: {e.Total429Count}");
            }
        };
    }

    private void SendAlert(string message)
    {
        Console.WriteLine($"[ALERT {DateTime.Now:HH:mm:ss}] {message}");
        // Send to monitoring service (e.g., Sentry, Datadog, Discord webhook)
    }
}
```

## Logging for Diagnostics

Log rate limit events for debugging and analysis:

```csharp
public class RateLimitLogger
{
    private readonly ILogger _logger;

    public RateLimitLogger(ILogger logger)
    {
        _logger = logger;

        RateLimitEventManager.BucketUpdated += OnBucketUpdated;
        RateLimitEventManager.RateLimitHit += OnRateLimitHit;
        RateLimitEventManager.PreEmptiveWait += OnPreEmptiveWait;
    }

    private void OnBucketUpdated(object? sender, RateLimitBucketUpdateEvent e)
    {
        _logger.LogDebug(
            "Bucket {BucketId} updated: {Remaining}/{Limit} remaining, resets at {ResetAt}",
            e.BucketId, e.Remaining, e.Limit, e.ResetAt);
    }

    private void OnRateLimitHit(object? sender, RateLimitHitEvent e)
    {
        _logger.LogWarning(
            "Rate limit hit on {Route} (bucket {BucketId}). " +
            "Retry after {RetryAfter}s. Total 429s: {Total429Count}. Global: {IsGlobal}",
            e.Route, e.BucketId, e.RetryAfter.TotalSeconds, e.Total429Count, e.IsGlobal);
    }

    private void OnPreEmptiveWait(object? sender, RateLimitPreEmptiveWaitEvent e)
    {
        _logger.LogInformation(
            "Pre-emptive wait for {Route}: waiting {WaitDuration}s " +
            "({Remaining}/{Limit} remaining, resets at {ResetAt})",
            e.Route, e.WaitDuration.TotalSeconds, e.Remaining, e.Limit, e.ResetAt);
    }
}
```

## Metrics Export

Export metrics to your monitoring system:

```csharp
public class RateLimitMetrics
{
    private readonly IRateLimitMonitor _monitor;

    public RateLimitMetrics(IRateLimitMonitor monitor)
    {
        _monitor = monitor;
    }

    public Dictionary<string, object> GetMetrics()
    {
        var buckets = _monitor.GetAllBuckets();

        return new Dictionary<string, object>
        {
            ["total_buckets"] = buckets.Count,
            ["total_requests"] = buckets.Sum(b => b.TotalRequests),
            ["total_429s"] = _monitor.GetTotal429Count(),
            ["total_preemptive_waits"] = _monitor.GetTotalPreEmptiveWaits(),
            ["rate_limited_buckets"] = _monitor.GetRateLimitedBuckets().Count,
            ["avg_remaining_percent"] = buckets.Average(b => b.RemainingPercentage),
            ["min_remaining_percent"] = buckets.Min(b => b.RemainingPercentage),
            ["buckets"] = buckets.Select(b => new
            {
                bucket_id = b.BucketId,
                route = b.Route,
                remaining = b.Remaining,
                limit = b.Limit,
                remaining_percent = b.RemainingPercentage,
                total_requests = b.TotalRequests,
                total_waits = b.TotalWaits,
                total_429s = b.Total429s,
                is_rate_limited = b.IsRateLimited,
                time_until_reset_seconds = b.TimeUntilReset.TotalSeconds
            }).ToList()
        };
    }

    // Export to Prometheus, StatsD, etc.
    public void ExportToPrometheus()
    {
        var metrics = GetMetrics();

        // Example: Export to Prometheus
        // PrometheusMetrics.Set("discord_rate_limit_buckets", (long)metrics["total_buckets"]);
        // PrometheusMetrics.Set("discord_rate_limit_429s", (long)metrics["total_429s"]);
        // etc.
    }
}
```

## Query Current State

Query rate limit state on-demand:

```csharp
public class RateLimitQueries
{
    private readonly IRateLimitMonitor _monitor;

    public RateLimitQueries(IRateLimitMonitor monitor)
    {
        _monitor = monitor;
    }

    public void ShowBucketDetails(string bucketId)
    {
        var bucket = _monitor.GetBucket(bucketId);
        if (bucket == null)
        {
            Console.WriteLine($"Bucket {bucketId} not found");
            return;
        }

        Console.WriteLine($"Bucket: {bucket.BucketId}");
        Console.WriteLine($"Route: {bucket.Route}");
        Console.WriteLine($"Remaining: {bucket.Remaining}/{bucket.Limit} ({bucket.RemainingPercentage:F1}%)");
        Console.WriteLine($"Reset At: {bucket.ResetAt:HH:mm:ss}");
        Console.WriteLine($"Time Until Reset: {bucket.TimeUntilReset.TotalSeconds:F1}s");
        Console.WriteLine($"Is Rate Limited: {bucket.IsRateLimited}");
        Console.WriteLine($"Total Requests: {bucket.TotalRequests}");
        Console.WriteLine($"Total Waits: {bucket.TotalWaits}");
        Console.WriteLine($"Total 429s: {bucket.Total429s}");
    }

    public bool CanSafelyBurst(string bucketId, int requestCount)
    {
        var bucket = _monitor.GetBucket(bucketId);
        if (bucket == null) return true; // Unknown bucket, allow

        return bucket.Remaining >= requestCount;
    }

    public void ShowHealthCheck()
    {
        var rateLimited = _monitor.GetRateLimitedBuckets();
        var total429s = _monitor.GetTotal429Count();
        var mostUtilized = _monitor.GetMostUtilizedBucket();

        string health = rateLimited.Count == 0 && total429s < 10 ? "🟢 Healthy" :
                       rateLimited.Count < 3 && total429s < 50 ? "🟡 Degraded" :
                       "🔴 Critical";

        Console.WriteLine($"Rate Limit Health: {health}");
        Console.WriteLine($"Rate Limited Buckets: {rateLimited.Count}");
        Console.WriteLine($"Total 429s: {total429s}");

        if (mostUtilized != null)
        {
            Console.WriteLine($"Lowest Remaining: {mostUtilized.RemainingPercentage:F1}% ({mostUtilized.Route})");
        }
    }
}
```

## Best Practices

1. **Monitor in Production** - Always subscribe to rate limit events in production
2. **Set up Alerts** - Alert on repeated 429s or global rate limits
3. **Log Everything** - Log rate limit events for post-mortem analysis
4. **Dashboard Visibility** - Create dashboards to visualize rate limit health
5. **Respect the Limits** - If you're hitting limits frequently, reduce request rate
6. **Use Pre-emptive Waits** - The library will automatically wait to avoid 429s
7. **Check Before Bursts** - Use `CanSafelyBurst()` before sending many requests

## Zero Message Loss Guarantee

SimpleDiscordNet's rate limiter ensures **zero message loss**:

- ✅ Requests are automatically retried up to 5 times on 429 responses
- ✅ Exponential backoff for server errors (5xx)
- ✅ Global queue prevents overwhelming Discord's global limit
- ✅ Pre-emptive waiting prevents 429s before they occur
- ✅ Bucket tracking respects Discord's rate limit boundaries

All requests will eventually succeed unless:
- The operation is cancelled via `CancellationToken`
- Discord returns a client error (4xx other than 429)
- Maximum retry count is exceeded (extremely rare)
