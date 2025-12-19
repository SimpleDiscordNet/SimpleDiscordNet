# SimpleDiscordDotNet

A lightweight, dependency-free Discord bot SDK for .NET 10 that provides direct access to Discord API v10 (REST + Gateway).

## Purpose

SimpleDiscordDotNet is designed for developers who want:
- **Zero dependencies** - BCL only, no external packages
- **Performance** - Memory-optimized with Span<T> and modern .NET 10 APIs for 30-50% less GC pressure
- **Simplicity** - Clean, approachable API with builder patterns
- **Modern C#** - Built for .NET 10 with C# 14 features and span-based APIs
- **Production-ready** - Advanced rate limiting, comprehensive error handling, and extensive API coverage

## Key Features

- ✅ Slash commands, components, and modals with attribute-based handlers
- ✅ Source generator for zero-reflection command/component discovery
- ✅ Ambient context for accessing cached guilds, channels, members, roles
- ✅ Comprehensive gateway events for all entity changes
- ✅ Advanced rate limiting with bucket management and monitoring
- ✅ Full Discord API v10 support (messages, reactions, permissions, roles, channels, threads, etc.)
- ✅ Native AOT and trimming compatible
- ✅ Memory-optimized with `Span<T>`, `Memory<T>`, and zero-allocation APIs
- ✅ **Horizontal sharding** - 3 modes: single process, multi-shard, or distributed coordinator/worker

## Quick Example

```csharp
using SimpleDiscordNet;
using SimpleDiscordNet.Commands;
using SimpleDiscordNet.Primitives;

public sealed class AppCommands
{
    [SlashCommand("hello", "Say hello")]
    public async Task HelloAsync(InteractionContext ctx)
        => await ctx.RespondAsync("Hello from SimpleDiscordDotNet!");
}

var bot = DiscordBot.NewBuilder()
    .WithToken(Environment.GetEnvironmentVariable("DISCORD_TOKEN")!)
    .WithIntents(DiscordIntents.Guilds)
    .Build();

await bot.StartAsync();
await Task.Delay(Timeout.Infinite);
```

## Documentation

**📖 Full documentation is available in the [Wiki](./wiki)**

- [Installation](./wiki/Installation.md) - Get started with NuGet or source reference
- [Getting Started](./wiki/Getting-Started.md) - Your first bot in minutes
- [Configuration](./wiki/Configuration.md) - Builder patterns, DI, intents
- [Commands](./wiki/Commands.md) - Slash commands, components, modals
- [Events](./wiki/Events.md) - Gateway events and logging
- [Sharding](./wiki/Sharding.md) - **NEW!** Horizontal scaling with distributed sharding
- [API Reference](./wiki/API-Reference.md) - Complete API documentation
- [Rate Limit Monitoring](./wiki/Rate-Limit-Monitoring.md) - Advanced monitoring and analytics
- [FAQ](./wiki/FAQ.md) - Common questions and troubleshooting

## Installation

Install from NuGet:

```bash
dotnet add package SimpleDiscordDotNet
```

Or via Package Manager:

```powershell
Install-Package SimpleDiscordDotNet
```

## Requirements

- .NET SDK 10.0 or newer
- A Discord bot token from the [Discord Developer Portal](https://discord.com/developers/applications)
- Gateway intents configured as needed

## Contributing

Issues and pull requests are welcome! Please keep the code dependency-free and aligned with the existing style.

## License

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE) and [NOTICE](NOTICE) for details.

---

**Ready to build your Discord bot?** Head to the [Wiki](./wiki) to get started!

## Version History

### v1.2.1 - Sharding Support (2025-12-19)
- ✅ Added 3-mode sharding system: single process, multi-shard, distributed
- ✅ Distributed coordinator/worker architecture with auto-discovery
- ✅ Health monitoring, load balancing, coordinator succession
- ✅ Cross-shard entity cache queries
- ✅ Shard-aware InteractionContext for commands
- ✅ Full AoT compliance with source-generated JSON serialization
- ✅ Zero reflection usage, ready for native compilation
- 📖 See [SHARDING_IMPLEMENTATION.md](SHARDING_IMPLEMENTATION.md) and [SHARDING_INTEGRATION_GUIDE.md](SHARDING_INTEGRATION_GUIDE.md)
