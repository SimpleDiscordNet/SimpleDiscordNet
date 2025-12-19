# Distributed Sharding Implementation Guide

This document provides the complete architecture and implementation plan for zero-dependency distributed sharding with automatic load balancing, failover, and coordinator resumption.

## Files Created ✅

1. `Sharding/ShardMode.cs` - Enum for SingleProcess vs Distributed
2. `Sharding/ShardStatus.cs` - Shard connection status enum
3. `Sharding/ShardInfo.cs` - Public API for shard information
4. `Sharding/ShardContextAttribute.cs` - Attribute for command injection
5. `Sharding/ShardCalculator.cs` - Span-based guild→shard calculation
6. `Sharding/Models/CoordinatorResumption.cs` - Coordinator recovery models
7. `Sharding/Models/ShardingModels.cs` - All coordination protocol models

## Files To Create (Implementation Required)

### Core Sharding (Single Process)
```
Sharding/
├── Shard.cs                        # Wrapper around GatewayClient with metrics
├── ShardManager.cs                 # Manages local shards, spawning, lifecycle
└── ShardCollection.cs              # Thread-safe shard storage
```

### Distributed Coordination
```
Sharding/Distributed/
├── ShardCoordinator.cs             # Main coordinator logic + resumption
├── ShardHttpServer.cs              # HttpListener API server
├── ShardHttpClient.cs              # Span-optimized HTTP client
├── PeerNode.cs                     # Represents remote process
├── HealthMonitor.cs                # Health checks + failure detection
├── LoadBalancer.cs                 # Metrics analysis + rebalancing
├── DistributedCache.cs             # Cross-process cache queries
├── SuccessionManager.cs            # Coordinator succession + elections
└── CoordinatorResumptionHandler.cs # Original coordinator recovery
```

## Complete Architecture

### 1. Coordinator Resumption Flow

```
Step 1: Original Coordinator (Machine 1) crashes
- Temporary Coordinator (Machine 2) takes over via succession
- Machine 2 becomes active coordinator
- Succession order: [2, 3, 4]

Step 2: Machine 1 recovers and reconnects
- Machine 1 detects it was the original coordinator (stored in local state)
- Machine 1 sends: POST /request-resumption to Machine 2
  {
    "original_coordinator_id": "machine-1",
    "original_coordinator_url": "http://192.168.1.10:5000",
    "timestamp": 1703012345678
  }

Step 3: Temporary Coordinator validates and prepares handoff
- Machine 2 verifies Machine 1's identity and original coordinator status
- Machine 2 gathers complete cluster state:
  * Succession order
  * All peer nodes and their shards
  * Shard assignments
  * Recent metrics and health data
- Machine 2 responds with: HandoffData containing full state

Step 4: Original Coordinator receives state and resumes role
- Machine 1 loads cluster state from handoff
- Machine 1 becomes active coordinator
- Machine 1 rebuilds succession order: [1, 2, 3, 4] (Machine 1 first again)

Step 5: Announce resumption to all workers
- Machine 1 broadcasts to ALL peers: POST /coordinator-resumed
  {
    "resumed_coordinator_id": "machine-1",
    "resumed_coordinator_url": "http://192.168.1.10:5000",
    "previous_coordinator_id": "machine-2",
    "succession_order": [
      { "position": 1, "process_id": "machine-1", "url": "...", "is_original_coordinator": true },
      { "position": 2, "process_id": "machine-2", "url": "...", "is_original_coordinator": false },
      ...
    ],
    "message": "Original coordinator machine-1 has resumed. Please re-register."
  }

Step 6: All workers re-register with resumed coordinator
- Each worker receives announcement
- Each worker sends: POST /register to Machine 1
- Machine 1 assigns them to succession order based on registration order
- New succession established: [1, 2, 3, 4]

Step 7: Cluster stabilized with original coordinator
- Machine 1 is coordinator again
- All workers have updated succession order
- Metrics and health monitoring resume normally
```

### 2. Coordinator Endpoints

