# Frequently Asked Questions

Common questions and answers about SimpleDiscordNet.

## General Questions

### What is SimpleDiscordNet?

SimpleDiscordNet is a modern, lightweight .NET library for building Discord bots. It focuses on simplicity, performance, and ease of use while providing all the essential features for bot development.

### What makes SimpleDiscordNet different?

- **Lightweight**: Minimal dependencies and memory-optimized performance
- **Source Generators**: Compile-time code generation for zero reflection overhead
- **Modern .NET**: Built for .NET 10 with Span<T>, Memory<T>, and native AOT support
- **High Performance**: 30-50% less GC pressure through span-based APIs
- **Simple API**: Intuitive, easy-to-learn interface
- **Type-safe**: Strong typing throughout the library

### What .NET versions are supported?

SimpleDiscordNet requires .NET 10.0 or later for optimal performance with modern span-based APIs.

### Is SimpleDiscordNet production-ready?

Yes! SimpleDiscordNet is stable and suitable for production use. The library follows semantic versioning.

## Getting Started

### How do I get a bot token?

1. Go to the [Discord Developer Portal](https://discord.com/developers/applications)
2. Click "New Application"
3. Go to the "Bot" section
4. Click "Add Bot"
5. Copy the token under "TOKEN"

**Important**: Never share your bot token publicly!

### What intents do I need?

It depends on your bot's functionality:

- **Basic bot**: `DiscordIntents.Guilds`
- **Read messages**: Add `DiscordIntents.GuildMessages`
- **Read message content**: Add `DiscordIntents.MessageContent` (privileged)
- **Track members**: Add `DiscordIntents.GuildMembers` (privileged)

Privileged intents must be enabled in the Discord Developer Portal.

### My bot isn't responding to commands

Check these common issues:

1. Did you call `await bot.StartAsync()`?
2. Did you register commands with `commandService.RegisterCommands()`?
3. Did you sync commands with `await commandService.SyncCommandsAsync()`?
4. Are you using the correct guild ID for guild commands?
5. Does the bot have the `applications.commands` scope?

### Commands take an hour to appear

This happens when using **global commands**. Use guild commands during development:

```csharp
await commandService.SyncCommandsAsync(guildId: YOUR_GUILD_ID);
```

Guild commands update instantly!

## Commands

### How do I create a slash command?

```csharp
[SlashCommand("hello", "Says hello")]
public class HelloCommand
{
    public async Task ExecuteAsync(InteractionContext context)
    {
        await context.RespondAsync("Hello!");
    }
}
```

Then register it:

```csharp
commandService.RegisterCommands(Assembly.GetExecutingAssembly());
await commandService.SyncCommandsAsync();
```

### How do I add command parameters?

Add parameters to your `ExecuteAsync` method:

```csharp
public async Task ExecuteAsync(InteractionContext context, string message, User user)
{
    await context.RespondAsync($"{user.Username}: {message}");
}
```

### Can I have optional parameters?

Yes! Use default values:

```csharp
public async Task ExecuteAsync(InteractionContext context, string message, bool ephemeral = false)
{
    await context.RespondAsync(message, ephemeral: ephemeral);
}
```

### How do I create command groups?

Use `[SlashCommandGroup]`:

```csharp
[SlashCommandGroup("admin", "Admin commands")]
public class AdminCommands
{
    [SlashCommand("ban", "Ban a user")]
    public async Task BanAsync(InteractionContext context, User user) { }

    [SlashCommand("kick", "Kick a user")]
    public async Task KickAsync(InteractionContext context, User user) { }
}
```

### My command takes longer than 3 seconds

Use the `[Defer]` attribute:

```csharp
[SlashCommand("process", "Process data")]
[Defer]
public class ProcessCommand
{
    public async Task ExecuteAsync(InteractionContext context)
    {
        // Long operation
        await Task.Delay(10000);
        await context.EditResponseAsync("Done!");
    }
}
```

## Events

### How do I handle messages?

Subscribe to the `MessageCreated` event:

```csharp
bot.Events.MessageCreated += async (sender, message) =>
{
    if (message.Author.IsBot) return;
    Console.WriteLine($"{message.Author.Username}: {message.Content}");
};
```

**Note**: Requires `MessageContent` intent for content access.

### I can't see message content

Enable the `MessageContent` privileged intent:

1. In Discord Developer Portal, enable "Message Content Intent"
2. In your code, add the intent:

```csharp
var options = new DiscordBotOptions
{
    Intents = DiscordIntents.Guilds
        | DiscordIntents.GuildMessages
        | DiscordIntents.MessageContent
};
```

### How do I detect when a user joins?

Subscribe to `GuildMemberAdded`:

```csharp
bot.Events.GuildMemberAdded += async (sender, member) =>
{
    Console.WriteLine($"{member.User.Username} joined!");
};
```

**Note**: Requires `GuildMembers` privileged intent.

### Can I have multiple event handlers?

Yes! Subscribe multiple handlers to the same event:

```csharp
bot.Events.MessageCreated += LogMessage;
bot.Events.MessageCreated += CheckSpam;
bot.Events.MessageCreated += UpdateStats;
```

## Embeds

### How do I create an embed?

Use `EmbedBuilder`:

```csharp
var embed = new EmbedBuilder()
    .WithTitle("Title")
    .WithDescription("Description")
    .WithColor(DiscordColor.Blue)
    .Build();

await channel.SendMessageAsync(embed: embed);
```

### How many fields can an embed have?

Maximum 25 fields per embed.

### Can I send multiple embeds?

Yes, up to 10 embeds per message:

```csharp
await channel.SendMessageAsync(embeds: new[] { embed1, embed2, embed3 });
```

### What are embed character limits?

- Title: 256 characters
- Description: 4096 characters
- Field name: 256 characters
- Field value: 1024 characters
- Footer: 2048 characters
- Total: 6000 characters

## Permissions

### How do I check user permissions?

```csharp
if (context.Member.Permissions.HasFlag(PermissionFlags.Administrator))
{
    // User is admin
}
```

### How do I check bot permissions?

```csharp
var botMember = await context.Guild.GetMemberAsync(bot.CurrentUser.Id);
if (botMember.Permissions.HasFlag(PermissionFlags.BanMembers))
{
    // Bot can ban members
}
```

### How do I give my bot permissions?

When creating the invite link, select the permissions your bot needs. Or use this URL format:

```
https://discord.com/api/oauth2/authorize?client_id=YOUR_BOT_ID&permissions=PERMISSION_INTEGER&scope=bot%20applications.commands
```

## Performance

### Is SimpleDiscordNet optimized for performance?

Yes! SimpleDiscordNet achieves **30-50% reduction in GC pressure** through:
- Span<T> and Memory<T> APIs for zero-allocation operations
- Direct UTF8 JSON serialization/deserialization
- Optimized WebSocket processing
- LINQ-free collection operations

See [Performance Optimizations](Performance-Optimizations) for detailed benchmarks.

### Does SimpleDiscordNet use caching?

Yes, entity caching is enabled by default and optimized with span-based operations for minimal allocations.

### How does source generation improve performance?

Source generation creates optimal code at compile time:
- Zero reflection overhead
- Inline helper methods instead of LINQ
- ~50 fewer allocations per command invocation
- Faster startup time

### Can I use SimpleDiscordNet with AOT?

Yes! SimpleDiscordNet is fully compatible with Native AOT compilation:
- Source-generated command handlers
- Source-generated JSON serialization
- Zero reflection dependencies
- All span-based optimizations are AOT-friendly

### Are there span-based API overloads?

Yes! Many formatting methods have span-based overloads:

```csharp
// Traditional (still works)
string mention = DiscordFormatting.MentionUser(userId);

// Zero-allocation (for strings < 256 chars)
ReadOnlySpan<char> userIdSpan = userId.AsSpan();
string mention = DiscordFormatting.MentionUser(userIdSpan);
```

Methods with span overloads: Bold, Italic, Code, MentionUser, MentionChannel, MentionRole, and more.

## Troubleshooting

### The bot connects but doesn't respond

1. Check you have the `applications.commands` scope
2. Verify commands are synced with `SyncCommandsAsync()`
3. Check the bot has permission to respond in the channel
4. Look for errors in console output

### "Unknown Interaction" error

This means Discord didn't receive a response within 3 seconds. Use `[Defer]` for long-running commands:

```csharp
[Defer]
public async Task ExecuteAsync(InteractionContext context) { }
```

### Bot disconnects frequently

Check your internet connection and firewall settings. Discord's gateway requires a stable connection.

### Rate limit errors

You're making too many API requests. Implement rate limiting or caching to reduce requests.

### Missing Access error

The bot lacks permissions. Check:
1. Bot has required permissions in the guild
2. Channel-specific permission overrides
3. Role hierarchy (bot role is high enough)

## Best Practices

### Should I use global or guild commands?

- **Development**: Guild commands (instant updates)
- **Production**: Global commands (work in all guilds)

### How do I store my bot token securely?

Use environment variables or a configuration file (excluded from version control):

```csharp
var token = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
```

Never commit tokens to Git!

### How do I handle errors?

Always use try-catch blocks:

```csharp
try
{
    await command.ExecuteAsync();
}
catch (Exception ex)
{
    await context.RespondAsync($"Error: {ex.Message}", ephemeral: true);
}
```

### Should I respond ephemerally?

Use ephemeral responses for:
- Error messages
- Permission denials
- User-specific information
- Commands that don't need to be public

## Getting Help

### Where can I get help?

- Check the [documentation](Home.md)
- Review [examples](Getting-Started.md)
- Search existing GitHub issues
- Create a new GitHub issue

### How do I report a bug?

Create an issue on GitHub with:
1. Description of the problem
2. Steps to reproduce
3. Expected vs actual behavior
4. Code samples
5. Library version

### Can I contribute?

Yes! Contributions are welcome. Check the repository for contribution guidelines.

## Sharding

### When should I use sharding?

- **< 2,500 guilds**: No sharding needed (SingleProcess mode)
- **2,500-10,000 guilds**: Use SingleProcess with sharding (manual)
- **10,000+ guilds**: Use Distributed mode (coordinator + workers)

Discord **requires** sharding at 2,500 guilds.

### How do I enable sharding?

**SingleProcess with Sharding**:
```csharp
var bot = new DiscordBot.Builder(token, intents)
    .WithSharding(shardId: 0, totalShards: 4)
    .Build();
```

**Distributed Mode**:
```csharp
// Coordinator
var coordinator = new DiscordBot.Builder(token, intents)
    .WithDistributedCoordinator("http://+:8080/", isOriginalCoordinator: true)
    .Build();

// Worker
var worker = new DiscordBot.Builder(token, intents)
    .WithDistributedWorker("http://coordinator:8080/", "http://+:8081/", "worker-1")
    .Build();
```

See [Sharding](Sharding.md) for complete guide.

### How do I access shard information in commands?

```csharp
[SlashCommand("info", "Get shard info")]
public async Task InfoAsync(InteractionContext ctx)
{
    var shardId = ctx.ShardId;
    await ctx.RespondAsync($"Shard {shardId}");
}
```

### Can I query data across all shards?

Yes! In distributed mode:
```csharp
var allGuilds = await bot.Cache.GetGuildsAsync(); // Queries all workers
```

## Next Steps

- [Getting Started](Getting-Started.md) - Build your first bot
- [Commands](Commands.md) - Create slash commands
- [Sharding](Sharding.md) - Scale horizontally with sharding
- [Events](Events.md) - Handle Discord events
- [API Reference](API-Reference.md) - Full API documentation
