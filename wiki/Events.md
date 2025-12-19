# Events

Handle Discord events to make your bot interactive and responsive.

## Event System

SimpleDiscordNet provides a strongly-typed event system through the `DiscordEvents` class.

## Subscribing to Events

Access events through the bot instance:

```csharp
var bot = new DiscordBot(options);

bot.Events.Ready += async (sender, args) =>
{
    Console.WriteLine($"Bot is ready! Logged in as {bot.CurrentUser.Username}");
};

await bot.StartAsync();
```

## Common Events

### Ready Event

Fired when the bot connects and is ready:

```csharp
bot.Events.Ready += async (sender, args) =>
{
    Console.WriteLine($"Connected as {bot.CurrentUser.Username}");
    Console.WriteLine($"Serving {bot.Guilds.Count} guilds");
};
```

### Guild Events

#### GuildCreated

Fired when bot joins a guild or guild becomes available:

```csharp
bot.Events.GuildCreated += async (sender, guild) =>
{
    Console.WriteLine($"Joined guild: {guild.Name}");
};
```

#### GuildUpdated

Fired when guild is updated:

```csharp
bot.Events.GuildUpdated += async (sender, guild) =>
{
    Console.WriteLine($"Guild updated: {guild.Name}");
};
```

#### GuildDeleted

Fired when bot leaves a guild or guild becomes unavailable:

```csharp
bot.Events.GuildDeleted += async (sender, guildId) =>
{
    Console.WriteLine($"Left guild: {guildId}");
};
```

### Message Events

#### MessageCreated

Fired when a message is sent:

```csharp
bot.Events.MessageCreated += async (sender, message) =>
{
    if (message.Author.IsBot) return;

    Console.WriteLine($"{message.Author.Username}: {message.Content}");
};
```

**Note:** Requires `MessageContent` intent for message content.

#### MessageUpdated

Fired when a message is edited:

```csharp
bot.Events.MessageUpdated += async (sender, message) =>
{
    Console.WriteLine($"Message {message.Id} was edited");
};
```

#### MessageDeleted

Fired when a message is deleted:

```csharp
bot.Events.MessageDeleted += async (sender, args) =>
{
    Console.WriteLine($"Message {args.MessageId} deleted in {args.ChannelId}");
};
```

### Member Events

#### GuildMemberAdded

Fired when a user joins a guild:

```csharp
bot.Events.GuildMemberAdded += async (sender, member) =>
{
    var channel = await bot.GetChannelAsync(welcomeChannelId);
    await channel.SendMessageAsync($"Welcome {member.User.Mention}!");
};
```

**Note:** Requires `GuildMembers` intent (privileged).

#### GuildMemberRemoved

Fired when a user leaves or is kicked:

```csharp
bot.Events.GuildMemberRemoved += async (sender, args) =>
{
    Console.WriteLine($"{args.User.Username} left {args.Guild.Name}");
};
```

#### GuildMemberUpdated

Fired when member is updated (roles, nickname, etc.):

```csharp
bot.Events.GuildMemberUpdated += async (sender, member) =>
{
    Console.WriteLine($"Member updated: {member.User.Username}");
};
```

### Role Events

#### RoleCreated

Fired when a role is created:

```csharp
bot.Events.RoleCreated += async (sender, role) =>
{
    Console.WriteLine($"Role created: {role.Name}");
};
```

#### RoleUpdated

Fired when a role is updated:

```csharp
bot.Events.RoleUpdated += async (sender, role) =>
{
    Console.WriteLine($"Role updated: {role.Name}");
};
```

#### RoleDeleted

Fired when a role is deleted:

```csharp
bot.Events.RoleDeleted += async (sender, args) =>
{
    Console.WriteLine($"Role deleted: {args.RoleId}");
};
```

### Channel Events

#### ChannelCreated

Fired when a channel is created:

```csharp
bot.Events.ChannelCreated += async (sender, channel) =>
{
    Console.WriteLine($"Channel created: {channel.Name}");
};
```

#### ChannelUpdated

Fired when a channel is updated:

```csharp
bot.Events.ChannelUpdated += async (sender, channel) =>
{
    Console.WriteLine($"Channel updated: {channel.Name}");
};
```

