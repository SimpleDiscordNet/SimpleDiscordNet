# Sharding

**Version**: 1.4.0
**Status**: ✅ Production Ready

Sharding allows your Discord bot to scale horizontally across multiple processes or machines, essential for large bots serving thousands of guilds.

**🆕 v1.4.0:** Distributed sharding now uses HTTPS with TLS 1.3+ for secure coordinator/worker communication.

## Overview

SimpleDiscordNet provides **three sharding modes**:

1. **SingleProcess** (default) - No sharding, single gateway connection
2. **SingleProcess with Sharding** - Multiple shards in one process
3. **Distributed** - Coordinator + Workers across multiple processes/machines

All modes are fully AoT-compatible with zero reflection usage.

---

## Mode 1: SingleProcess (Default)

The simplest mode with no sharding. Perfect for bots in fewer than 2,500 guilds.

```csharp
var bot = new DiscordBot(token, intents);
await bot.StartAsync();
```

**When to Use**: Small to medium bots (< 2,500 guilds)

---

## Mode 2: SingleProcess with Sharding

Run multiple shards within a single process. Discord requires sharding at 2,500 guilds.

### Automatic Sharding

```csharp
var bot = new DiscordBot.Builder(token, intents)
    .WithSharding(shardId: 0, totalShards: 4)
    .Build();

await bot.StartAsync();
```

### Running Multiple Shard Processes

```bash
# Terminal 1 - Shard 0 of 4
dotnet run -- --shard-id 0 --total-shards 4

# Terminal 2 - Shard 1 of 4
dotnet run -- --shard-id 1 --total-shards 4

# Terminal 3 - Shard 2 of 4
dotnet run -- --shard-id 2 --total-shards 4

# Terminal 4 - Shard 3 of 4
dotnet run -- --shard-id 3 --total-shards 4
```

```csharp
// Parse command line args
int shardId = int.Parse(args[0]);
int totalShards = int.Parse(args[1]);

var bot = new DiscordBot.Builder(token, intents)
    .WithSharding(shardId, totalShards)
    .Build();
```

**When to Use**: Medium bots (2,500-10,000 guilds), manual control desired

---

## Mode 3: Distributed

Fully automated distributed sharding with coordinator/worker architecture.

### Architecture

```
┌─────────────────┐
│   Coordinator   │ ← Auto-detects shard count
│  (HTTP API)     │ ← Health monitoring
└────────┬────────┘ ← Load balancing
         │
    ┌────┴─────┬─────────┬─────────┐
    │          │         │         │
┌───▼───┐  ┌──▼────┐ ┌──▼────┐ ┌──▼────┐
│Worker1│  │Worker2│ │Worker3│ │Worker4│
│Shard 0│  │Shard 1│ │Shard 2│ │Shard 3│
└───────┘  └───────┘ └───────┘ └───────┘
```

### Coordinator Setup

```csharp
var coordinator = new DiscordBot.Builder(token, intents)
    .WithDistributedCoordinator(
        listenUrl: "https://+:8443/",  // 🆕 v1.4.0: HTTPS with TLS
        isOriginalCoordinator: true
    )
    .Build();

await coordinator.StartAsync();
```

**🆕 v1.4.0 Security Note:**
- Default port changed from 8080 to 8443
- HTTPS/TLS is now required for coordinator communication
- For production: Configure SSL certificates using `netsh http add sslcert` (Windows) or use a TLS-terminating reverse proxy (nginx, Caddy, etc.)
- Development: Self-signed certificates can be used for testing

**Role**:
- Auto-detects shard count from Discord API
- Assigns shards to workers as they register
- Monitors worker health (15s timeout)
- Triggers load balancing (CPU > 80% or latency > 500ms)
- Handles coordinator succession on failure

### Worker Setup

```csharp
var worker = new DiscordBot.Builder(token, intents)
    .WithDistributedWorker(
        coordinatorUrl: "https://coordinator:8443/",  // 🆕 v1.4.0: HTTPS
        listenUrl: "http://+:8081/",
        processId: "worker-1"
    )
    .WithSlashCommand("/ping", async ctx => {
        await ctx.RespondAsync($"Pong from shard {ctx.ShardId}!");
    })
    .Build();

await worker.StartAsync();
```

