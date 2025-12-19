# API Reference

Complete API documentation for SimpleDiscordNet.

> **Performance Note:** SimpleDiscordNet is memory-optimized with Span<T> and Memory<T> APIs, achieving 30-50% less GC pressure. See [Performance Optimizations](Performance-Optimizations) for details.

## Core Classes

### DiscordBot

The main bot client.

**Namespace:** `SimpleDiscordNet.Core`

#### Constructor

```csharp
public DiscordBot(DiscordBotOptions options)
```

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `CurrentUser` | `User` | The bot's user object |
| `Guilds` | `IReadOnlyList<Guild>` | All guilds the bot is in |
| `Events` | `DiscordEvents` | Event subscription system |
| `Latency` | `int` | Gateway latency in milliseconds |
| `IsConnected` | `bool` | Whether bot is connected |

#### Methods

```csharp
Task StartAsync()
Task StopAsync()
Task<Guild> GetGuildAsync(ulong guildId)
Task<Channel> GetChannelAsync(ulong channelId)
Task<User> GetUserAsync(ulong userId)
```

### DiscordBotOptions

Configuration options for the bot.

**Namespace:** `SimpleDiscordNet.Core`

#### Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Token` | `string` | Yes | Bot token |
| `Intents` | `DiscordIntents` | Yes | Gateway intents |
| `JsonOptions` | `JsonSerializerOptions` | No | JSON serialization options |
| `TimeProvider` | `TimeProvider?` | No | Time provider for testing |
| `PreloadGuilds` | `bool` | No | Preload guilds on start (default: true) |
| `PreloadChannels` | `bool` | No | Preload channels on start (default: true) |
| `PreloadMembers` | `bool` | No | Preload members on start (default: false) |
| `AutoLoadFullGuildData` | `bool` | No | Auto-load complete guild data after GUILD_CREATE (default: true) |
| `DevelopmentMode` | `bool` | No | Enable development mode with instant command sync (default: false) |
| `DevelopmentGuildIds` | `List<string>` | No | Guild IDs for development mode command sync |
| `LogSink` | `Action<LogMessage>?` | No | Custom log sink for mirroring logs |
| `MinimumLogLevel` | `LogLevel` | No | Minimum log level to emit (default: Trace) |

## Commands

### CommandService

Handles command registration and execution.

**Namespace:** `SimpleDiscordNet.Commands`

#### Constructor

```csharp
public CommandService(IDiscordBot bot)
```

#### Methods

```csharp
void RegisterCommands(Assembly assembly)
Task SyncCommandsAsync(ulong? guildId = null)
Task<IEnumerable<ApplicationCommand>> GetGlobalCommandsAsync()
Task<IEnumerable<ApplicationCommand>> GetGuildCommandsAsync(ulong guildId)
```

### Attributes

#### SlashCommandAttribute

Define a slash command.

```csharp
[SlashCommand(string name, string description)]
```

#### SlashCommandGroupAttribute

Define a command group.

```csharp
[SlashCommandGroup(string name, string description)]
```

#### DeferAttribute

Automatically defer interaction response.

```csharp
[Defer(bool ephemeral = false)]
```

#### CommandOptionAttribute

Define command parameter options with constraints.

**Namespace:** `SimpleDiscordNet.Commands`

```csharp
[CommandOption(string name, string description)]
```

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Option name (lowercase, 1-32 chars) |
| `Description` | `string` | Option description |
| `Required` | `bool?` | Override required/optional behavior |
| `MinLength` | `int?` | Minimum string length (STRING type) |
| `MaxLength` | `int?` | Maximum string length (STRING type) |
| `MinValue` | `double?` | Minimum numeric value (INTEGER/NUMBER types) |
| `MaxValue` | `double?` | Maximum numeric value (INTEGER/NUMBER types) |
| `ChannelTypes` | `string?` | Comma-separated channel type IDs (CHANNEL type) |
| `Choices` | `string?` | Predefined choices in format "Display:value,Display2:value2" |
| `Autocomplete` | `bool` | Enable autocomplete (requires handler) |

**Supported Parameter Types:**
- `string` → Discord STRING (type 3)
- `int`, `long` → Discord INTEGER (type 4)
- `bool` → Discord BOOLEAN (type 5)
- `User` → Discord USER (type 6)
- `Channel` → Discord CHANNEL (type 7)
- `Role` → Discord ROLE (type 8)
- `double`, `float` → Discord NUMBER (type 10)