```csharp
// Worker Management
POST   /register                    // Worker joins cluster
POST   /unregister                  // Worker leaves gracefully
POST   /metrics                     // Worker reports metrics (every 5s)
GET    /cluster-state               // Get full cluster state

// Health & Monitoring
GET    /health                      // Health check
POST   /heartbeat                   // Worker heartbeat

// Shard Management
POST   /assign-shards               // Coordinator assigns shards
POST   /prepare-migration           // Prepare shard for migration
POST   /accept-migration            // Accept migrated shard
POST   /migration-complete          // Confirm migration done

// Coordination & Succession
POST   /succession-update           // Broadcast succession changes
POST   /become-coordinator          // Trigger coordinator election
POST   /request-resumption          // Original coordinator requests to resume
POST   /coordinator-resumed         // Announce coordinator resumed
POST   /handoff-state               // Transfer state to resuming coordinator

// Data Queries (Cross-Shard)
GET    /guilds/{id}                 // Query guild
GET    /channels/{id}               // Query channel
GET    /members/{guildId}/{userId}  // Query member
```

### 3. Key Implementation Details

#### ShardCoordinator.cs (Coordinator Resumption Logic)
```csharp
public sealed class ShardCoordinator
{
    private readonly string _processId;
    private readonly string _listenUrl;
    private readonly bool _isOriginalCoordinator; // Set on first start
    private volatile bool _isActiveCoordinator;
    private readonly List<SuccessionEntry> _successionOrder = new();
    private readonly Dictionary<string, PeerNode> _peers = new();

    // When this process starts
    public async Task StartAsync(string? coordinatorUrl)
    {
        if (coordinatorUrl == null)
        {
            // No coordinator URL = I'm the original coordinator
            _isOriginalCoordinator = true;
            _isActiveCoordinator = true;
            _successionOrder.Add(new SuccessionEntry(1, _processId, _listenUrl, true));
            await _httpServer.StartAsync();
        }
        else
        {
            // Connect to existing coordinator
            await RegisterWithCoordinatorAsync(coordinatorUrl);
        }
    }

    // Handle original coordinator requesting to resume
    public async Task<CoordinatorHandoffData> HandleResumptionRequestAsync(
        CoordinatorResumptionRequest request)
    {
        if (!_isActiveCoordinator)
            throw new InvalidOperationException("Not the active coordinator");

        _logger.Info($"Original coordinator {request.OriginalCoordinatorId} requesting resumption");

        // Gather complete cluster state
        var handoffData = new CoordinatorHandoffData(
            SuccessionOrder: _successionOrder.ToList(),
            PeerNodes: _peers.Values.Select(p => p.ToState()).ToList(),
            ShardAssignments: GetAllShardAssignments(),
            TotalShards: _totalShards,
            ClusterState: GetClusterState(),
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        );

        // Step down as coordinator
        _isActiveCoordinator = false;

        _logger.Info($"Handing off coordinator role to {request.OriginalCoordinatorId}");

        return handoffData;
    }

    // Resume coordinator role after recovery
    public async Task ResumeCoordinatorRoleAsync(CoordinatorHandoffData handoffData)
    {
        if (!_isOriginalCoordinator)
            throw new InvalidOperationException("Only original coordinator can resume");

        _logger.Info("Resuming coordinator role with handoff data");

        // Load cluster state
        _successionOrder.Clear();
        _successionOrder.AddRange(handoffData.SuccessionOrder);

        // Rebuild peer nodes
        _peers.Clear();
        foreach (var peerState in handoffData.PeerNodes)
        {
            var peer = PeerNode.FromState(peerState);
            _peers[peer.ProcessId] = peer;
        }

        _totalShards = handoffData.TotalShards;
        _isActiveCoordinator = true;

        // Put self first in succession
        var selfEntry = _successionOrder.FirstOrDefault(e => e.ProcessId == _processId);
        if (selfEntry != null)
        {
            _successionOrder.Remove(selfEntry);
            _successionOrder.Insert(0, selfEntry with { Position = 1, IsOriginalCoordinator = true });
        }

        // Renumber succession positions
        for (int i = 0; i < _successionOrder.Count; i++)
        {
            _successionOrder[i] = _successionOrder[i] with { Position = i + 1 };
        }

        // Announce resumption to all workers
        await AnnounceResumptionAsync();

        _logger.Info("Coordinator role resumed successfully");
    }

    private async Task AnnounceResumptionAsync()
    {
        var announcement = new CoordinatorResumedAnnouncement(
            ResumedCoordinatorId: _processId,
            ResumedCoordinatorUrl: _listenUrl,
            PreviousCoordinatorId: _successionOrder[1].ProcessId, // 2nd in line was temp coordinator
            SuccessionOrder: _successionOrder.ToList(),
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Message: $"Original coordinator {_processId} has resumed. Please re-register to establish new succession order."
        );

        // Broadcast to all peers
        var tasks = _peers.Values.Select(peer =>
            _httpClient.PostAsync(peer.Url, "/coordinator-resumed", announcement)
        );

        await Task.WhenAll(tasks);
    }

    // Worker receives resumption announcement
    public async Task HandleCoordinatorResumedAsync(CoordinatorResumedAnnouncement announcement)
    {
        _logger.Info($"Original coordinator {announcement.ResumedCoordinatorId} has resumed. Re-registering...");

        // Update our knowledge of coordinator
        _coordinatorUrl = announcement.ResumedCoordinatorUrl;

        // Re-register with resumed coordinator
        await RegisterWithCoordinatorAsync(_coordinatorUrl);

        _logger.Info("Re-registration complete with resumed coordinator");
    }
}
```

