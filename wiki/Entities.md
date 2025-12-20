# Working with Entities

SimpleDiscordNet provides strongly-typed entity classes for all Discord objects. All entities are cached and accessible through the static `DiscordContext` class.

## Table of Contents

- [Accessing Entities](#accessing-entities)
- [Channels](#channels)
- [Guilds](#guilds)
- [Members](#members)
- [Messages](#messages)
- [Roles](#roles)
- [Users](#users)

## Accessing Entities

All cached entities are accessible through the static `DiscordContext` class:

```csharp
using SimpleDiscordNet.Context;

// Get all cached entities
var allChannels = DiscordContext.Channels;
var allGuilds = DiscordContext.Guilds;
var allMembers = DiscordContext.Members;
var allRoles = DiscordContext.Roles;
var allUsers = DiscordContext.Users;

// Query specific entities
var channel = DiscordContext.GetChannel(channelId);
var guild = DiscordContext.GetGuild(guildId);
var member = DiscordContext.GetMember(guildId, userId);
var role = DiscordContext.GetRole(guildId, roleId);
```

## Channels

### Properties

Each `DiscordChannel` has:
- `Id` - Channel ID
- `Name` - Channel name
- `Type` - Channel type (text, voice, category, etc.)
- `ChannelType` - Typed enum for channel type
- `Guild` - The guild this channel belongs to (null for DMs)
- `Parent_Id` - Category ID (if in a category)

### Channel Types

```csharp
// Check channel type
if (channel.IsTextChannel)
    Console.WriteLine("This is a text channel");

if (channel.IsVoiceChannel)
    Console.WriteLine("This is a voice channel");

if (channel.IsCategory)
    Console.WriteLine("This is a category");

// Or use the enum
if (channel.ChannelType == ChannelType.GuildText)
    Console.WriteLine("Text channel");
```

### Sending Messages

```csharp
// Send a simple text message
var message = await channel.SendMessageAsync("Hello, world!");

// Send with an embed
var embed = new EmbedBuilder()
    .WithTitle("Important Announcement")
    .WithDescription("This is the content")
    .WithColor(0xFF0000);

await channel.SendMessageAsync("Check this out!", embed);

// Send with a MessageBuilder (for components)
var builder = new MessageBuilder()
    .WithContent("Click a button!")
    .WithComponents(new Button("Click me", "button_id"));

await channel.SendMessageAsync(builder);
```

### Modifying Channels

```csharp
// Set channel name
await channel.SetNameAsync("new-channel-name");

// Set channel topic
await channel.SetTopicAsync("Welcome to our channel!");

// Set NSFW flag
await channel.SetNsfwAsync(true);

// Set slowmode (5 seconds between messages)
await channel.SetSlowmodeAsync(5);

// Move channel to a different category
await channel.MoveAsync(parentId: categoryId);

// Change channel position
await channel.MoveAsync(position: 5);

// Voice channel settings
await voiceChannel.SetBitrateAsync(96000); // 96 kbps
await voiceChannel.SetUserLimitAsync(10); // Max 10 users
```

### Deleting Channels

```csharp
await channel.DeleteAsync();
```

## Guilds

### Properties

Each `DiscordGuild` has:
- `Id` - Guild ID
- `Name` - Guild name
- `Icon` - Icon hash
- `Owner_Id` - Owner user ID
- `Roles` - Array of roles
- `Members` - Live collection of cached members
- `Channels` - Live collection of cached channels

### Creating Channels

```csharp
// Create a text channel
var textChannel = await guild.CreateChannelAsync(
    "general",
    ChannelType.GuildText
);

// Create a channel in a category
var category = guild.Channels.FirstOrDefault(c => c.IsCategory);
var channelInCategory = await guild.CreateChannelAsync(
    "chat",
    ChannelType.GuildText,
    parent: category
);

// Create a voice channel
var voiceChannel = await guild.CreateChannelAsync(
    "Voice Chat",
    ChannelType.GuildVoice
);

// Create a category
var newCategory = await guild.CreateCategoryAsync("General Channels");
```

### Accessing Guild Data

```csharp
// Get all members
foreach (var member in guild.Members)
{
    Console.WriteLine($"{member.User.Username} joined at {member.Joined_At}");
}

// Get all channels
foreach (var channel in guild.Channels)
{
    if (channel.IsTextChannel)
        Console.WriteLine($"Text channel: {channel.Name}");
}

// Check guild features
if (guild.IsCommunity)
    Console.WriteLine("This is a community guild");

if (guild.IsVerified)
    Console.WriteLine("This guild is verified");
```

## Members

### Properties

Each `DiscordMember` has:
- `User` - The underlying Discord user
- `Guild` - The guild this member belongs to
- `Nick` - Nickname (null if no nickname set)
- `Roles` - Array of role IDs
- `Joined_At` - When they joined
- `Avatar` - Guild-specific avatar hash

### Managing Roles

```csharp
// Add a role
await member.AddRoleAsync(roleId);

// Remove a role
await member.RemoveRoleAsync(roleId);

// Check if member has a role
bool hasRole = member.Roles?.Contains(roleId) ?? false;
```

### Getting Member Information

```csharp
// Get effective avatar URL (guild avatar or user avatar)
string avatarUrl = member.GetEffectiveAvatarUrl();

// Get guild-specific avatar
string? guildAvatar = member.GetGuildAvatarUrl();

// Access user properties
Console.WriteLine($"Username: {member.User.Username}");
Console.WriteLine($"User ID: {member.User.Id}");
```

## Messages

### Properties

Each `DiscordMessage` has:
- `Id` - Message ID
- `ChannelId` - Channel ID
- `GuildId` - Guild ID (null for DMs)
- `Author` - The user who sent the message
- `Content` - Message text content
- `Embeds` - Array of embeds
- `Attachments` - Array of file attachments
- `Pinned` - Whether message is pinned

### Message Operations

```csharp
// Pin a message
await message.PinAsync();

// Delete a message
await message.DeleteAsync();

// Check message type
if (message.Type == DiscordMessage.MessageType.Default)
    Console.WriteLine("Regular message");
```

### Sending Messages

All send methods return the created `DiscordMessage`:

```csharp
// Send and get the message back
var sentMessage = await channel.SendMessageAsync("Hello!");

// Pin it immediately
await sentMessage.PinAsync();

// Send a DM
var dmMessage = await user.SendDMAsync("Private message!");
```

## Roles

### Properties

Each `DiscordRole` has:
- `Id` - Role ID
- `Name` - Role name
- `Guild` - The guild this role belongs to
- `Color` - Role color (integer RGB)
- `Position` - Role position in hierarchy
- `Permissions` - Permission bitfield

### Working with Roles

```csharp
// Get role from guild
var role = guild.Roles?.FirstOrDefault(r => r.Name == "Moderator");

if (role != null)
{
    Console.WriteLine($"Role: {role.Name}");
    Console.WriteLine($"Color: {role.Color}");
    Console.WriteLine($"Position: {role.Position}");
}

// Assign role to member
await member.AddRoleAsync(role.Id);
```

## Users

### Properties

Each `DiscordUser` has:
- `Id` - User ID
- `Username` - Username
- `Guilds` - Array of all guilds the bot shares with this user
- `IsBot` - Whether this is a bot account
- `IsCurrentBot` - Whether this is the currently running bot

### Sending Direct Messages

```csharp
// Send a DM to a user
var message = await user.SendDMAsync("Hello from the bot!");

// Send a DM with an embed
var embed = new EmbedBuilder()
    .WithTitle("Welcome!")
    .WithDescription("Thanks for using our bot");

await user.SendDMAsync("Welcome!", embed);
```

### Checking User Context

```csharp
// Check if user is a bot
if (user.IsBot)
    return; // Ignore bot messages

// Check if user is the current bot
if (user.IsCurrentBot)
    return; // Ignore self

// Get all mutual guilds
foreach (var guild in user.Guilds)
{
    Console.WriteLine($"Shared guild: {guild.Name}");
}
```

## Best Practices

### 1. Use Live Entity References

Entities from `DiscordContext` are live references that update automatically:

```csharp
var channel = DiscordContext.GetChannel(channelId);
// This channel object will reflect updates from CHANNEL_UPDATE events
```

### 2. Check for Null

Always check if entities exist before using them:

```csharp
var channel = DiscordContext.GetChannel(channelId);
if (channel == null)
{
    Console.WriteLine("Channel not found in cache");
    return;
}
```

### 3. Use Entity Methods

Prefer entity methods over bot methods:

```csharp
// ✅ Good - use entity methods
await channel.SendMessageAsync("Hello!");
await message.DeleteAsync();
await member.AddRoleAsync(roleId);

// ❌ Less ideal - using bot directly
await bot.SendMessageAsync(channelId, "Hello!");
await bot.DeleteMessageAsync(channelId, messageId);
await bot.AddRoleToMemberAsync(guildId, userId, roleId);
```

### 4. Query Before Creating

Check if entities exist before creating duplicates:

```csharp
// Check if channel already exists
var existingChannel = guild.Channels.FirstOrDefault(c => c.Name == "general");
if (existingChannel == null)
{
    await guild.CreateChannelAsync("general", ChannelType.GuildText);
}
```

## Next Steps

- Learn about [Gateway Events](Events.md) to react to entity changes
- Explore [Commands](Commands.md) for handling user interactions
- Check the [API Reference](API-Reference.md) for complete entity documentation