**Role**:
- Registers with coordinator on startup
- Receives shard assignments dynamically
- Reports metrics every 5 seconds
- Handles shard migration requests
- Re-registers on coordinator failover

### Docker Compose Example

```yaml
version: '3.8'
services:
  coordinator:
    image: mybot:latest
    environment:
      - DISCORD_TOKEN=${DISCORD_TOKEN}
      - SHARD_MODE=coordinator
      - LISTEN_URL=https://+:8443/
    ports:
      - "8443:8443"
    volumes:
      - ./certs:/certs  # SSL certificates
    command: ["--mode", "coordinator"]

  worker-1:
    image: mybot:latest
    environment:
      - DISCORD_TOKEN=${DISCORD_TOKEN}
      - SHARD_MODE=worker
      - COORDINATOR_URL=http://coordinator:8080/
      - LISTEN_URL=http://+:8081/
      - PROCESS_ID=worker-1
    command: ["--mode", "worker", "--id", "worker-1"]

  worker-2:
    image: mybot:latest
    environment:
      - DISCORD_TOKEN=${DISCORD_TOKEN}
      - SHARD_MODE=worker
      - COORDINATOR_URL=http://coordinator:8080/
      - LISTEN_URL=http://+:8081/
      - PROCESS_ID=worker-2
    command: ["--mode", "worker", "--id", "worker-2"]
```

**When to Use**: Large bots (10,000+ guilds), horizontal scaling required

---

## Features

### Shard-Aware Commands

```csharp
[SlashCommand("info", "Get shard info")]
public async Task InfoAsync(InteractionContext ctx)
{
    var shardId = ctx.ShardId;
    var totalShards = ctx.Bot.TotalShards;

    await ctx.RespondAsync($"You are on shard {shardId}/{totalShards}");
}
```

### Cross-Shard Cache Queries

```csharp
// In distributed mode, queries across all workers
var allGuilds = await bot.Cache.GetGuildsAsync();
var guild = await bot.Cache.GetGuildAsync(guildId);
```

### Shard Metrics

```csharp
var shards = bot.ShardManager.GetAllShards();
foreach (var shard in shards)
{
    Console.WriteLine($"Shard {shard.Id}: {shard.Status}");
    Console.WriteLine($"  Guilds: {shard.GuildCount}");
    Console.WriteLine($"  Latency: {shard.LatencyMs}ms");
    Console.WriteLine($"  Events/sec: {shard.EventsPerSecond}");
}
```

### Guild → Shard Mapping

```csharp
using SimpleDiscordNet.Sharding;

// Calculate which shard handles a guild
int shardId = ShardCalculator.GetShardId(guildId, totalShards);
```

---

## High Availability

### Coordinator Succession

If the coordinator fails, workers automatically elect the next coordinator from the succession list:

```csharp
// Coordinator announces succession order on startup
// Workers track succession and promote on failure
// New coordinator resumes from previous state
```

**Failover Time**: < 30 seconds

### Original Coordinator Resumption

If the original coordinator comes back online, it can resume control:

```csharp
var coordinator = new DiscordBot.Builder(token, intents)
    .WithDistributedCoordinator(
        listenUrl: "https://+:8443/",  // 🆕 v1.4.0: HTTPS
        isOriginalCoordinator: true    // ← Enables resumption
    )
    .Build();
```

Workers re-register and state is synchronized.

### Worker Failure Recovery

If a worker crashes:
1. Coordinator detects failure (3 missed heartbeats = 45 seconds)
2. Shards are reassigned to healthy workers
3. New workers can join anytime and receive shards

---

## Load Balancing

The coordinator automatically migrates shards when:

- **CPU Usage** > 80% on a worker
- **Gateway Latency** > 500ms for a shard

Migration is seamless with no downtime.

---

## Configuration

### Coordinator Options

```csharp
.WithDistributedCoordinator(
    listenUrl: "https://+:8443/",         // 🆕 v1.4.0: HTTPS endpoint
    isOriginalCoordinator: true           // Enable resumption
)
```

