# Commands

SimpleDiscordNet provides a powerful command system with automatic registration and source generation.

## Quick Start

Create a command class:

```csharp
using SimpleDiscordNet.Commands;

[SlashCommand("hello", "Says hello")]
public class HelloCommand
{
    public async Task ExecuteAsync(InteractionContext context)
    {
        await context.RespondAsync("Hello, World!");
    }
}
```

Register commands:

```csharp
var commandService = new CommandService(bot);
commandService.RegisterCommands(Assembly.GetExecutingAssembly());
await commandService.SyncCommandsAsync();
```

## Command Attributes

### SlashCommand

Define a slash command:

```csharp
[SlashCommand("ping", "Check bot latency")]
public class PingCommand
{
    public async Task ExecuteAsync(InteractionContext context)
    {
        await context.RespondAsync($"Pong! Latency: {context.Bot.Latency}ms");
    }
}
```

### SlashCommandGroup

Create command groups:

```csharp
[SlashCommandGroup("admin", "Admin commands")]
public class AdminCommands
{
    [SlashCommand("ban", "Ban a user")]
    public async Task BanAsync(InteractionContext context, User user, string reason)
    {
        // Ban logic
        await context.RespondAsync($"Banned {user.Username}");
    }

    [SlashCommand("kick", "Kick a user")]
    public async Task KickAsync(InteractionContext context, User user)
    {
        // Kick logic
        await context.RespondAsync($"Kicked {user.Username}");
    }
}
```

## Command Parameters

Commands automatically parse parameters:

```csharp
[SlashCommand("echo", "Repeat a message")]
public class EchoCommand
{
    public async Task ExecuteAsync(
        InteractionContext context,
        string message,
        bool ephemeral = false)
    {
        await context.RespondAsync(message, ephemeral: ephemeral);
    }
}
```

### Supported Parameter Types

- `string` - Text input
- `int`, `long` - Numbers
- `bool` - True/false
- `User` - Discord user
- `Channel` - Discord channel
- `Role` - Discord role
- `double` - Decimal numbers

## Defer Responses

Use `[Defer]` for long-running commands:

```csharp
[SlashCommand("process", "Process data")]
[Defer]
public class ProcessCommand
{
    public async Task ExecuteAsync(InteractionContext context)
    {
        // Long-running operation
        await Task.Delay(5000);

        // Respond after deferring
        await context.EditResponseAsync("Processing complete!");
    }
}
```

### Defer Options

```csharp
[Defer(ephemeral: true)] // Defer with ephemeral response
```

## Context Object

The `InteractionContext` provides:

- `Bot` - Access to the bot instance
- `Interaction` - Raw interaction data
- `Guild` - Current guild (if in guild)
- `Channel` - Current channel
- `User` - User who invoked command
- `Member` - Member object (if in guild)

## Response Methods

### Basic Responses

```csharp
// Simple text response
await context.RespondAsync("Hello!");

// Ephemeral response (only user can see)
await context.RespondAsync("Secret message", ephemeral: true);

// Response with embed
await context.RespondAsync(embed: myEmbed);
```

### Editing Responses

```csharp
// Edit deferred response
await context.EditResponseAsync("Updated message");

// Edit with embed
await context.EditResponseAsync(embed: newEmbed);
```

### Follow-up Messages

```csharp
// Send follow-up
await context.FollowUpAsync("Additional message");
```

## Source Generation

SimpleDiscordNet uses source generators to create optimal command registration code:

```csharp
// Generated code handles:
// - Command metadata
// - Parameter parsing
// - Type conversion
// - Error handling
```

The generator runs at compile time, ensuring:
- Zero reflection overhead
- Type-safe parameter handling
- Compile-time validation

## Guild vs Global Commands

### Global Commands

```csharp
// Sync to all guilds (can take up to 1 hour)
await commandService.SyncCommandsAsync();
```

### Guild Commands

```csharp
// Sync to specific guild (instant)
await commandService.SyncCommandsAsync(guildId: 123456789);
```

**Recommendation:** Use guild commands during development for instant updates.

## Command Permissions

Check permissions in your command:

```csharp
[SlashCommand("ban", "Ban a user")]
public class BanCommand
{
    public async Task ExecuteAsync(InteractionContext context, User user)
    {
        if (!context.Member.Permissions.HasFlag(PermissionFlags.BanMembers))
        {
            await context.RespondAsync("You don't have permission!", ephemeral: true);
            return;
        }

        // Ban logic
    }
}
```

## Error Handling

Handle errors gracefully:

```csharp
public async Task ExecuteAsync(InteractionContext context)
{
    try
    {
        // Command logic
    }
    catch (Exception ex)
    {
        await context.RespondAsync($"Error: {ex.Message}", ephemeral: true);
    }
}
```

## Best Practices

1. **Use descriptive names and descriptions** - Help users understand commands
2. **Defer long operations** - Prevent interaction timeouts
3. **Use ephemeral responses for errors** - Don't clutter channels
4. **Validate permissions** - Check user permissions before executing
5. **Handle errors** - Always catch and report errors gracefully
6. **Use guild commands for testing** - Get instant updates

## Next Steps

- [Events](Events.md) - Handle Discord events
- [Embeds](Embeds.md) - Create rich embed messages
- [API Reference](API-Reference.md) - Full API documentation
