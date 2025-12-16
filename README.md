SimpleDiscordNet
================

A tiny, dependency‑free Discord bot SDK for .NET 10 that talks directly to the Discord API v10 (REST + Gateway).

- Slash commands only (Discord is deprecating text prefix commands)
- No extra NuGet packages required
- Builder pattern and DI‑friendly
- Global static event hub for logs and domain events
- Simple embeds, attachments, color helpers
- Minimal, rate‑limit‑aware REST client
- Ambient attributes + context to access cached Guilds/Channels/Members/Users from anywhere

This project is designed for simplicity, performance, and low memory usage, while keeping a small, approachable API.


Requirements
------------
- .NET SDK 10.0 or newer (C# 14)
- A Discord application with a Bot token
- Gateway Intents configured as needed (in the Discord Developer Portal → Bot):
  - Required for most features: Guilds
  - For member join/update/leave events and ambient members/users: GuildMembers (privileged)
  - For ban events: GuildBans (privileged)
  - To receive direct messages (DMs): DirectMessages
  - MessageContent is NOT required for slash commands or DMs (only needed for text prefix commands)


Install / Add to your project
-----------------------------
SimpleDiscordNet is a source project (no external dependencies). You have two easy ways to use it.

1) Add as a project reference
- Clone or copy this repository somewhere alongside your app.
- In your app solution, add the project and reference it:

```powershell
# from your solution folder
dotnet sln add .\SimpleDiscordNet\SimpleDiscordNet\SimpleDiscordNet.csproj

dotnet add <YourApp.csproj> reference .\SimpleDiscordNet\SimpleDiscordNet\SimpleDiscordNet.csproj
```

2) (Optional) Pack as a local NuGet and install
```powershell
cd .\SimpleDiscordNet\SimpleDiscordNet
dotnet pack -c Release
# Find the produced nupkg under bin\Release and install it to your app project
dotnet add <YourApp.csproj> package SimpleDiscordNet --source <path-to-your-local-packages>
```


Quick start (Builder)
---------------------
```csharp
using SimpleDiscordNet;
using SimpleDiscordNet.Commands;
using SimpleDiscordNet.Events;
using SimpleDiscordNet.Primitives;

// 1) Build the bot
var bot = DiscordBot.NewBuilder()
    .WithToken("YOUR_BOT_TOKEN")
    .WithIntents(DiscordIntents.Guilds | DiscordIntents.GuildMembers | DiscordIntents.DirectMessages) // add DirectMessages to receive DMs; pick only what you need
    .WithDevelopmentMode(true)                                        // instant per‑guild slash sync (dev)
    .WithDevelopmentGuild("YOUR_DEV_GUILD_ID")
    .Build();

// 2) Log/connection events (global, static)
DiscordEvents.Log += (_, m) => Console.WriteLine($"[{m.Level}] {m.Message}");
DiscordEvents.Connected += (_, __) => Console.WriteLine("Connected");
DiscordEvents.Error += (_, ex) => Console.WriteLine($"Error: {ex}");

// 3) Start (commands are auto‑discovered and registered on startup;
//    in dev mode, commands sync immediately to your dev guild before connecting)
await bot.StartAsync();
// Note: StartAsync connects and returns; your app must keep running.
// See the "Keeping the app running" section below for recommended patterns.
```

Example commands:
```csharp
using SimpleDiscordNet.Commands;

public sealed class AppCommands
{
    [SlashCommand("hello", "Say hello")]
    public async Task HelloAsync(InteractionContext ctx)
        => await ctx.RespondAsync("Hello!", ephemeral: true);
}

[SlashCommandGroup("util", "Utility commands")]
public sealed class UtilCommands
{
    [SlashCommand("ping", "Ping the bot")]
    public async Task PingAsync(InteractionContext ctx)
        => await ctx.RespondAsync("Pong!");

    [SlashCommand("uptime", "Show uptime")]
    public async Task UptimeAsync(InteractionContext ctx)
        => await ctx.RespondAsync("I'm alive!");
}
```

Notes
- Names are normalized to lowercase and must be 1–32 chars of a–z, 0–9, '-', '_'.
- Grouped commands become subcommands under the top‑level group.
- In development mode, per‑guild sync is instant; in production you can call SyncSlashCommandsAsync with specific guilds.


Quick start (DI‑friendly)
-------------------------
You can inject the bot via the IDiscordBot interface using your DI container. The library does not depend on Microsoft.Extensions.* — use your own app’s references.

