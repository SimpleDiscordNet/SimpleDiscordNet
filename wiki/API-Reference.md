# API Reference

Complete API documentation for SimpleDiscordNet.

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
| `EnableCaching` | `bool` | No | Enable entity caching (default: true) |
| `CacheOptions` | `CacheOptions` | No | Cache configuration |
| `LogLevel` | `LogLevel` | No | Logging verbosity |
| `MaxRetries` | `int` | No | Max API retries (default: 3) |
| `Timeout` | `int` | No | Request timeout in ms |

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

#### Events

```csharp
event EventHandler<EventArgs> Ready
event EventHandler<Guild> GuildCreated
event EventHandler<Guild> GuildUpdated
event EventHandler<ulong> GuildDeleted
event EventHandler<Channel> ChannelCreated
event EventHandler<Channel> ChannelUpdated
event EventHandler<Channel> ChannelDeleted
event EventHandler<Message> MessageCreated
event EventHandler<Message> MessageUpdated
event EventHandler<MessageDeletedEventArgs> MessageDeleted
event EventHandler<Member> GuildMemberAdded
event EventHandler<GuildMemberRemovedEventArgs> GuildMemberRemoved
event EventHandler<Member> GuildMemberUpdated
event EventHandler<Role> RoleCreated
event EventHandler<Role> RoleUpdated
event EventHandler<RoleDeletedEventArgs> RoleDeleted
event EventHandler<Interaction> InteractionCreated
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