#### SuccessionManager.cs (Succession Logic)
```csharp
public sealed class SuccessionManager
{
    private readonly List<SuccessionEntry> _succession = new();
    private readonly object _lock = new();

    // Add worker to succession (in order of connection)
    public int AddWorker(string processId, string url, bool isOriginalCoordinator)
    {
        lock (_lock)
        {
            int position = _succession.Count + 1;
            _succession.Add(new SuccessionEntry(position, processId, url, isOriginalCoordinator));
            return position;
        }
    }

    // Remove worker from succession
    public void RemoveWorker(string processId)
    {
        lock (_lock)
        {
            _succession.RemoveAll(e => e.ProcessId == processId);

            // Renumber positions
            for (int i = 0; i < _succession.Count; i++)
            {
                _succession[i] = _succession[i] with { Position = i + 1 };
            }
        }
    }

    // Get next coordinator in succession
    public SuccessionEntry? GetNextCoordinator()
    {
        lock (_lock)
        {
            // Position 1 is current coordinator, position 2 is next
            return _succession.FirstOrDefault(e => e.Position == 2);
        }
    }

    // Get snapshot of succession order
    public List<SuccessionEntry> GetSuccession()
    {
        lock (_lock)
        {
            return _succession.ToList();
        }
    }
}
```

### 4. Configuration

```csharp
// Update DiscordBotOptions
public sealed record DiscordBotOptions
{
    public ShardMode ShardMode { get; init; } = ShardMode.SingleProcess;
    public int? CoordinationPort { get; init; }
    public string? CoordinatorUrl { get; init; }
    public int? MaxShardsPerProcess { get; init; }
    public int MetricsInterval { get; init; } = 5; // seconds
    public double CpuThreshold { get; init; } = 80.0; // %
    public int LatencyThreshold { get; init; } = 150; // ms
}

// Update DiscordBot.Builder
public Builder WithSharding(ShardMode mode)
public Builder WithCoordination(int listenPort, string? coordinator = null)
public Builder WithMaxShards(int maxShards)
```

### 5. Example Usage

```csharp
// Machine 1 - Original Coordinator
var bot1 = DiscordBot.NewBuilder()
    .WithToken(token)
    .WithSharding(ShardMode.Distributed)
    .WithCoordination(listenPort: 5000) // No coordinator = I'm original
    .Build();

// Machine 2 - Worker
var bot2 = DiscordBot.NewBuilder()
    .WithToken(token)
    .WithSharding(ShardMode.Distributed)
    .WithCoordination(listenPort: 5000, coordinator: "http://192.168.1.10:5000")
    .Build();

// Machine 1 crashes, Machine 2 becomes coordinator
// Machine 1 recovers:
// - Detects it was original coordinator (stored in local state file)
// - Requests resumption from Machine 2
// - Machine 2 hands off state
// - Machine 1 announces resumption
// - All workers re-register
// - Succession re-established with Machine 1 first
```

## Implementation Priority

1. ✅ Basic types (ShardMode, ShardStatus, ShardInfo, etc.)
2. ⏳ Shard and ShardManager (single-process)
3. ⏳ ShardHttpServer and ShardHttpClient
4. ⏳ ShardCoordinator with registration
5. ⏳ SuccessionManager and failover
6. ⏳ CoordinatorResumptionHandler
7. ⏳ LoadBalancer and metrics
8. ⏳ HealthMonitor and failure detection
9. ⏳ DistributedCache and cross-process queries
10. ⏳ Source generator updates
11. ⏳ Documentation

## Next Steps

Continue implementation with Shard.cs and ShardManager.cs for single-process sharding, then build out the distributed coordination layer.

All span optimizations, thread safety, nullability annotations, and comprehensive logging should be implemented as shown in the examples above.