**Example:**

```csharp
[SlashCommand("profile", "Update profile")]
public async Task ProfileAsync(
    InteractionContext ctx,
    [CommandOption("name", "Display name", MinLength = 2, MaxLength = 32)]
    string name,
    [CommandOption("age", "Your age", MinValue = 13, MaxValue = 120)]
    int age,
    [CommandOption("size", "T-shirt size", Choices = "Small:S,Medium:M,Large:L")]
    string size)
{
    await ctx.RespondAsync($"Profile: {name}, {age}, {size}");
}
```

#### CommandChoiceAttribute

Define individual choices for command options (alternative to Choices property).

**Namespace:** `SimpleDiscordNet.Commands`

```csharp
[CommandChoice(string name, object value)]
```

**Parameters:**
- `name` - Display name shown to user (1-100 chars)
- `value` - Actual value sent to command (must match parameter type: string, int, long, or double)

**Usage:**
- Use `[AttributeUsage(AllowMultiple = true)]` to add multiple choices
- Takes precedence over `Choices` property if both are specified
- Limited to 25 choices maximum per option

**Example:**

```csharp
[SlashCommand("greet", "Greet someone")]
public async Task GreetAsync(
    InteractionContext ctx,
    [CommandOption("style", "Greeting style")]
    [CommandChoice("Formal", "formal")]
    [CommandChoice("Casual", "casual")]
    [CommandChoice("Enthusiastic", "enthusiastic")]
    string style = "casual")
{
    await ctx.RespondAsync($"Hello! (Style: {style})");
}
```

### InteractionContext

Context for command execution.

