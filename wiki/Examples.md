# Examples

This page provides practical examples using SimpleDiscordDotNet's easy-to-use APIs, perfect for developers with less than 1 year of experience.

## Table of Contents
- [Quick Start](#quick-start)
- [Sending Messages](#sending-messages)
- [Using Embeds](#using-embeds)
- [Adding Buttons](#adding-buttons)
- [Working with Commands](#working-with-commands)
- [Using Context Helpers](#using-context-helpers)
- [Permission Checks](#permission-checks)
- [Formatting Text](#formatting-text)

---

## Quick Start

### Creating a Bot in One Line
```csharp
using SimpleDiscordNet;

var bot = QuickStart.CreateBot("YOUR_BOT_TOKEN", "YOUR_GUILD_ID");
await bot.StartAsync();
```

### Creating a Full-Featured Bot
```csharp
var bot = QuickStart.CreateFullBot("YOUR_BOT_TOKEN", "YOUR_GUILD_ID");
await bot.StartAsync();
```

---

## Sending Messages

### Simple Text Message
```csharp
await bot.SendMessageAsync(channelId, "Hello, world!");
```

### Message with Embed
```csharp
var embed = new EmbedBuilder()
    .WithTitle("Welcome!")
    .WithDescription("Thanks for joining our server")
    .WithColor(DiscordColor.Blue);

await bot.SendMessageAsync(channelId, "Check this out:", embed);
```

### Embed Only (No Text)
```csharp
var embed = new EmbedBuilder()
    .WithTitle("Server Stats")
    .WithDescription("100 members online");

await bot.SendEmbedAsync(channelId, embed);
```

### Message with Buttons
```csharp
var btn1 = ButtonBuilder.Primary("Accept", "accept_btn");
var btn2 = ButtonBuilder.Danger("Decline", "decline_btn");

await bot.SendMessageWithButtonsAsync(channelId, "Do you accept?", btn1, btn2);
```

### Direct Message to User
```csharp
await bot.SendDMAsync(userId, "Hey! This is a private message.");
```

---

## Using Embeds

### Quick Embed Presets
```csharp
// Success message
var success = EmbedPresets.Success("User was banned successfully!");
await bot.SendEmbedAsync(channelId, success);

// Error message
var error = EmbedPresets.Error("Could not find that user");
await bot.SendEmbedAsync(channelId, error);

// Warning message
var warning = EmbedPresets.Warning("This action cannot be undone!");
await bot.SendEmbedAsync(channelId, warning);

// Info message
var info = EmbedPresets.Info("The server will restart in 5 minutes");
await bot.SendEmbedAsync(channelId, info);
```

### Announcement Embed
```csharp
var announcement = EmbedPresets.Announcement(
    "New Feature Released!",
    "We've added slash commands support!"
);
await bot.SendEmbedAsync(channelId, announcement);
```

### User Profile Embed
```csharp
var user = await bot.GetMemberAsync(guildId, userId);
var profile = EmbedPresets.UserProfile(user.User.Username, user.User.GetAvatarUrl());
profile.WithField("Joined", user.Joined_At ?? "Unknown", inline: true);
profile.WithField("Roles", user.Roles?.Length.ToString() ?? "0", inline: true);

await bot.SendEmbedAsync(channelId, profile);
```

### Custom Embed with Colors
```csharp
var embed = new EmbedBuilder()
    .WithTitle("My Custom Embed")
    .WithDescription("This is a custom embed")
    .WithColor(DiscordColor.Blurple)  // Discord's brand color
    .WithThumbnail("https://example.com/image.png")
    .WithFooter("Powered by SimpleDiscordDotNet")
    .WithTimestamp(DateTimeOffset.UtcNow);

await bot.SendEmbedAsync(channelId, embed);
```

---

## Adding Buttons

### Button Styles
```csharp
// Primary (Blurple)
var primary = ButtonBuilder.Primary("Primary", "primary_id");

// Secondary (Grey)
var secondary = ButtonBuilder.Secondary("Secondary", "secondary_id");

// Success (Green)
var success = ButtonBuilder.Success("Success", "success_id");

// Danger (Red)
var danger = ButtonBuilder.Danger("Danger", "danger_id");

// Link (Opens URL)
var link = ButtonBuilder.Link("Visit Website", "https://example.com");

await bot.SendMessageWithButtonsAsync(channelId, "Pick a style:",
    primary, secondary, success, danger, link);
```

### Disabled Button
```csharp
var disabled = ButtonBuilder.Secondary("Disabled", "disabled_id", disabled: true);
await bot.SendMessageWithButtonsAsync(channelId, "This button is disabled:", disabled);
```

---

## Working with Commands

### Simple Command Response
```csharp
[SlashCommand("ping", "Check if the bot is alive")]
public async Task PingAsync(InteractionContext ctx)
{
    await ctx.ReplyAsync("Pong! 🏓");
}
```

### Ephemeral Response (Only User Sees It)
```csharp
[SlashCommand("secret", "Get a secret message")]
public async Task SecretAsync(InteractionContext ctx)
{
    await ctx.ReplyEphemeralAsync("This message is only visible to you! 🤫");
}
```

### Reply with Embed
```csharp
[SlashCommand("info", "Get bot information")]
public async Task InfoAsync(InteractionContext ctx)
{
    var embed = EmbedPresets.Info("I'm a bot built with SimpleDiscordDotNet!");
    await ctx.ReplyWithEmbedAsync("Bot Info:", embed);
}
```

### Reply with Buttons
```csharp
[SlashCommand("verify", "Verify your account")]
public async Task VerifyAsync(InteractionContext ctx)
{
    var verify = ButtonBuilder.Success("Verify", "verify_btn");
    var cancel = ButtonBuilder.Secondary("Cancel", "cancel_btn");

    await ctx.ReplyWithButtonsAsync("Click to verify:", verify, cancel);
}
```

---

## Using Context Helpers

### Getting Command Options
```csharp
[SlashCommand("greet", "Greet a user")]
public async Task GreetAsync(InteractionContext ctx)
{
    // Get option (returns null if not provided)
    string? name = ctx.GetOption("name");

    // Get option with default value
    string greeting = ctx.GetOptionOrDefault("greeting", "Hello");

    await ctx.ReplyAsync($"{greeting}, {name ?? "friend"}!");
}
```

### Getting Integer Options
```csharp
[SlashCommand("repeat", "Repeat a message multiple times")]
public async Task RepeatAsync(InteractionContext ctx)
{
    string? message = ctx.GetOption("message");
    long? count = ctx.GetOptionInt("count");

    if (count.HasValue && count > 10)
    {
        await ctx.ReplyEphemeralAsync("Count must be 10 or less!");
        return;
    }

    var repeated = string.Join("\n", Enumerable.Repeat(message, (int)(count ?? 1)));
    await ctx.ReplyAsync(repeated);
}
```

### Handling Button Clicks
```csharp
[Component("accept_btn")]
public async Task OnAcceptAsync(InteractionContext ctx)
{
    string userId = ctx.UserId;
    string username = ctx.Username;

    await ctx.ReplyAsync($"Thanks for accepting, {username}!");
}
```

### Getting Select Menu Values
```csharp
[Component("role_select")]
public async Task OnRoleSelectAsync(InteractionContext ctx)
{
    string? selectedRole = ctx.GetSelectedValue();

    if (selectedRole is not null)
    {
        await ctx.ReplyAsync($"You selected: {selectedRole}");
    }
}
```

### Getting Modal Input
```csharp
[Component("feedback_modal")]
public async Task OnFeedbackAsync(InteractionContext ctx)
{
    string? feedback = ctx.GetModalValue("feedback_input");
    string? rating = ctx.GetModalValue("rating_input");

    await ctx.ReplyAsync($"Thanks for your feedback!\nRating: {rating}\nFeedback: {feedback}");
}
```

### Checking Context
```csharp
[SlashCommand("serverinfo", "Get server information")]
public async Task ServerInfoAsync(InteractionContext ctx)
{
    if (!ctx.IsInGuild)
    {
        await ctx.ReplyEphemeralAsync("This command only works in a server!");
        return;
    }

    await ctx.ReplyAsync($"Guild ID: {ctx.GuildId}");
}
```

---

## Permission Checks

### Check if User is Admin
```csharp
var member = await bot.GetMemberAsync(guildId, userId);
if (member.IsAdmin())
{
    await bot.SendMessageAsync(channelId, "You have admin permissions!");
}
```

### Check Role Permissions
```csharp
var roles = await bot.GetGuildRolesAsync(guildId);
var modRole = roles?.FirstOrDefault(r => r.Name == "Moderator");

if (modRole is not null)
{
    if (modRole.CanKickMembers())
    {
        await bot.SendMessageAsync(channelId, "Moderators can kick members");
    }

    if (modRole.CanBanMembers())
    {
        await bot.SendMessageAsync(channelId, "Moderators can ban members");
    }

    if (modRole.CanManageMessages())
    {
        await bot.SendMessageAsync(channelId, "Moderators can manage messages");
    }
}
```

### Check if Member Has Role
```csharp
bool hasRole = await bot.MemberHasRoleAsync(guildId, userId, roleId);
if (hasRole)
{
    await bot.SendMessageAsync(channelId, "User has the required role!");
}
```

---

## Formatting Text

### Basic Formatting
```csharp
using SimpleDiscordNet.Extensions;

string bold = DiscordFormatting.Bold("Important!");
string italic = DiscordFormatting.Italic("Emphasis");
string underline = DiscordFormatting.Underline("Notice");
string strikethrough = DiscordFormatting.Strikethrough("Crossed out");

await bot.SendMessageAsync(channelId, $"{bold} {italic} {underline} {strikethrough}");
```

### Code Formatting
```csharp
string inline = DiscordFormatting.Code("variable");
string block = DiscordFormatting.CodeBlock("console.log('Hello');", "javascript");

await bot.SendMessageAsync(channelId, $"Inline: {inline}\n\n{block}");
```

### Mentions
```csharp
string userMention = DiscordFormatting.MentionUser(userId);
string channelMention = DiscordFormatting.MentionChannel(channelId);
string roleMention = DiscordFormatting.MentionRole(roleId);

await bot.SendMessageAsync(channelId,
    $"{userMention} check out {channelMention} for the {roleMention} role!");
```

### Timestamps
```csharp
var now = DateTimeOffset.UtcNow;

string shortTime = DiscordFormatting.Timestamp(now, TimestampStyle.ShortTime);
string relative = DiscordFormatting.Timestamp(now, TimestampStyle.Relative);

await bot.SendMessageAsync(channelId,
    $"Current time: {shortTime}\nRelative: {relative}");
```

### Lists
```csharp
string bulletList = DiscordFormatting.BulletList(
    "First item",
    "Second item",
    "Third item"
);

string numberedList = DiscordFormatting.NumberedList(
    "Step one",
    "Step two",
    "Step three"
);

await bot.SendMessageAsync(channelId, bulletList);
await bot.SendMessageAsync(channelId, numberedList);
```

### Spoilers and Quotes
```csharp
string spoiler = DiscordFormatting.Spoiler("Darth Vader is Luke's father");
string quote = DiscordFormatting.Quote("To be or not to be");

await bot.SendMessageAsync(channelId, $"{spoiler}\n\n{quote}");
```

---

## Complete Example: Welcome Bot

Here's a complete bot that welcomes new members with a beautiful embed and buttons:

```csharp
using SimpleDiscordNet;
using SimpleDiscordNet.Commands;
using SimpleDiscordNet.Extensions;
using SimpleDiscordNet.Primitives;

// Create bot
var bot = QuickStart.CreateFullBot(
    Environment.GetEnvironmentVariable("DISCORD_TOKEN")!,
    Environment.GetEnvironmentVariable("DEV_GUILD_ID")
);

// Subscribe to member join event
DiscordEvents.GuildMemberAdd += async (sender, e) =>
{
    var welcomeChannel = "YOUR_WELCOME_CHANNEL_ID";

    var embed = EmbedPresets.UserProfile(e.Member.User.Username, e.Member.User.GetAvatarUrl())
        .WithTitle($"Welcome to the server, {e.Member.User.DisplayName}!")
        .WithDescription("We're glad to have you here!")
        .WithColor(DiscordColor.DiscordGreen)
        .WithField("Member Count", "1,234", inline: true)
        .WithField("Account Created", DiscordFormatting.Timestamp(DateTimeOffset.UtcNow, TimestampStyle.Relative), inline: true);

    var rulesBtn = ButtonBuilder.Primary("Read Rules", "rules_btn");
    var rolesBtn = ButtonBuilder.Secondary("Get Roles", "roles_btn");

    await bot.SendMessageWithButtonsAsync(welcomeChannel,
        DiscordFormatting.MentionUser(e.Member.User.Id),
        rulesBtn, rolesBtn);

    await bot.SendEmbedAsync(welcomeChannel, embed);
};

await bot.StartAsync();
await Task.Delay(-1);
```

---

## More Examples

For more examples, check out:
- [Commands](Commands) - Full command system documentation
- [Embeds](Embeds) - Advanced embed techniques
- [Events](Events) - All available events
- [API Reference](API-Reference) - Complete API documentation