**Security Configuration:**
- SSL certificates must be configured for HTTPS to work
- Windows: Use `netsh http add sslcert` to bind certificates
- Linux: Use reverse proxy (nginx, Caddy) for TLS termination
- See [Microsoft docs](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-http3) for HttpListener SSL configuration

### Worker Options

```csharp
.WithDistributedWorker(
    coordinatorUrl: "http://coordinator:8080/",
    listenUrl: "http://+:8081/",          // Worker HTTP endpoint
    processId: "worker-1",                // Unique identifier
    maxShards: 10                         // Optional limit
)
```

---

## Monitoring

### HTTP Endpoints

**Coordinator**: `http://coordinator:8080/cluster/state`
```json
{
  "coordinatorId": "coordinator-1",
  "totalShards": 16,
  "workers": [
    {
      "processId": "worker-1",
      "shards": [0, 1, 2, 3],
      "cpuUsage": 45.2,
      "memoryMb": 512,
      "status": "healthy"
    }
  ]
}
```

**Worker**: `http://worker:8081/health`
```json
{
  "processId": "worker-1",
  "status": "healthy",
  "shards": [0, 1, 2, 3],
  "cpuUsage": 45.2,
  "memoryMb": 512
}
```

---

## Performance Characteristics

### SingleProcess Mode
- **Latency**: ~50-150ms per shard
- **Memory**: ~50-100MB per shard
- **CPU**: ~5-10% per shard
- **Recommended**: Up to 4-8 shards

### Distributed Mode
- **Latency**: ~50-150ms per shard + ~10-20ms HTTP overhead
- **Memory**: ~50-100MB per shard + ~30MB coordinator
- **CPU**: ~5-10% per shard + ~2-5% coordinator
- **Recommended**: 8+ shards
- **Failover**: < 5s worker, < 30s coordinator

---

## Best Practices

1. **Start Simple**: Use SingleProcess until you hit 2,500 guilds
2. **Monitor Metrics**: Watch CPU, memory, latency before scaling
3. **Use Distributed for Scale**: 10,000+ guilds benefit from coordinator/worker
4. **Firewall HTTP Endpoints**: Workers should only accept coordinator traffic
5. **Use Process IDs Wisely**: Make them descriptive (`us-east-worker-1`)
6. **Test Failover**: Simulate worker crashes to verify recovery

---

## Troubleshooting

### Workers Not Registering

**Symptom**: Workers start but don't receive shards

**Solution**:
- Verify `coordinatorUrl` is reachable from worker
- Check firewall rules allow HTTP traffic
- Ensure coordinator started first

### High Latency in Distributed Mode

**Symptom**: Commands slow in distributed mode

**Solution**:
- Check network latency between coordinator and workers
- Ensure workers are in same datacenter/region
- Consider increasing `maxShards` per worker to reduce HTTP calls

### Coordinator Succession Not Working

**Symptom**: Workers disconnect when coordinator fails

**Solution**:
- Verify workers have `coordinatorUrl` accessible
- Check workers are reporting heartbeats (every 5s)
- Ensure succession list has multiple entries

---

## Limitations

1. **HTTP-based Communication**: Uses `HttpListener` (simple, not optimal for extreme throughput)
2. **No State Persistence**: Coordinator state is in-memory (lost on crash without succession)
3. **Single Active Coordinator**: Only one coordinator at a time
4. **No Authentication**: HTTP endpoints are unauthenticated (use firewalls)
5. **Platform**: `HttpListener` works best on Windows (use Kestrel for Linux)

---

## Future Enhancements

- gRPC for coordinator ↔ worker communication
- Redis/etcd for state persistence
- Multi-coordinator with Raft consensus
- Kubernetes operator for auto-scaling
- Prometheus metrics export

---

## Additional Resources

- [SHARDING_IMPLEMENTATION.md](../SHARDING_IMPLEMENTATION.md) - Technical implementation details
- [SHARDING_INTEGRATION_GUIDE.md](../SHARDING_INTEGRATION_GUIDE.md) - Step-by-step integration guide
- [ShardCalculator API](API-Reference#shardcalculator) - Guild → shard mapping
- [Examples](Examples#sharding) - Complete examples

---

**Next Steps**: [Performance Optimizations](Performance-Optimizations) | [Rate Limit Monitoring](Rate-Limit-Monitoring)