**Namespace:** `SimpleDiscordNet.Commands`

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Bot` | `IDiscordBot` | Bot instance |
| `Interaction` | `Interaction` | Raw interaction data |
| `Guild` | `Guild` | Current guild (null in DMs) |
| `Channel` | `Channel` | Current channel |
| `User` | `User` | User who invoked command |
| `Member` | `Member` | Member object (null in DMs) |

#### Methods

```csharp
Task RespondAsync(string content = null, Embed embed = null, bool ephemeral = false)
Task EditResponseAsync(string content = null, Embed embed = null)
Task FollowUpAsync(string content = null, Embed embed = null, bool ephemeral = false)
Task DeferAsync(bool ephemeral = false)
```

## Entities

### Guild

Represents a Discord guild (server).

**Namespace:** `SimpleDiscordNet.Entities`

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `ulong` | Guild ID |
| `Name` | `string` | Guild name |
| `IconUrl` | `string` | Guild icon URL |
| `Owner` | `User` | Guild owner |
| `OwnerId` | `ulong` | Owner's user ID |
| `MemberCount` | `int` | Total member count |
| `Channels` | `IReadOnlyList<Channel>` | All channels |
| `Roles` | `IReadOnlyList<Role>` | All roles |
| `CreatedAt` | `DateTimeOffset` | When guild was created |

#### Methods

```csharp
Task<Member> GetMemberAsync(ulong userId)
Task<Channel> GetChannelAsync(ulong channelId)
Task<Role> GetRoleAsync(ulong roleId)
Task<IEnumerable<Member>> GetMembersAsync()
Task<Channel> CreateChannelAsync(string name, ChannelType type)
Task<Role> CreateRoleAsync(string name, DiscordColor color, PermissionFlags permissions)
Task LeaveAsync()
```

### Channel

Represents a Discord channel.

**Namespace:** `SimpleDiscordNet.Entities`

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `ulong` | Channel ID |
| `Name` | `string` | Channel name |
| `Type` | `ChannelType` | Channel type |
| `GuildId` | `ulong?` | Guild ID (null for DMs) |
| `Position` | `int` | Channel position |
| `Topic` | `string` | Channel topic |

#### Methods

```csharp
Task<Message> SendMessageAsync(string content = null, Embed embed = null)
Task<Message> GetMessageAsync(ulong messageId)
Task DeleteAsync()
Task ModifyAsync(string name = null, string topic = null, int? position = null)
```

### User

Represents a Discord user.

**Namespace:** `SimpleDiscordNet.Entities`

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `ulong` | User ID |
| `Username` | `string` | Username |
| `Discriminator` | `string` | User discriminator |
| `AvatarUrl` | `string` | Avatar URL |
| `IsBot` | `bool` | Whether user is a bot |
| `CreatedAt` | `DateTimeOffset` | When account was created |
| `Mention` | `string` | User mention string |

#### Methods

```csharp
Task<Channel> CreateDmAsync()
Task<Message> SendMessageAsync(string content = null, Embed embed = null)
```

### Member

Represents a guild member.

**Namespace:** `SimpleDiscordNet.Entities`

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `User` | `User` | User object |
| `Guild` | `Guild` | Parent guild |
| `Nickname` | `string` | Member nickname |
| `Roles` | `IReadOnlyList<Role>` | Member roles |
| `JoinedAt` | `DateTimeOffset` | When member joined |
| `Permissions` | `PermissionFlags` | Member permissions |

#### Methods

```csharp
Task AddRoleAsync(Role role)
Task RemoveRoleAsync(Role role)
Task ModifyAsync(string nickname = null)
Task KickAsync(string reason = null)
Task BanAsync(int deleteMessageDays = 0, string reason = null)
```

### Role

Represents a guild role.

**Namespace:** `SimpleDiscordNet.Entities`

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `ulong` | Role ID |
| `Name` | `string` | Role name |
| `Color` | `DiscordColor` | Role color |
| `Position` | `int` | Role position |
| `Permissions` | `PermissionFlags` | Role permissions |
| `IsHoisted` | `bool` | Display separately |
| `IsMentionable` | `bool` | Can be mentioned |

#### Methods

```csharp
Task ModifyAsync(string name = null, DiscordColor? color = null, PermissionFlags? permissions = null)
Task DeleteAsync()
```

### Message

Represents a Discord message.

**Namespace:** `SimpleDiscordNet.Entities`

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `ulong` | Message ID |
| `Content` | `string` | Message content |
| `Author` | `User` | Message author |
| `Channel` | `Channel` | Channel message is in |
| `Guild` | `Guild` | Guild (null if DM) |
| `Timestamp` | `DateTimeOffset` | When message was sent |
| `EditedTimestamp` | `DateTimeOffset?` | When message was edited |
| `MentionedUsers` | `IReadOnlyList<User>` | Mentioned users |
| `MentionedRoles` | `IReadOnlyList<Role>` | Mentioned roles |
| `Embeds` | `IReadOnlyList<Embed>` | Message embeds |

#### Methods

```csharp
Task DeleteAsync()
Task ModifyAsync(string content = null, Embed embed = null)
Task AddReactionAsync(Emoji emoji)
Task RemoveReactionAsync(Emoji emoji)
```

## Embeds

### EmbedBuilder

Build embed messages.

**Namespace:** `SimpleDiscordNet.Primitives`

#### Methods

```csharp
EmbedBuilder WithTitle(string title)
EmbedBuilder WithDescription(string description)
EmbedBuilder WithUrl(string url)
EmbedBuilder WithColor(DiscordColor color)
EmbedBuilder WithTimestamp(DateTimeOffset timestamp)
EmbedBuilder WithFooter(string text, string iconUrl = null)
EmbedBuilder WithImageUrl(string url)
EmbedBuilder WithThumbnailUrl(string url)
EmbedBuilder WithAuthor(string name, string iconUrl = null, string url = null)
EmbedBuilder AddField(string name, string value, bool inline = false)
Embed Build()
```

### DiscordColor

Represents a color in Discord embeds.

**Namespace:** `SimpleDiscordNet.Primitives`

#### Constructor

```csharp
public DiscordColor(byte r, byte g, byte b)
```

#### Static Methods

```csharp
static DiscordColor FromHex(string hex)
static DiscordColor FromRgb(byte r, byte g, byte b)
```

#### Predefined Colors

```csharp
DiscordColor.Default
DiscordColor.Blue
DiscordColor.Green
DiscordColor.Red
DiscordColor.Gold
DiscordColor.Orange
DiscordColor.Purple
DiscordColor.Magenta
// ... and more
```

## Events

### DiscordEvents

Event subscription system.

**Namespace:** `SimpleDiscordNet.Events`

#### Connection & Logging Events

```csharp
event EventHandler? Connected
event EventHandler<Exception?>? Disconnected
event EventHandler<Exception>? Error
event EventHandler<LogMessage>? Log
```

#### Guild Events

```csharp
event EventHandler<GuildEvent>? GuildAdded
event EventHandler<GuildEvent>? GuildUpdated
event EventHandler<string>? GuildRemoved           // Guild ID
event EventHandler<GuildEvent>? GuildReady         // Fired when guild is fully loaded (requires AutoLoadFullGuildData)
```

#### Channel & Thread Events

```csharp
event EventHandler<ChannelEvent>? ChannelCreated
event EventHandler<ChannelEvent>? ChannelUpdated
event EventHandler<ChannelEvent>? ChannelDeleted
event EventHandler<ThreadEvent>? ThreadCreated
event EventHandler<ThreadEvent>? ThreadUpdated
event EventHandler<ThreadEvent>? ThreadDeleted
```

#### Role Events

```csharp
event EventHandler<RoleEvent>? RoleCreated
event EventHandler<RoleEvent>? RoleUpdated
event EventHandler<RoleEvent>? RoleDeleted
```

#### Message & Reaction Events

```csharp
event EventHandler<MessageUpdateEvent>? MessageUpdated
event EventHandler<MessageEvent>? MessageDeleted
event EventHandler<MessageEvent>? MessagesBulkDeleted
event EventHandler<ReactionEvent>? ReactionAdded
event EventHandler<ReactionEvent>? ReactionRemoved
event EventHandler<ReactionEvent>? ReactionsClearedForEmoji
event EventHandler<MessageEvent>? ReactionsCleared
```

#### Member & Ban Events

```csharp
event EventHandler<MemberEvent>? MemberJoined
event EventHandler<MemberEvent>? MemberUpdated
event EventHandler<MemberEvent>? MemberLeft        // Includes kicks and leaves
event EventHandler<BanEvent>? BanAdded
event EventHandler<BanEvent>? BanRemoved
```

#### User & DM Events

```csharp
event EventHandler<BotUserEvent>? BotUserUpdated
event EventHandler<DirectMessageEvent>? DirectMessageReceived
```

## Enums

### DiscordIntents

Gateway intents flags.

**Namespace:** `SimpleDiscordNet.Primitives`

```csharp
[Flags]
public enum DiscordIntents
{
    None = 0,
    Guilds = 1 << 0,
    GuildMembers = 1 << 1,          // Privileged
    GuildBans = 1 << 2,
    GuildEmojis = 1 << 3,
    GuildIntegrations = 1 << 4,
    GuildWebhooks = 1 << 5,
    GuildInvites = 1 << 6,
    GuildVoiceStates = 1 << 7,
    GuildPresences = 1 << 8,        // Privileged
    GuildMessages = 1 << 9,
    GuildMessageReactions = 1 << 10,
    GuildMessageTyping = 1 << 11,
    DirectMessages = 1 << 12,
    DirectMessageReactions = 1 << 13,
    DirectMessageTyping = 1 << 14,
    MessageContent = 1 << 15,       // Privileged

