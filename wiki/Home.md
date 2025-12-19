# Welcome to SimpleDiscordNet

A tiny, dependency-free Discord bot SDK for .NET 10 that talks directly to the Discord API v10 (REST + Gateway).

## 🚀 Quick Links

- **[Getting Started](Getting-Started)** - Build your first bot in minutes
- **[Installation](Installation)** - NuGet and source reference setup
- **[Examples](Examples)** - Complete working bot examples
- **[API Reference](API-Reference)** - Quick method reference

## 📦 Installation

```bash
dotnet add package SimpleDiscordDotNet
```

**NuGet:** https://www.nuget.org/packages/SimpleDiscordDotNet/
**GitHub:** https://github.com/SimpleDiscordNet/SimpleDiscordNet

## ✨ Key Features

### Core Features
- **Slash commands with full option support** - Attribute-based handlers with constraints, choices, autocomplete, and all Discord option types (STRING, INTEGER, NUMBER, BOOLEAN, USER, CHANNEL, ROLE)
- **Components and modals** - Buttons, select menus, and forms
- **Zero external dependencies** - BCL only
- **Builder pattern** and DI-friendly
- **Global static event hub** for logs and domain events
- **Source generator** for zero-reflection command/component discovery
- **Native AOT compatible** - Full trimming and AOT support

### Rich Discord API v10 Support
- **Messages:** send, edit, delete, bulk delete, pin/unpin
- **Reactions:** add, remove, clear, get users who reacted
- **Embeds & Components:** rich embeds, buttons, selects, modals
- **Permissions:** channel overwrites (per-role and per-member)
- **Roles:** create, edit, delete, assign/remove, permission checking
- **Channels:** create, edit, delete, categories, text, voice
- **Threads:** create, join, leave, add/remove members
- **Moderation:** kick, ban, unban, role assignment
- **Events:** comprehensive gateway events for all entity changes
- **Rate Limiting:** enterprise-grade with bucket tracking, monitoring, and zero message loss

### Ambient DiscordContext
- Access cached **Guilds/Channels/Members/Users/Roles** from anywhere
- **Filtered collections** (Categories, TextChannels, VoiceChannels, Threads)
- **Helper methods** for querying by guild, category, role
- **Type-safe, read-only snapshots**

## 📚 Documentation

### Getting Started
- **[Getting Started](Getting-Started)** - Your first Discord bot
- **[Installation](Installation)** - Setup and configuration
- **[Dependency Injection](Dependency-Injection)** - Using with DI containers

### Commands & Interactions
- **[Slash Commands](Slash-Commands)** - Command creation and handling
- **[Components & Modals](Components-and-Modals)** - Buttons, selects, and forms
- **[Defer Attribute](Defer-Attribute)** - Handling long-running operations

### Messages & Communication
- **[Messages & Embeds](Messages-and-Embeds)** - Sending rich messages
- **[Reactions](Reactions)** - Adding and managing reactions

### Server Management
- **[Permissions & Roles](Permissions-and-Roles)** - Role and permission management
- **[Channels & Categories](Channels-and-Categories)** - Channel organization
- **[Threads](Threads)** - Thread creation and management
- **[Moderation](Moderation)** - Member moderation tools

### Data Access & Events
- **[DiscordContext](DiscordContext)** - Ambient data access from anywhere
- **[Events](Events)** - Handling Discord gateway events

### Advanced Topics
- **[Rate Limit Monitoring](Rate-Limit-Monitoring)** - Enterprise-grade rate limiting with monitoring
- **[AOT & Trimming](AOT-and-Trimming)** - Native AOT compatibility
- **[API Reference](API-Reference)** - Quick method reference
- **[FAQ](FAQ)** - Common questions and troubleshooting

## 💡 Quick Example

```csharp
using SimpleDiscordNet;
using SimpleDiscordNet.Commands;
using SimpleDiscordNet.Primitives;

// Define your commands with options
public sealed class MyCommands
{
    [SlashCommand("greet", "Greet someone")]
    public async Task GreetAsync(
        InteractionContext ctx,
        [CommandOption("name", "Person's name", MinLength = 2, MaxLength = 32)]
        string name,
        [CommandOption("style", "Greeting style", Choices = "Friendly:👋,Formal:🤝,Casual:✌️")]
        string style)
    {
        await ctx.RespondAsync($"{style} Hello, {name}!", ephemeral: true);
    }
}

// Build and start your bot
var bot = DiscordBot.NewBuilder()
    .WithToken(Environment.GetEnvironmentVariable("DISCORD_TOKEN")!)
    .WithIntents(DiscordIntents.Guilds)
    .WithDevelopmentMode(true)
    .WithDevelopmentGuild("YOUR_DEV_GUILD_ID")
    .Build();

await bot.StartAsync();
await Task.Delay(Timeout.Infinite);
```

## 🎯 Design Philosophy

SimpleDiscordNet is designed with these principles:

1. **Simplicity First** - Extremely easy to use with minimal boilerplate
2. **Zero Dependencies** - No external packages, only BCL
3. **Performance** - Optimized for low memory usage and speed
4. **Modern C#** - Built with C# 14 and .NET 10 features
5. **AOT Ready** - Compatible with Native AOT compilation
6. **Well Documented** - Every public method has XML docs with examples

## 🤝 Contributing

Issues and PRs are welcome on GitHub! Keep the code dependency-free and aligned with the existing style.

## 📄 License

SimpleDiscordNet is licensed under the **Apache License, Version 2.0**.

---

**Ready to get started?** Head to **[Getting Started](Getting-Started)** to build your first bot!
