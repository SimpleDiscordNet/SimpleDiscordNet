# Embeds

Create rich, visually appealing embed messages with SimpleDiscordNet.

## Quick Start

Use `EmbedBuilder` to create embeds:

```csharp
var embed = new EmbedBuilder()
    .WithTitle("Hello, World!")
    .WithDescription("This is an embed message")
    .WithColor(DiscordColor.Blue)
    .Build();

await channel.SendMessageAsync(embed: embed);
```

## EmbedBuilder

### Title and Description

```csharp
var embed = new EmbedBuilder()
    .WithTitle("Bot Status")
    .WithDescription("The bot is online and ready!")
    .Build();
```

### Colors

Use predefined colors or create custom ones:

```csharp
// Predefined colors
.WithColor(DiscordColor.Blue)
.WithColor(DiscordColor.Red)
.WithColor(DiscordColor.Green)
.WithColor(DiscordColor.Gold)

// Custom RGB color
.WithColor(new DiscordColor(255, 100, 50))

// Custom hex color
.WithColor(DiscordColor.FromHex("#FF6432"))
```

### Fields

Add multiple fields to organize information:

```csharp
var embed = new EmbedBuilder()
    .WithTitle("Server Info")
    .AddField("Members", "1,234", inline: true)
    .AddField("Channels", "42", inline: true)
    .AddField("Owner", "John#1234", inline: true)
    .Build();
```

**Inline fields** appear side-by-side (up to 3 per row).

### Timestamps

Add timestamps to embeds:

```csharp
var embed = new EmbedBuilder()
    .WithTitle("Event Log")
    .WithDescription("User joined the server")
    .WithTimestamp(DateTimeOffset.Now)
    .Build();
```

### Footer

Add footer text and icon:

```csharp
var embed = new EmbedBuilder()
    .WithFooter("Bot v1.0", "https://example.com/icon.png")
    .Build();
```

### Images and Thumbnails

```csharp
var embed = new EmbedBuilder()
    .WithTitle("Image Example")
    .WithImageUrl("https://example.com/image.png")
    .WithThumbnailUrl("https://example.com/thumb.png")
    .Build();
```

### Author

Add author information:

```csharp
var embed = new EmbedBuilder()
    .WithAuthor("John Doe", "https://example.com/avatar.png", "https://example.com")
    .Build();
```

### URL

Make the title clickable:

```csharp
var embed = new EmbedBuilder()
    .WithTitle("Visit our website")
    .WithUrl("https://example.com")
    .Build();
```

## Complete Example

```csharp
var embed = new EmbedBuilder()
    .WithTitle("Server Statistics")
    .WithDescription("Here are the current server statistics")
    .WithColor(DiscordColor.Blue)
    .WithThumbnailUrl(guild.IconUrl)
    .AddField("Total Members", guild.MemberCount.ToString(), inline: true)
    .AddField("Online Members", onlineCount.ToString(), inline: true)
    .AddField("Channels", guild.Channels.Count.ToString(), inline: true)
    .AddField("Roles", guild.Roles.Count.ToString(), inline: true)
    .AddField("Created", guild.CreatedAt.ToString("MMM dd, yyyy"), inline: true)
    .AddField("Owner", guild.Owner.Username, inline: true)
    .WithFooter($"Server ID: {guild.Id}")
    .WithTimestamp(DateTimeOffset.Now)
    .Build();

await channel.SendMessageAsync(embed: embed);
```

## Embed Limits

Discord enforces the following limits:

- **Title**: 256 characters
- **Description**: 4096 characters
- **Fields**: 25 fields maximum
- **Field name**: 256 characters
- **Field value**: 1024 characters
- **Footer**: 2048 characters
- **Author name**: 256 characters
- **Total characters**: 6000 characters across all fields

## Colors Reference

SimpleDiscordNet provides these predefined colors:

