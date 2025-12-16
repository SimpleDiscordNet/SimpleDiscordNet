using System.Collections.Concurrent;
using System.Threading;

namespace SimpleDiscordNet.Rest;

internal sealed class RouteRateLimiter(TimeProvider time)
{
    private readonly TimeProvider _time = time;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _routeLocks = new();

    public async Task<IDisposable> EnterAsync(string route, CancellationToken ct)
    {
        SemaphoreSlim sem = _routeLocks.GetOrAdd(route, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct).ConfigureAwait(false);
        return new Releaser(sem);
    }

    private sealed class Releaser(SemaphoreSlim sem) : IDisposable
    {
        public void Dispose() => sem.Release();
    }
}