#### ChannelDeleted

Fired when a channel is deleted:

```csharp
bot.Events.ChannelDeleted += async (sender, channel) =>
{
    Console.WriteLine($"Channel deleted: {channel.Name}");
};
```

### Interaction Events

#### InteractionCreated

Fired when any interaction is received (commands, buttons, etc.):

```csharp
bot.Events.InteractionCreated += async (sender, interaction) =>
{
    Console.WriteLine($"Interaction: {interaction.Type}");
};
```

**Note:** Command interactions are typically handled by the CommandService.

## Event Arguments

Most events provide rich data objects:

```csharp
bot.Events.MessageCreated += async (sender, message) =>
{
    // Access message properties
    var content = message.Content;
    var author = message.Author;
    var channel = message.Channel;
    var guild = message.Guild; // null if DM

    // Check if bot was mentioned
    if (message.MentionedUsers.Any(u => u.Id == bot.CurrentUser.Id))
    {
        await message.Channel.SendMessageAsync("You mentioned me!");
    }
};
```

## Async Event Handlers

All event handlers support async/await:

```csharp
bot.Events.GuildMemberAdded += async (sender, member) =>
{
    // Async operations
    var channel = await bot.GetChannelAsync(channelId);
    await channel.SendMessageAsync($"Welcome {member.User.Username}!");

    // Add default role
    var role = member.Guild.Roles.First(r => r.Name == "Member");
    await member.AddRoleAsync(role);
};
```

## Multiple Handlers

Subscribe multiple handlers to the same event:

```csharp
bot.Events.MessageCreated += LogMessage;
bot.Events.MessageCreated += CheckForSpam;
bot.Events.MessageCreated += UpdateUserStats;

async Task LogMessage(object sender, Message message) { /* ... */ }
async Task CheckForSpam(object sender, Message message) { /* ... */ }
async Task UpdateUserStats(object sender, Message message) { /* ... */ }
```

## Unsubscribing from Events

Remove event handlers when needed:

```csharp
bot.Events.MessageCreated -= LogMessage;
```

## Error Handling

Always handle errors in event handlers:

```csharp
bot.Events.MessageCreated += async (sender, message) =>
{
    try
    {
        // Event logic
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error handling message: {ex.Message}");
    }
};
```

## Required Intents

Some events require specific intents:

| Event | Required Intent | Privileged |
|-------|----------------|-----------|
| MessageCreated | `GuildMessages` or `DirectMessages` | No |
| Message content | `MessageContent` | Yes |
| GuildMemberAdded | `GuildMembers` | Yes |
| GuildMemberUpdated | `GuildMembers` | Yes |

## Best Practices

1. **Use async/await** - All handlers support async operations
2. **Handle errors** - Catch exceptions in event handlers
3. **Check intents** - Ensure you have required intents enabled
4. **Avoid blocking** - Don't perform long operations synchronously
5. **Check for bots** - Filter bot messages if needed: `if (message.Author.IsBot) return;`
6. **Null checks** - Some properties may be null (e.g., `message.Guild` in DMs)

## Example: Auto-Moderation

```csharp
bot.Events.MessageCreated += async (sender, message) =>
{
    if (message.Author.IsBot) return;

    // Check for spam
    if (message.Content.Length > 2000 || message.Content.Contains("spam"))
    {
        await message.DeleteAsync();
        await message.Author.SendMessageAsync("Please don't spam!");
    }
};
```

## Example: Welcome System

```csharp
bot.Events.GuildMemberAdded += async (sender, member) =>
{
    var channel = member.Guild.Channels
        .FirstOrDefault(c => c.Name == "welcome");

    if (channel != null)
    {
        var embed = new EmbedBuilder()
            .WithTitle($"Welcome {member.User.Username}!")
            .WithDescription("Thanks for joining our server!")
            .WithColor(DiscordColor.Green)
            .Build();

        await channel.SendMessageAsync(embed: embed);
    }
};
```

## Next Steps

- [Commands](Commands.md) - Create slash commands
- [Embeds](Embeds.md) - Create rich embed messages
- [API Reference](API-Reference.md) - Full API documentation