```csharp
using Microsoft.Extensions.DependencyInjection; // in your app
using SimpleDiscordNet;
using SimpleDiscordNet.Primitives;

var services = new ServiceCollection();
services.AddSingleton<IDiscordBot>(sp =>
{
    var opts = new DiscordBotOptions
    {
        Token = configuration["Discord:Token"],
        Intents = DiscordIntents.Guilds | DiscordIntents.GuildMembers | DiscordIntents.DirectMessages,
        DevelopmentMode = true,
        DevelopmentGuildIds = new List<string> { "YOUR_DEV_GUILD_ID" },
        // Mirror logs to your sink (optional)
        LogSink = m => Console.WriteLine($"[{m.Level}] {m.Message}"),
        // NEW: control verbosity
        MinimumLogLevel = SimpleDiscordNet.Logging.LogLevel.Information
    };
    return DiscordBot.FromOptions(opts);
});

var provider = services.BuildServiceProvider();
var bot = provider.GetRequiredService<IDiscordBot>();

// Start (commands are auto‑discovered on startup)
await bot.StartAsync();
```


Keeping the app running (don’t exit immediately)
-----------------------------------------------
StartAsync connects and returns quickly; the bot continues running in the background. Your app must keep the process alive. Here are clean, copy‑paste patterns you can pick from:

1) Console app with Ctrl+C to stop (recommended)
```csharp
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

await bot.StartAsync(cts.Token);

// Block until Ctrl+C is pressed
try { await Task.Delay(Timeout.Infinite, cts.Token); }
catch (OperationCanceledException) { /* expected on Ctrl+C */ }

await bot.StopAsync();
```

2) Public flag you can flip from anywhere
```csharp
public static class AppState
{
    public static volatile bool Running = true;
}

// Somewhere central, e.g., Program.cs
Console.CancelKeyPress += (_, e) => { e.Cancel = true; AppState.Running = false; };

await bot.StartAsync();
while (AppState.Running)
{
    await Task.Delay(250);
}
await bot.StopAsync();
```

3) Short‑lived demo (e.g., run for 10 seconds and exit)
```csharp
await bot.StartAsync();
await Task.Delay(TimeSpan.FromSeconds(10));
await bot.StopAsync();
```

Ambient data access with attributes
-----------------------------------
Mark any class or method in any project with these attributes to indicate you intend to consume cached data. Then read from DiscordContext. This is ambient (not tied to CommandContext) and works anywhere in your app.

Available attribute (namespace: SimpleDiscordNet.Attributes):
- [DiscordContext]

Access the data via SimpleDiscordNet.Context.DiscordContext:
```csharp
using SimpleDiscordNet.Attributes;
using SimpleDiscordNet.Context;

[DiscordContext]
public sealed class MyService
{
    public void DoWork()
    {
        var channels = DiscordContext.Channels;   // IReadOnlyList<ChannelWithGuild>
        var guilds   = DiscordContext.Guilds;     // IReadOnlyList<Guild>
    }
}

[DiscordContext]
public sealed class AnotherService
{
    public void Inspect()
    {
        var members = DiscordContext.Members;     // IReadOnlyList<MemberWithGuild>
        var users   = DiscordContext.Users;       // IReadOnlyList<UserWithGuild>
    }
}
```

Preloading cache
- On Start, the bot registers an ambient provider so DiscordContext returns snapshots.
- You can preload guilds/channels/members via builder or options (members are heavy and require GuildMembers intent).

Builder (example):
```csharp
var bot = DiscordBot.NewBuilder()
    .WithToken("...")
    .WithIntents(DiscordIntents.Guilds | DiscordIntents.GuildMembers)
    .WithPreloadOnStart(guilds: true, channels: true, members: false)
    .Build();
```


Events (global static)
----------------------
Subscribe anywhere using SimpleDiscordNet.Events.DiscordEvents:
- Connected, Disconnected, Error, Log
- GuildAdded, GuildUpdated, GuildRemoved
- ChannelCreated, ChannelUpdated, ChannelDeleted
- MemberJoined, MemberUpdated, MemberLeft
- BanAdded, BanRemoved
- BotUserUpdated (bot user only)

```csharp
using SimpleDiscordNet.Events;

DiscordEvents.GuildAdded += (_, e) => Console.WriteLine($"Guild added: {e.Guild.Name}");
DiscordEvents.ChannelCreated += (_, e) => Console.WriteLine($"#{e.Channel.Name} in {e.Guild.Name}");
DiscordEvents.MemberJoined += (_, e) => Console.WriteLine($"Member joined: {e.User.Username} -> {e.Guild.Name}");
DiscordEvents.Log += (_, m) => Console.WriteLine($"[{m.Level}] {m.Message}");
```


