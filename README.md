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

**📖 Full documentation is available in the [Wiki](https://github.com/YourUsername/SimpleDiscordDotNet/wiki)**

- [Installation](https://github.com/YourUsername/SimpleDiscordDotNet/wiki/Installation) - Get started with NuGet or source reference
- [Getting Started](https://github.com/YourUsername/SimpleDiscordDotNet/wiki/Getting-Started) - Your first bot in minutes
- [Configuration](https://github.com/YourUsername/SimpleDiscordDotNet/wiki/Configuration) - Builder patterns, DI, intents
- [Commands](https://github.com/YourUsername/SimpleDiscordDotNet/wiki/Commands) - Slash commands, components, modals
- [Events](https://github.com/YourUsername/SimpleDiscordDotNet/wiki/Events) - Gateway events and logging
- [API Reference](https://github.com/YourUsername/SimpleDiscordDotNet/wiki/API-Reference) - Complete API documentation
- [Rate Limit Monitoring](https://github.com/YourUsername/SimpleDiscordDotNet/wiki/Rate-Limit-Monitoring) - Advanced monitoring and analytics
- [FAQ](https://github.com/YourUsername/SimpleDiscordDotNet/wiki/FAQ) - Common questions and troubleshooting

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

**Ready to build your Discord bot?** Head to the [Wiki](https://github.com/YourUsername/SimpleDiscordDotNet/wiki) to get started!