    AllUnprivileged = Guilds | GuildBans | GuildEmojis | /* ... */,
    All = AllUnprivileged | GuildMembers | GuildPresences | MessageContent
}
```

### PermissionFlags

Discord permissions.

**Namespace:** `SimpleDiscordNet.Primitives`

```csharp
[Flags]
public enum PermissionFlags : ulong
{
    None = 0,
    CreateInstantInvite = 1UL << 0,
    KickMembers = 1UL << 1,
    BanMembers = 1UL << 2,
    Administrator = 1UL << 3,
    ManageChannels = 1UL << 4,
    ManageGuild = 1UL << 5,
    AddReactions = 1UL << 6,
    ViewAuditLog = 1UL << 7,
    SendMessages = 1UL << 11,
    ManageMessages = 1UL << 13,
    MentionEveryone = 1UL << 17,
    ManageRoles = 1UL << 28,
    // ... and more
}
```

### ChannelType

Channel types.

**Namespace:** `SimpleDiscordNet.Models`

```csharp
public enum ChannelType
{
    GuildText = 0,
    Dm = 1,
    GuildVoice = 2,
    GroupDm = 3,
    GuildCategory = 4,
    GuildNews = 5,
    GuildStore = 6,
    GuildNewsThread = 10,
    GuildPublicThread = 11,
    GuildPrivateThread = 12,
    GuildStageVoice = 13
}
```

## Next Steps

- [Getting Started](Getting-Started.md) - Build your first bot
- [Commands](Commands.md) - Create slash commands
- [Events](Events.md) - Handle Discord events
- [Embeds](Embeds.md) - Create rich embeds