Sending messages, embeds, attachments
------------------------------------
```csharp
using SimpleDiscordNet.Primitives;

// Simple message
await bot.SendMessageAsync(channelId, "Hello world");

// Embed
var embed = new EmbedBuilder()
    .WithTitle("Hi")
    .WithDescription("From SimpleDiscordNet")
    .WithColor(DiscordColor.Blue);

await bot.SendMessageAsync(channelId, "Hello with embed", embed);

// Attachment
var bytes = System.Text.Encoding.UTF8.GetBytes("hello.txt content");
await bot.SendAttachmentAsync(channelId, "Here is a file", "hello.txt", bytes, embed);
```


REST helpers
------------
```csharp
var guild = await bot.GetGuildAsync(guildId);
var channels = await bot.GetGuildChannelsAsync(guildId);
var roles = await bot.GetGuildRolesAsync(guildId);
var members = await bot.ListGuildMembersAsync(guildId, limit: 1000);
```


Development mode (instant command sync)
---------------------------------------
Waiting for global slash commands to propagate is slow during development. Enable dev mode and specify guild ids — the bot will PUT the entire current command set to those guilds on Start/StartAsync so changes appear immediately.

```csharp
var bot = DiscordBot.NewBuilder()
    .WithToken("...")
    .WithDevelopmentMode(true)
    .WithDevelopmentGuild("YOUR_DEV_GUILD_ID")
    .Build();

await bot.StartAsync(); // syncs immediately to dev guild(s) before connecting
```

In production you can call:
```csharp
await bot.SyncSlashCommandsAsync(new [] { "GUILD_ID_A", "GUILD_ID_B" });
```


Limits and scope
----------------
- Discord API: v10
- Commands: Slash commands (no text prefix commands)
- Voice: Not supported
- Rate limiting: Minimal per‑route limiter + 429 Retry‑After handling. Avoid spamming routes; respect Discord’s limits.
- Caching: In‑memory only, per process. No persistence between runs. Ambient snapshots are read‑only.
- Slash command options: Basic handling focused on subcommand + simple option values (string/int/bool). You can extend locally if you need more complex option types.
- Intents: You must enable the intents your app requires (GuildMembers for member lists/events, GuildBans for bans). If not enabled in the Developer Portal and on the bot, related data/events will be empty or absent.
- DMs: Channel/member events are not cached, but direct messages are surfaced via a new global event `DiscordEvents.DirectMessageReceived` and an info log entry. You can subscribe to handle DMs.
- Dependencies: No external packages; everything uses BCL APIs.


Components and Modals (super simple)
------------------------------------
SimpleDiscordNet supports all Discord components and modals with attribute‑based handlers and a fluent context.

Key points
- Handlers are matched by `custom_id` (exact or prefix) using `[ComponentHandler]` attributes.
- Ephemeral responses default to false.
- You can send immediate responses, update the original message, open modals, or send follow‑ups.

Sending messages with components
```csharp
using SimpleDiscordNet.Commands;
using SimpleDiscordNet.Primitives;

public sealed class ComponentDemo
{
    [SlashCommand("components", "Show buttons and selects")] // trigger message with components
    public async Task ShowAsync(InteractionContext ctx)
    {
        await ctx.RespondAsync(
            "Choose an option:",
            components: new IComponent[]
            {
                new Button(label: "Yes", customId: "choose:yes", style: 1),
                new Button(label: "No",  customId: "choose:no",  style: 4)
            }
        );
    }

    [ComponentHandler("choose:", prefix: true)]
    public async Task HandleChoiceAsync(InteractionContext ctx)
    {
        // When a button is clicked, ComponentService defers the update for you.
        // Use UpdateMessageAsync to modify the original message.
        await ctx.UpdateMessageAsync("Thanks! You clicked a button.");
    }
}
```

All select menus
```csharp
public sealed class SelectsDemo
{
    [SlashCommand("selects", "Show all select types")] 
    public async Task ShowSelectsAsync(InteractionContext ctx)
    {
        await ctx.RespondAsync(
            "All selects:",
            components: new IComponent[]
            {
                new StringSelect("sel:string", new []
                {
                    new SelectOption("Red",   "red"),
                    new SelectOption("Green", "green"),
                    new SelectOption("Blue",  "blue"),
                }, placeholder: "Pick colors", min: 1, max: 2),
                new UserSelect("sel:user", placeholder: "Pick user"),
                new RoleSelect("sel:role", placeholder: "Pick role"),
                new MentionableSelect("sel:mention", placeholder: "Pick user/role"),
                new ChannelSelect("sel:channel", new [] { ChannelType.GUILD_TEXT }, placeholder: "Pick text channel")
            }
        );
    }

    [ComponentHandler("sel:", prefix: true)]
    public async Task HandleSelectsAsync(InteractionContext ctx)
    {
        // Access selected values directly from the context
        // Works for string selects and entity selects (user/role/mentionable/channel)
        var values = ctx.SelectedValues; // IReadOnlyList<string>

        string picked = values.Count == 0 ? "(none)" : string.Join(", ", values);
        await ctx.UpdateMessageAsync($"Selection received: {picked}");
    }
}
```

