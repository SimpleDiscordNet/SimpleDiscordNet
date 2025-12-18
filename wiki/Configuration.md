# Configuration

Learn how to configure SimpleDiscordNet for your bot's needs.

## Basic Configuration

Configure your bot using `DiscordBotOptions`:

```csharp
var options = new DiscordBotOptions
{
    Token = "YOUR_BOT_TOKEN",
    Intents = DiscordIntents.Guilds | DiscordIntents.GuildMessages
};

var bot = new DiscordBot(options);
```

## Bot Options

### Required Options

- **Token** - Your Discord bot token from the [Discord Developer Portal](https://discord.com/developers/applications)
- **Intents** - Gateway intents your bot needs

### Optional Options

- **EnableCaching** - Enable entity caching (default: true)
- **CacheOptions** - Configure cache behavior
- **LogLevel** - Set logging verbosity
- **MaxRetries** - Maximum API request retries (default: 3)
- **Timeout** - Request timeout in milliseconds

## Gateway Intents

Intents control what events your bot receives:

```csharp
// Minimal intents
var intents = DiscordIntents.Guilds;

// Multiple intents
var intents = DiscordIntents.Guilds
    | DiscordIntents.GuildMessages
    | DiscordIntents.MessageContent;

// All non-privileged intents
var intents = DiscordIntents.AllUnprivileged;
```

### Common Intents

| Intent | Purpose | Privileged |
|--------|---------|-----------|
| `Guilds` | Guild/channel/role events | No |
| `GuildMessages` | Message events in guilds | No |
| `MessageContent` | Access message content | Yes |
| `GuildMembers` | Member join/leave events | Yes |
| `DirectMessages` | DM events | No |

**Note:** Privileged intents must be enabled in the Discord Developer Portal.

## Caching

Control entity caching:

```csharp
var options = new DiscordBotOptions
{
    EnableCaching = true,
    CacheOptions = new CacheOptions
    {
        CacheChannels = true,
        CacheGuilds = true,
        CacheMembers = true,
        CacheRoles = true,
        MaxCacheSize = 10000
    }
};
```

## Dependency Injection

Register with ASP.NET Core:

```csharp
services.AddSingleton<IDiscordBot>(provider =>
{
    var options = new DiscordBotOptions
    {
        Token = configuration["Discord:Token"],
        Intents = DiscordIntents.Guilds | DiscordIntents.GuildMessages
    };
    return new DiscordBot(options);
});
```

## Environment Variables

Store your token securely:

```csharp
var token = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
var options = new DiscordBotOptions
{
    Token = token,
    Intents = DiscordIntents.Guilds
};
```

Or use `appsettings.json`:

```json
{
  "Discord": {
    "Token": "YOUR_BOT_TOKEN",
    "Intents": "Guilds,GuildMessages"
  }
}
```

## Next Steps

- [Commands](Commands.md) - Set up slash commands
- [Events](Events.md) - Handle Discord events
- [API Reference](API-Reference.md) - Detailed API documentation
