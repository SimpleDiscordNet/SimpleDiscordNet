# Beginner's Guide to Discord Bots

Welcome! This guide will help you create your first Discord bot from scratch, even if you've never built one before.

## Table of Contents

1. [What is a Discord Bot?](#what-is-a-discord-bot)
2. [Prerequisites](#prerequisites)
3. [Creating Your Bot Account](#creating-your-bot-account)
4. [Setting Up Your Project](#setting-up-your-project)
5. [Your First Bot](#your-first-bot)
6. [Adding Slash Commands](#adding-slash-commands)
7. [Working with Messages](#working-with-messages)
8. [Responding to Events](#responding-to-events)
9. [Common Patterns](#common-patterns)
10. [Next Steps](#next-steps)

## What is a Discord Bot?

A Discord bot is an automated program that runs on Discord servers. Bots can:
- Respond to commands from users
- Send messages automatically
- Manage channels, roles, and members
- React to events like users joining or messages being sent
- Play music, run games, moderate content, and much more!

## Prerequisites

Before you begin, you'll need:

1. **.NET SDK 10.0 or newer**
   - Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download)
   - Verify installation: Open a terminal and type `dotnet --version`

2. **A code editor**
   - Visual Studio 2022, VS Code, or Rider
   - Any text editor works, but IDEs provide better autocomplete and debugging

3. **A Discord account**
   - Sign up at [discord.com](https://discord.com) if you don't have one

## Creating Your Bot Account

### Step 1: Go to the Discord Developer Portal

1. Visit [discord.com/developers/applications](https://discord.com/developers/applications)
2. Click "New Application" in the top right
3. Give your application a name (e.g., "My First Bot")
4. Click "Create"

### Step 2: Create a Bot User

1. Click on "Bot" in the left sidebar
2. Click "Add Bot" and confirm
3. Under "Token", click "Reset Token" and copy it
   - **⚠️ Important**: Keep this token secret! Anyone with it can control your bot

### Step 3: Enable Intents

Intents tell Discord what events your bot wants to receive.

1. Scroll down to "Privileged Gateway Intents"
2. Enable these intents:
   - ✅ **Server Members Intent** (to see guild members)
   - ✅ **Message Content Intent** (to read message content)
3. Click "Save Changes"

### Step 4: Invite Your Bot to a Server

1. Click on "OAuth2" > "URL Generator" in the left sidebar
2. Under "Scopes", check:
   - ✅ `bot`
   - ✅ `applications.commands`
3. Under "Bot Permissions", select the permissions your bot needs:
   - For beginners, check "Administrator" (you can refine this later)
4. Copy the generated URL at the bottom
5. Open the URL in your browser and select a server to add the bot to

## Setting Up Your Project

### Step 1: Create a New Project

Open a terminal and run:

```bash
mkdir MyDiscordBot
cd MyDiscordBot
dotnet new console
```

### Step 2: Install SimpleDiscordNet

```bash
dotnet add package SimpleDiscordDotNet
```

### Step 3: Store Your Token Securely

**Never hardcode your token in your code!** Instead, use an environment variable or a configuration file.

#### Option A: Environment Variable (Recommended)

**Windows (PowerShell):**
```powershell
$env:DISCORD_TOKEN = "your_token_here"
```

**Linux/macOS:**
```bash
export DISCORD_TOKEN="your_token_here"
```

#### Option B: Configuration File

Create a file named `appsettings.json`:

```json
{
  "Discord": {
    "Token": "your_token_here"
  }
}
```

Add to your `.csproj`:
```xml
<ItemGroup>
  <None Update="appsettings.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

**⚠️ Add `appsettings.json` to `.gitignore` if using Git!**

## Your First Bot

Replace the contents of `Program.cs` with:

```csharp
using SimpleDiscordNet;

// Get token from environment variable
string? token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
if (string.IsNullOrEmpty(token))
{
    Console.WriteLine("Error: DISCORD_TOKEN environment variable not set!");
    return;
}

// Create and configure the bot
var bot = DiscordBot.NewBuilder()
    .WithToken(token)
    .WithIntents(DiscordIntents.Guilds | DiscordIntents.GuildMessages | DiscordIntents.MessageContent)
    .Build();

// Log when bot connects
bot.OnConnected += (sender, e) =>
{
    Console.WriteLine("Bot connected! Press Ctrl+C to stop.");
};

// Start the bot
await bot.StartAsync();

// Keep the bot running
await Task.Delay(Timeout.Infinite);
```

### Run Your Bot

```bash
dotnet run
```

You should see "Bot connected!" in the console. Your bot is now online! 🎉

## Adding Slash Commands

Slash commands are the modern way to interact with Discord bots. Let's create some!

### Step 1: Create a Commands Class

Create a new file `Commands.cs`:

```csharp
using SimpleDiscordNet.Commands;
using SimpleDiscordNet.Primitives;

public sealed class MyCommands
{
    [SlashCommand("hello", "Say hello")]
    public async Task HelloAsync(InteractionContext ctx)
    {
        await ctx.RespondAsync($"Hello, {ctx.User?.Username}! 👋");
    }

    [SlashCommand("ping", "Check if the bot is responsive")]
    public async Task PingAsync(InteractionContext ctx)
    {
        await ctx.RespondAsync("Pong! 🏓");
    }

    [SlashCommand("serverinfo", "Get information about this server")]
    public async Task ServerInfoAsync(InteractionContext ctx)
    {
        var guild = ctx.Guild;
        if (guild == null)
        {
            await ctx.RespondAsync("This command only works in a server!");
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle(guild.Name)
            .WithDescription($"Server ID: {guild.Id}")
            .AddField("Owner", $"<@{guild.Owner_Id}>", inline: true)
            .AddField("Members", guild.Members.Count.ToString(), inline: true)
            .AddField("Channels", guild.Channels.Count.ToString(), inline: true)
            .WithColor(0x5865F2);

        await ctx.RespondAsync(embed: embed);
    }

    [SlashCommand("echo", "Repeat your message")]
    [SlashCommandOption("message", "The message to repeat", OptionType.String, required: true)]
    public async Task EchoAsync(InteractionContext ctx)
    {
        string? message = ctx.GetOption("message");
        await ctx.RespondAsync(message ?? "No message provided");
    }
}
```

### Step 2: Update Program.cs

The bot automatically discovers commands through source generation. No additional setup needed!

### Step 3: Sync Commands to Discord

After starting your bot for the first time with commands, Discord needs to know about them. You have two options:

#### Option A: Guild Commands (Fast, for development)

```csharp
// After bot.StartAsync()
await bot.SyncSlashCommandsAsync(new[] { "YOUR_GUILD_ID_HERE" });
```

Guild commands update instantly (perfect for testing).

#### Option B: Global Commands (Slow, for production)

```csharp
// After bot.StartAsync()
await bot.SyncSlashCommandsAsync();
```

Global commands take up to 1 hour to update (use for production).

**Complete example:**

```csharp
await bot.StartAsync();

// Sync commands (do this once, or when you add/change commands)
await bot.SyncSlashCommandsAsync(new[] { "YOUR_GUILD_ID_HERE" });
Console.WriteLine("Commands synced!");

await Task.Delay(Timeout.Infinite);
```

### Step 4: Try Your Commands!

In Discord, type `/` and you should see your bot's commands appear!

## Working with Messages

### Sending Messages

```csharp
using SimpleDiscordNet.Context;

// Get a channel from cache
var channel = DiscordContext.GetChannel(channelId);
if (channel != null)
{
    await channel.SendMessageAsync("Hello from code!");
}
```

### Sending Embeds

Embeds are rich, formatted messages:

```csharp
var embed = new EmbedBuilder()
    .WithTitle("Welcome!")
    .WithDescription("Thanks for joining our server")
    .WithColor(0x00FF00) // Green
    .AddField("Field Title", "Field Value", inline: false)
    .WithFooter("Bot made with SimpleDiscordNet")
    .WithTimestamp(DateTimeOffset.Now);

await channel.SendMessageAsync(embed: embed);
```

### Sending Buttons

```csharp
var button = new Button("Click me!", "my_button_id", ButtonStyle.Primary);

var message = new MessageBuilder()
    .WithContent("Click the button below!")
    .WithComponents(button);

await channel.SendMessageAsync(message);
```

### Handling Button Clicks

```csharp
[Component("my_button_id")]
public async Task HandleButtonAsync(InteractionContext ctx)
{
    await ctx.RespondAsync("You clicked the button! ✨");
}
```

## Responding to Events

Events let your bot react to things happening in Discord.

### Message Created Event

```csharp
using SimpleDiscordNet.Events;

// In your bot setup
bot.OnMessageCreated += async (sender, e) =>
{
    var message = e.Message;

    // Ignore bot messages
    if (message.Author.IsBot) return;

    // Respond to mentions
    if (message.Content.Contains($"<@{bot.CurrentUser?.Id}>"))
    {
        var channel = DiscordContext.GetChannel(message.ChannelId);
        if (channel != null)
            await channel.SendMessageAsync("You mentioned me!");
    }
};
```

### Member Joined Event

```csharp
bot.OnGuildMemberAdded += async (sender, e) =>
{
    var member = e.Member;
    var guild = e.Guild;

    // Find a welcome channel
    var welcomeChannel = guild.Channels
        .FirstOrDefault(c => c.Name == "welcome");

    if (welcomeChannel != null)
    {
        await welcomeChannel.SendMessageAsync(
            $"Welcome to {guild.Name}, {member.User.Username}! 🎉"
        );
    }
};
```

### Role Added Event

```csharp
bot.OnRoleCreated += (sender, e) =>
{
    Console.WriteLine($"New role created: {e.Role.Name}");
};
```

## Common Patterns

### Auto-Delete Messages After Delay

```csharp
[SlashCommand("tempannounce", "Send a temporary announcement")]
[SlashCommandOption("message", "The message", OptionType.String, required: true)]
public async Task TempAnnounceAsync(InteractionContext ctx)
{
    string? message = ctx.GetOption("message");

    // Send the message
    var sent = await ctx.Channel?.SendMessageAsync(message ?? "")!;

    // Confirm to user
    await ctx.RespondAsync("Announcement sent! It will be deleted in 10 seconds.", ephemeral: true);

    // Wait 10 seconds
    await Task.Delay(TimeSpan.FromSeconds(10));

    // Delete the message
    await sent.DeleteAsync();
}
```

### Role Menu with Buttons

```csharp
[SlashCommand("rolemenu", "Create a role selection menu")]
public async Task RoleMenuAsync(InteractionContext ctx)
{
    var buttons = new[]
    {
        new Button("Get Updates", "role_updates", ButtonStyle.Primary),
        new Button("Get Events", "role_events", ButtonStyle.Success),
        new Button("Get News", "role_news", ButtonStyle.Secondary)
    };

    var message = new MessageBuilder()
        .WithContent("Select roles to receive notifications:")
        .WithComponents(buttons);

    await ctx.RespondAsync(message);
}

[Component("role_updates")]
public async Task RoleUpdatesAsync(InteractionContext ctx)
{
    var member = ctx.Member;
    if (member == null) return;

    ulong roleId = 123456789; // Your role ID
    await member.AddRoleAsync(roleId);
    await ctx.RespondAsync("Role added! ✅", ephemeral: true);
}
```

### Moderator-Only Commands

```csharp
[SlashCommand("kick", "Kick a user")]
[SlashCommandOption("user", "User to kick", OptionType.User, required: true)]
public async Task KickAsync(InteractionContext ctx)
{
    var member = ctx.Member;
    if (member == null) return;

    // Check if user has kick permissions (permission value for KICK_MEMBERS is 2)
    if ((member.Permissions & 2) == 0)
    {
        await ctx.RespondAsync("❌ You don't have permission to kick members!", ephemeral: true);
        return;
    }

    // Your kick logic here
    await ctx.RespondAsync("User kicked!", ephemeral: true);
}
```

## Next Steps

Congratulations! You now know the basics of creating Discord bots. Here's what to learn next:

### Intermediate Topics

1. **[Working with Entities](Entities.md)** - Deep dive into channels, guilds, members, and more
2. **[Commands](Commands.md)** - Advanced command patterns, modals, and select menus
3. **[Events](Events.md)** - Complete list of all gateway events

### Advanced Topics

4. **[Configuration](Configuration.md)** - Dependency injection, logging, and advanced setup
5. **[Sharding](Sharding.md)** - Scale your bot across multiple processes
6. **[Rate Limit Monitoring](Rate-Limit-Monitoring.md)** - Monitor API usage and prevent rate limits

### Resources

- **[API Reference](API-Reference.md)** - Complete API documentation
- **[FAQ](FAQ.md)** - Common questions and troubleshooting
- **Discord Developer Portal** - [discord.com/developers/docs](https://discord.com/developers/docs)

## Tips for Success

1. **Start Small** - Build simple commands first, then add complexity
2. **Test Locally** - Use a test server to avoid spamming your main server
3. **Read Error Messages** - They usually tell you exactly what's wrong
4. **Check Intents** - Many features require specific intents to be enabled
5. **Use Ephemeral Responses** - For error messages or confirmations, use `ephemeral: true`
6. **Join the Community** - Ask questions in Discord bot development servers

## Common Issues

### "Bot not responding to commands"
- Make sure you called `SyncSlashCommandsAsync()` after adding commands
- Guild commands update instantly; global commands take up to 1 hour
- Check that your bot has the `applications.commands` scope

### "Missing Access" errors
- Verify your bot has the required permissions in Discord
- Check that intents are enabled in the Developer Portal
- Ensure the bot role is high enough in the role hierarchy

### "Token is invalid"
- Copy the token again from the Developer Portal
- Make sure there are no extra spaces when setting the environment variable
- Never share your token publicly!

## Example: Complete Bot

Here's a complete bot with multiple features:

```csharp
using SimpleDiscordNet;
using SimpleDiscordNet.Commands;
using SimpleDiscordNet.Primitives;
using SimpleDiscordNet.Context;
using SimpleDiscordNet.Events;

// Commands class
public sealed class BotCommands
{
    [SlashCommand("info", "Get bot information")]
    public async Task InfoAsync(InteractionContext ctx)
    {
        var embed = new EmbedBuilder()
            .WithTitle("Bot Information")
            .WithDescription("A helpful Discord bot!")
            .AddField("Servers", DiscordContext.Guilds.Count.ToString(), inline: true)
            .AddField("Channels", DiscordContext.Channels.Count.ToString(), inline: true)
            .WithColor(0x5865F2);

        await ctx.RespondAsync(embed: embed);
    }

    [SlashCommand("poll", "Create a simple poll")]
    [SlashCommandOption("question", "The poll question", OptionType.String, required: true)]
    public async Task PollAsync(InteractionContext ctx)
    {
        string? question = ctx.GetOption("question");

        var embed = new EmbedBuilder()
            .WithTitle("📊 Poll")
            .WithDescription(question)
            .WithColor(0xFFAA00);

        var buttons = new[]
        {
            new Button("👍 Yes", "poll_yes", ButtonStyle.Success),
            new Button("👎 No", "poll_no", ButtonStyle.Danger)
        };

        var message = new MessageBuilder()
            .WithEmbed(embed.Build())
            .WithComponents(buttons);

        await ctx.RespondAsync(message);
    }
}

// Main program
class Program
{
    static async Task Main()
    {
        string? token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
        if (string.IsNullOrEmpty(token))
        {
            Console.WriteLine("Error: DISCORD_TOKEN not set!");
            return;
        }

        var bot = DiscordBot.NewBuilder()
            .WithToken(token)
            .WithIntents(DiscordIntents.Guilds | DiscordIntents.GuildMessages | DiscordIntents.MessageContent)
            .Build();

        // Event: Bot connected
        bot.OnConnected += (sender, e) =>
        {
            Console.WriteLine($"✅ Connected as {bot.CurrentUser?.Username}");
        };

        // Event: New member joined
        bot.OnGuildMemberAdded += async (sender, e) =>
        {
            var welcomeChannel = e.Guild.Channels.FirstOrDefault(c => c.Name == "welcome");
            if (welcomeChannel != null)
            {
                await welcomeChannel.SendMessageAsync($"Welcome {e.Member.User.Username}! 🎉");
            }
        };

        await bot.StartAsync();

        // Sync commands (replace with your guild ID for testing)
        await bot.SyncSlashCommandsAsync(new[] { "YOUR_GUILD_ID" });
        Console.WriteLine("✅ Commands synced!");

        await Task.Delay(Timeout.Infinite);
    }
}
```

Happy bot building! 🤖✨