Modals (open and handle submit)
```csharp
public sealed class ModalDemo
{
    // IMPORTANT: Opening a modal must be the first response.
    // Disable auto-defer for this handler so the modal can be sent immediately.
    [SlashCommand("modal", "Open a modal")] 
    [AutoDefer(false)]
    public async Task ShowModalAsync(InteractionContext ctx)
    {
        // Show a modal with two inputs
        await ctx.OpenModalAsync(
            customId: "edit_user_modal",
            title: "Edit user",
            new ActionRow(new TextInput("name", "Name", style: 1, required: true)),
            new ActionRow(new TextInput("bio",  "Bio",  style: 2, maxLength: 200))
        );
    }

    // Handle modal submission by custom_id
    [ComponentHandler("edit_user_modal")]
    public async Task HandleModalAsync(InteractionContext ctx)
    {
        // Read submitted inputs directly from the context
        string? name = ctx.Modal?.Inputs.FirstOrDefault(i => i.CustomId == "name")?.Value;
        string? bio  = ctx.Modal?.Inputs.FirstOrDefault(i => i.CustomId == "bio")?.Value;
        await ctx.RespondAsync($"Saved: {name} / {bio}");
    }
}
```

Open a modal from a component click (button/select)
```csharp
public sealed class EditFromComponentDemo
{
    // Disable auto-defer because we want to open a modal instead of deferring the update
    [ComponentHandler("edit:user")]
    [AutoDefer(false)]
    public async Task OnEditUserAsync(InteractionContext ctx)
    {
        await ctx.OpenModalAsync(
            customId: "edit_user_modal",
            title: "Edit user",
            new ActionRow(new TextInput("name", "Name", style: 1, required: true))
        );
    }
}
```

Notes
- `[ComponentHandler]` can be applied multiple times per method. Use `prefix: true` to route a family of `custom_id` values.
- For component clicks (buttons/selects), the library auto‑defers the update by default; call `UpdateMessageAsync` or send a follow‑up with `RespondAsync(...)` after deferring. You can disable auto‑defer per handler with `[AutoDefer(false)]`.
- For modals, call `OpenModalAsync(...)` as the initial response; if you plan to open a modal from a slash command or a component handler, annotate the method with `[AutoDefer(false)]` so the first response can be the modal. The submit is routed by the modal’s `custom_id`.

Advanced: full interaction details in context
- `ctx.Type` → InteractionType (ApplicationCommand, MessageComponent, ModalSubmit)
- `ctx.Command`, `ctx.Component`, `ctx.Modal` → typed accessors to current interaction data
- `ctx.CustomId`, `ctx.MessageId`, `ctx.SelectedValues` → common convenience properties
- `ctx.Event` → the raw InteractionCreateEvent for maximum flexibility


Security & configuration tips
-----------------------------
- Never hardcode the bot token. Use environment variables or secure configuration providers.
- Restrict intents to the minimum needed.
- Use a separate development guild for rapid command iteration (dev mode).


Full minimal example (Program.cs)
---------------------------------
```csharp
using SimpleDiscordNet;
using SimpleDiscordNet.Commands;
using SimpleDiscordNet.Events;
using SimpleDiscordNet.Primitives;

public sealed class AppCommands
{
    [SlashCommand("hello", "Say hello")]
    public async Task HelloAsync(InteractionContext ctx)
        => await ctx.RespondAsync("Hello!");
}

var bot = DiscordBot.NewBuilder()
    .WithToken(Environment.GetEnvironmentVariable("DISCORD_TOKEN")!)
    .WithIntents(DiscordIntents.Guilds)
    .WithDevelopmentMode(true)
    .WithDevelopmentGuild("YOUR_DEV_GUILD_ID")
    .Build();

DiscordEvents.Log += (_, m) => Console.WriteLine($"[{m.Level}] {m.Message}");
await bot.StartAsync();
await Task.Delay(Timeout.Infinite);

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

await bot.StartAsync(cts.Token);

// Run until Ctrl+C
try { await Task.Delay(Timeout.Infinite, cts.Token); }
catch (OperationCanceledException) { }

await bot.StopAsync();
```