```csharp
DiscordColor.Default    // #000000
DiscordColor.Blue       // #3498db
DiscordColor.Green      // #2ecc71
DiscordColor.Red        // #e74c3c
DiscordColor.Gold       // #f1c40f
DiscordColor.Orange     // #e67e22
DiscordColor.Purple     // #9b59b6
DiscordColor.Magenta    // #e91e63
DiscordColor.DarkBlue   // #206694
DiscordColor.DarkGreen  // #1f8b4c
DiscordColor.DarkRed    // #992d22
DiscordColor.DarkGold   // #c27c0e
DiscordColor.LightGray  // #95a5a6
DiscordColor.DarkGray   // #607d8b
```

## Using Embeds in Commands

```csharp
[SlashCommand("status", "Check bot status")]
public class StatusCommand
{
    public async Task ExecuteAsync(InteractionContext context)
    {
        var embed = new EmbedBuilder()
            .WithTitle("Bot Status")
            .WithDescription("All systems operational")
            .WithColor(DiscordColor.Green)
            .AddField("Latency", $"{context.Bot.Latency}ms", inline: true)
            .AddField("Uptime", GetUptime(), inline: true)
            .AddField("Guilds", context.Bot.Guilds.Count.ToString(), inline: true)
            .WithTimestamp(DateTimeOffset.Now)
            .Build();

        await context.RespondAsync(embed: embed);
    }
}
```

## Multiple Embeds

Send up to 10 embeds in one message:

```csharp
var embed1 = new EmbedBuilder()
    .WithTitle("First Embed")
    .Build();

var embed2 = new EmbedBuilder()
    .WithTitle("Second Embed")
    .Build();

await channel.SendMessageAsync(embeds: new[] { embed1, embed2 });
```

## Embed with Text

Combine regular text with embeds:

```csharp
await channel.SendMessageAsync(
    content: "Check out this information:",
    embed: embed
);
```

## User Profile Example

```csharp
[SlashCommand("userinfo", "Get user information")]
public class UserInfoCommand
{
    public async Task ExecuteAsync(InteractionContext context, User user)
    {
        var member = await context.Guild.GetMemberAsync(user.Id);

        var embed = new EmbedBuilder()
            .WithTitle($"{user.Username}'s Profile")
            .WithThumbnailUrl(user.AvatarUrl)
            .WithColor(DiscordColor.Blue)
            .AddField("Username", user.Username, inline: true)
            .AddField("ID", user.Id.ToString(), inline: true)
            .AddField("Discriminator", user.Discriminator, inline: true)
            .AddField("Joined Server", member.JoinedAt.ToString("MMM dd, yyyy"), inline: true)
            .AddField("Account Created", user.CreatedAt.ToString("MMM dd, yyyy"), inline: true)
            .AddField("Roles", string.Join(", ", member.Roles.Select(r => r.Name)), inline: false)
            .WithFooter($"Requested by {context.User.Username}")
            .WithTimestamp(DateTimeOffset.Now)
            .Build();

        await context.RespondAsync(embed: embed);
    }
}
```

## Error Messages

Use red embeds for errors:

```csharp
var errorEmbed = new EmbedBuilder()
    .WithTitle("❌ Error")
    .WithDescription("You don't have permission to use this command.")
    .WithColor(DiscordColor.Red)
    .Build();

await context.RespondAsync(embed: errorEmbed, ephemeral: true);
```

## Success Messages

Use green embeds for success:

```csharp
var successEmbed = new EmbedBuilder()
    .WithTitle("✅ Success")
    .WithDescription($"Successfully banned {user.Username}")
    .WithColor(DiscordColor.Green)
    .Build();

await context.RespondAsync(embed: successEmbed);
```

## Best Practices

1. **Use colors meaningfully** - Red for errors, green for success, blue for info
2. **Keep descriptions concise** - Use fields for structured data
3. **Add timestamps** - Help users understand when information was generated
4. **Use inline fields** - For compact, side-by-side information
5. **Include footers** - Add context or attribution
6. **Add thumbnails** - Make embeds more visually appealing
7. **Respect limits** - Stay within Discord's character limits
8. **Use emojis sparingly** - Enhance readability without cluttering

## Next Steps

- [Commands](Commands.md) - Use embeds in slash commands
- [Events](Events.md) - Send embeds in response to events
- [API Reference](API-Reference.md) - Full API documentation
