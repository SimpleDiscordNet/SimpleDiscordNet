# Getting Started

This guide will help you create your first Discord bot with SimpleDiscordNet in just a few minutes.

## Table of Contents
- [Prerequisites](#prerequisites)
- [Setting Up Your Discord Bot](#setting-up-your-discord-bot)
- [Creating Your Bot Project](#creating-your-bot-project)
- [Writing Your First Command](#writing-your-first-command)
- [Running Your Bot](#running-your-bot)
- [Adding More Features](#adding-more-features)
- [Next Steps](#next-steps)

---

## Prerequisites

Before you begin:

1. **.NET 10 SDK** installed - [Download](https://dotnet.microsoft.com/download/dotnet/10.0)
2. **Discord Account** - [Sign up](https://discord.com)
3. **Code Editor** - Visual Studio, VS Code, or Rider

---

## Setting Up Your Discord Bot

### 1. Create a Discord Application

1. Go to the [Discord Developer Portal](https://discord.com/developers/applications)
2. Click **New Application**
3. Give your application a name (e.g., "My First Bot")
4. Click **Create**

### 2. Create a Bot User

1. In your application, click **Bot** in the left sidebar
2. Click **Add Bot** → **Yes, do it!**
3. Under **Token**, click **Reset Token** → **Copy**
   - **⚠️ Keep this token secret!** Never commit it to version control

### 3. Enable Intents

Scroll down to **Privileged Gateway Intents** and enable:

- ✅ **Presence Intent** (optional)
- ✅ **Server Members Intent** (required for member operations)
- ✅ **Message Content Intent** (NOT required for slash commands)

For now, enable **Server Members Intent**.

### 4. Invite Your Bot to a Server

1. Click **OAuth2** → **URL Generator** in the left sidebar
2. Under **Scopes**, select:
   - ✅ `bot`
   - ✅ `applications.commands`
3. Under **Bot Permissions**, select:
   - ✅ `Send Messages`
   - ✅ `Use Slash Commands`
   - ✅ Add any other permissions you need
4. Copy the generated URL and open it in your browser
5. Select a server and click **Authorize**

### 5. Get Your Guild ID (for Development)

1. In Discord, enable **Developer Mode** (User Settings → Advanced → Developer Mode)
2. Right-click your server → **Copy Server ID**
3. Save this ID - you'll need it for development mode

---

## Creating Your Bot Project

### 1. Create a New Console Project

```bash
dotnet new console -n MyDiscordBot
cd MyDiscordBot
```

### 2. Install SimpleDiscordNet

```bash
dotnet add package SimpleDiscordDotNet
```

### 3. Create a Configuration File

Create `appsettings.json` (optional, for storing config):

```json
{
  "Discord": {
    "Token": "YOUR_BOT_TOKEN_HERE",
    "DevelopmentGuild": "YOUR_GUILD_ID_HERE"
  }
}
```

**Important:** Add `appsettings.json` to `.gitignore`!

```
appsettings.json
appsettings.*.json
```

Better yet, use **environment variables**:

```bash
# Windows PowerShell
$env:DISCORD_TOKEN="your_token_here"

# Windows CMD
set DISCORD_TOKEN=your_token_here

# Linux/Mac
export DISCORD_TOKEN="your_token_here"
```

---

## Writing Your First Command

### 1. Create a Commands File

Create `Commands/BasicCommands.cs`:

```csharp
using SimpleDiscordNet.Commands;

namespace MyDiscordBot.Commands;

public sealed class BasicCommands
{
    [SlashCommand("hello", "Say hello to the bot")]
    public async Task HelloAsync(InteractionContext ctx)
    {
        await ctx.RespondAsync("Hello! 👋", ephemeral: true);
    }

    [SlashCommand("ping", "Check if the bot is responding")]
    public async Task PingAsync(InteractionContext ctx)
    {
        await ctx.RespondAsync("Pong! 🏓");
    }
}
```

**Notes:**
- Command names must be lowercase
- `ephemeral: true` makes the response visible only to the user
- No need to register commands manually - the source generator handles it!

### 2. Set Up Your Bot

Edit `Program.cs`:

```csharp
using SimpleDiscordNet;
using SimpleDiscordNet.Events;
using SimpleDiscordNet.Primitives;

// Get token from environment variable (secure!)
var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN")
    ?? throw new Exception("DISCORD_TOKEN environment variable not set!");

var devGuildId = Environment.GetEnvironmentVariable("DEV_GUILD_ID")
    ?? throw new Exception("DEV_GUILD_ID environment variable not set!");

// Build the bot
var bot = DiscordBot.NewBuilder()
    .WithToken(token)
    .WithIntents(DiscordIntents.Guilds | DiscordIntents.GuildMembers)
    .WithDevelopmentMode(true)  // Commands sync instantly to dev guild
    .WithDevelopmentGuild(devGuildId)
    .Build();

// Subscribe to events
DiscordEvents.Log += (_, msg) => Console.WriteLine($"[{msg.Level}] {msg.Message}");
DiscordEvents.Connected += (_, _) => Console.WriteLine("✅ Bot connected!");
DiscordEvents.Error += (_, ex) => Console.WriteLine($"❌ Error: {ex.Message}");

// Start the bot
Console.WriteLine("Starting bot...");
await bot.StartAsync();

// Keep the bot running
Console.WriteLine("Bot is running. Press Ctrl+C to stop.");
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

try
{
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Shutting down...");
}

await bot.StopAsync();
Console.WriteLine("Bot stopped.");
```

---

## Running Your Bot

### 1. Set Environment Variables

```bash
# Windows PowerShell
$env:DISCORD_TOKEN="your_bot_token_here"
$env:DEV_GUILD_ID="your_guild_id_here"

# Linux/Mac
export DISCORD_TOKEN="your_bot_token_here"
export DEV_GUILD_ID="your_guild_id_here"
```

### 2. Run Your Bot

```bash
dotnet run
```

You should see:

```
Starting bot...
[Information] Bot connected!
✅ Bot connected!
Bot is running. Press Ctrl+C to stop.
```

### 3. Test Your Commands

In your Discord server, type:

```
/hello
/ping
```

Your bot should respond!

**Troubleshooting:**
- Commands not showing? Wait a few seconds and refresh Discord (Ctrl+R)
- In development mode, commands sync instantly to your dev guild
- Check the console for any errors

---

## Adding More Features

### Command with Parameters

```csharp
[SlashCommand("greet", "Greet someone")]
public async Task GreetAsync(InteractionContext ctx, string name)
{
    await ctx.RespondAsync($"Hello, {name}! 👋");
}
```

### Command Groups

```csharp
[SlashCommandGroup("admin", "Admin commands")]
public sealed class AdminCommands
{
    [SlashCommand("announce", "Make an announcement")]
    public async Task AnnounceAsync(InteractionContext ctx, string message)
    {
        await ctx.RespondAsync($"📢 Announcement: {message}");
    }
}
```

Usage: `/admin announce Hello everyone!`

### Rich Embeds

```csharp
using SimpleDiscordNet.Primitives;

[SlashCommand("info", "Show bot info")]
public async Task InfoAsync(InteractionContext ctx)
{
    var embed = new EmbedBuilder()
        .WithTitle("Bot Information")
        .WithDescription("A simple bot built with SimpleDiscordNet!")
        .WithColor(DiscordColor.Blue)
        .AddField("Version", "1.0.0", inline: true)
        .AddField("Library", "SimpleDiscordNet", inline: true);

    await ctx.RespondAsync(embed: embed);
}
```

---

## Next Steps

Congratulations! You've created your first Discord bot. Here's what to explore next:

### Learn More Features
- **[Slash Commands](Slash-Commands)** - Advanced command features
- **[Components & Modals](Components-and-Modals)** - Interactive buttons and forms
- **[Messages & Embeds](Messages-and-Embeds)** - Rich message formatting
- **[Events](Events)** - Respond to Discord events

### Add Bot Features
- **[Reactions](Reactions)** - Add reaction-based features
- **[Permissions & Roles](Permissions-and-Roles)** - Manage server roles
- **[Moderation](Moderation)** - Kick, ban, and moderate members
- **[DiscordContext](DiscordContext)** - Access server data from anywhere

### Advanced Topics
- **[Dependency Injection](Dependency-Injection)** - Use DI containers
- **[AOT & Trimming](AOT-and-Trimming)** - Native AOT compilation
- **[Examples](Examples)** - Complete bot examples

---

## Common Issues

### Commands Not Showing

**Problem:** `/hello` doesn't appear in Discord

**Solutions:**
1. Refresh Discord (Ctrl+R or Cmd+R)
2. Wait 5-10 seconds for sync to complete
3. Verify bot has `applications.commands` scope
4. Check console for sync errors

### Bot Not Connecting

**Problem:** Bot doesn't connect or immediately disconnects

**Solutions:**
1. Verify token is correct
2. Check intents are enabled in Developer Portal
3. Ensure token environment variable is set
4. Check console for error messages

### Permission Denied

**Problem:** Bot can't perform actions

**Solutions:**
1. Check bot has required permissions in server
2. Verify bot's role is high enough in role hierarchy
3. Check channel-specific permissions

---

**Need more help?** Check the [FAQ](FAQ) or [open an issue](https://github.com/SimpleDiscordNet/SimpleDiscordNet/issues).
