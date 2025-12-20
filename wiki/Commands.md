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

Commands support automatic parameter binding. The `[CommandOption]` attribute is **optional** (added in v1.4.3) but recommended for customization:

```csharp
// With [CommandOption] - recommended for custom descriptions and constraints
[SlashCommand("profile", "Update your profile")]
public async Task ProfileAsync(
    InteractionContext ctx,
    [CommandOption("name", "Your display name")]
    string name,
    [CommandOption("age", "Your age")]
    int age)
{
    await ctx.RespondAsync($"Profile updated: {name}, {age} years old");
}

// Without [CommandOption] - parameters auto-detected from method signature (v1.4.3+)
[SlashCommand("ban", "Ban a user")]
public async Task BanAsync(InteractionContext ctx, ulong userId, string reason)
{
    await ctx.RespondAsync($"Banned user {userId}: {reason}");
}
```

### Supported Parameter Types

- `string` - Text input
- `int`, `long`, `ulong` - Integer numbers (ulong support added in v1.4.3)
- `double`, `float` - Decimal numbers
- `bool` - True/false
- `User` - Discord user (resolved from cache)
- `Channel` - Discord channel (resolved from cache)
- `Role` - Discord role (resolved from cache)

**Note:** When using `ulong` for Discord IDs (user IDs, role IDs, etc.), the source generator automatically maps them to Discord's integer type.

### Parameter Constraints

Add validation constraints to your parameters:

#### String Constraints

```csharp
[SlashCommand("register", "Register with a username")]
public async Task RegisterAsync(
    InteractionContext ctx,
    [CommandOption("username", "Choose a username", MinLength = 3, MaxLength = 20)]
    string username)
{
    await ctx.RespondAsync($"Registered as {username}");
}
```

#### Numeric Constraints

```csharp
[SlashCommand("setage", "Set your age")]
public async Task SetAgeAsync(
    InteractionContext ctx,
    [CommandOption("age", "Your age", MinValue = 13, MaxValue = 120)]
    int age)
{
    await ctx.RespondAsync($"Age set to {age}");
}
```

#### Channel Type Filtering

```csharp
[SlashCommand("announce", "Send announcement")]
public async Task AnnounceAsync(
    InteractionContext ctx,
    [CommandOption("channel", "Text channel for announcement", ChannelTypes = "0")]
    Channel channel,
    [CommandOption("message", "Announcement message")]
    string message)
{
    // ChannelTypes: "0" = GUILD_TEXT, "2" = GUILD_VOICE, etc.
    await ctx.RespondAsync($"Announcement sent to {channel.Name}");
}
```

### Choices (Dropdown Menus)

Provide predefined choices for users. You can use either the inline format or separate attributes:

#### Inline Format

```csharp
[SlashCommand("order", "Order a drink")]
public async Task OrderAsync(
    InteractionContext ctx,
    [CommandOption("size", "Drink size", Choices = "Small:S,Medium:M,Large:L,Extra Large:XL")]
    string size,
    [CommandOption("quantity", "Number of drinks", Choices = "One:1,Two:2,Three:3,Four:4")]
    int quantity)
{
    await ctx.RespondAsync($"Ordered {quantity} {size} drink(s)");
}
```

Format: `"Display Name:value,Display Name 2:value2"`

#### Attribute Format (Recommended for Readability)

```csharp
[SlashCommand("greet", "Greet someone")]
public async Task GreetAsync(
    InteractionContext ctx,
    [CommandOption("name", "Name to greet")]
    string name,
    [CommandOption("style", "Greeting style")]
    [CommandChoice("Formal", "formal")]
    [CommandChoice("Casual", "casual")]
    [CommandChoice("Enthusiastic", "enthusiastic")]
    string style = "casual",
    [CommandOption("ephemeral", "Make the response visible only to you")]
    bool ephemeral = false)
{
    await ctx.RespondAsync($"Hello {name}! (Style: {style})", ephemeral: ephemeral);
}
```

**Note:** Limited to 25 choices maximum per option. `[CommandChoice]` attributes take precedence over the `Choices` property if both are specified.

### Autocomplete

Enable autocomplete for dynamic options:

```csharp
[SlashCommand("search", "Search the database")]
public async Task SearchAsync(
    InteractionContext ctx,
    [CommandOption("query", "Search query", Autocomplete = true)]
    string query)
{
    // Note: You'll need to handle AUTOCOMPLETE interactions separately
    await ctx.RespondAsync($"Searching for: {query}");
}
```

**Note:** Autocomplete requires implementing an autocomplete interaction handler (see Events documentation).

### Optional Parameters

Use nullable types or default values for optional parameters:

```csharp
[SlashCommand("greet", "Greet someone")]
public async Task GreetAsync(
    InteractionContext ctx,
    [CommandOption("name", "Person to greet")]
    string? name = null,
    [CommandOption("emoji", "Add an emoji")]
    bool addEmoji = false)
{
    string greeting = name != null ? $"Hello, {name}!" : "Hello!";
    if (addEmoji) greeting += " 👋";
    await ctx.RespondAsync(greeting);
}
```

### Required vs Optional

Parameters are required by default unless:
- They have a default value
- They are nullable (`string?`, `int?`, `User?`)
- Explicitly marked with `Required = false`

```csharp
[CommandOption("optional", "Optional parameter", Required = false)]
string? optionalParam
```

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

The `InteractionContext` provides access to all interaction-related data:

### Core Properties

- `InteractionId` - Unique interaction identifier
- `InteractionToken` - Token for followup messages
- `ApplicationId` - Bot's application ID
- `GuildId` - Guild ID (if in guild, otherwise null)
- `ChannelId` - Channel ID where interaction occurred

### 🆕 v1.4.0: Direct Entity Access

**Member and Guild objects are now provided directly without cache lookups:**

```csharp
[SlashCommand("whoami", "Get your info")]
public async Task WhoAmIAsync(InteractionContext ctx)
{
    // Direct access to Member - no cache lookup needed!
    DiscordMember? member = ctx.Member;
    if (member is not null)
    {
        string username = member.User.Username;
        ulong[] roles = member.Roles;
        string? nickname = member.Nick;

        await ctx.RespondAsync($"You are {username} (Nick: {nickname ?? "None"}), {roles.Length} roles");
    }

    // Direct access to Guild - no cache lookup needed!
    DiscordGuild? guild = ctx.Guild;
    if (guild is not null)
    {
        await ctx.RespondAsync($"In guild: {guild.Name}");
    }
}
```

### Cache-Based Properties

- `Channel` - Current channel (from cache)

**Note:** Member and Guild are null for DM interactions.

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