FAQ / Troubleshooting
---------------------
- My commands don’t show up
  - In dev, ensure DevelopmentMode is enabled and a DevelopmentGuildId is set before Start.
  - Ensure your bot has the applications.commands scope installed in the target guild.
  - Check logs for sync errors (subscribe to DiscordEvents.Log and DiscordEvents.Error).
- Member lists are empty
  - You must enable the GuildMembers intent in the Developer Portal and request it in .WithIntents(...).
  - Consider preloading on start (WithPreloadOnStart) but be mindful of rate limits and large guilds.
- Do I need MessageContent intent?
  - No. This project does not process text prefix commands.


Logging options
---------------
You can control library log verbosity via DiscordBotOptions:

```csharp
var bot = DiscordBot.FromOptions(new DiscordBotOptions
{
    Token = "...",
    MinimumLogLevel = SimpleDiscordNet.Logging.LogLevel.Warning, // only Warning and above
    LogSink = m => Console.WriteLine($"[{m.Timestamp:HH:mm:ss} {m.Level}] {m.Message}")
});
```

Defaults preserve previous behavior (MinimumLogLevel = Trace). You can also subscribe to global events via DiscordEvents.Log.


Contributing
------------
Issues and PRs are welcome. Keep the code dependency‑free and aligned with the existing style: sealed classes by default, lean BCL usage, and careful attention to performance.


License
-------
SimpleDiscordNet is licensed under the Apache License, Version 2.0.

- See LICENSE for the full license text.
- See NOTICE for attribution requirements. In short: when redistributing source or binaries, keep the LICENSE file, and for binaries include the NOTICE text in your documentation and/or other materials (Apache‑2.0 §4(d)).

Optional header for source files in your applications:
```text
Copyright (c) 2025 Robert Erath
Licensed under the Apache License, Version 2.0. See LICENSE and NOTICE.
```


License details (plain‑language summary)
---------------------------------------
This section is a friendly overview to help new users. It is NOT legal advice and NOT a legally binding description of the license. The authoritative terms are in LICENSE (Apache‑2.0).

What you can do (freely)
- Use this project in personal, educational, open‑source, or commercial software.
- Modify the code, fork the repository, and publish your own versions or distributions.
- Sell your software or services that include or depend on this project.
- Keep your application closed‑source if you want — Apache‑2.0 does not require you to open your code.

Attribution and what to include
- If you redistribute SOURCE (e.g., a fork on GitHub, shipping source with your product):
  - Keep the LICENSE file.
  - Keep the NOTICE file.
- If you redistribute BINARIES (e.g., an app, library, Docker image):
  - Include the NOTICE text in your documentation and/or other materials that ship with your binary (for example: README, About dialog, docs site, installer EULA page). This satisfies Apache‑2.0 §4(d).
- If you MODIFY the source and redistribute:
  - Keep LICENSE and NOTICE.
  - You may add a short line in NOTICE describing your modifications and your copyright (optional but recommended by Apache‑2.0).

Simple examples
- Fork on GitHub: keep LICENSE and NOTICE at the repo root. Optionally mention “Includes SimpleDiscordNet (Apache‑2.0)” in your README.
- Closed‑source app: add a small “Third‑Party Notices” or “Open‑Source Credits” section in your docs/About page with the NOTICE text and a link to this repo. Keep LICENSE/NOTICE somewhere in your distribution (e.g., alongside your binaries or in an licenses/ folder).
- NuGet/library distribution: include the NOTICE text in your package description or project documentation, and ensure LICENSE is referenced in your package metadata.
- Docker image: include the NOTICE text in the image at a standard path (e.g., /licenses/NOTICE) and in the image README.

Using other versions
- You can use any version of this project (older or newer) under the same Apache‑2.0 terms as published for that version.
- You cannot change the license of SimpleDiscordNet itself, but your own application’s code can be under whatever license you choose, provided you comply with the Apache‑2.0 conditions for the parts you use from this project.

A quick non‑lawyer summary of Apache‑2.0
- Permissive: broad rights to use, modify, and distribute.
- Attribution: keep LICENSE in source distributions; include NOTICE text with binaries.
- Patents: contributors grant you a patent license for their contributions; if you sue someone over the project’s patents, your patent license under Apache‑2.0 terminates.
- No warranty: provided “as is,” without warranties or liability.

When in doubt
- Read the full LICENSE in this repo.
- If you have legal questions for your specific situation, consult your legal counsel.
