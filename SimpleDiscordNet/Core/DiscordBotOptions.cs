using System.Text.Json;
using SimpleDiscordNet.Logging;

namespace SimpleDiscordNet;

/// <summary>
/// Configuration options for constructing <see cref="DiscordBot"/> via DI or factories.
/// </summary>
public sealed record DiscordBotOptions
{
    /// <summary>Bot token. Required.</summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>Gateway intents. Defaults to Guilds | GuildMessages | DirectMessages | MessageContent.</summary>
    public DiscordIntents Intents { get; init; } = DiscordIntents.Guilds | DiscordIntents.GuildMessages | DiscordIntents.DirectMessages | DiscordIntents.MessageContent;

    /// <summary>System.Text.Json options. Defaults to JsonSerializerDefaults.Web with case-insensitive props.</summary>
    public JsonSerializerOptions JsonOptions { get; init; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Optional time provider for testing or customization.</summary>
    public TimeProvider? TimeProvider { get; init; }

    /// <summary>Preload caches on start using REST calls.</summary>
    public bool PreloadGuilds { get; init; } = true;
    public bool PreloadChannels { get; init; } = true;
    public bool PreloadMembers { get; init; } = false;

    /// <summary>
    /// When enabled, the bot will immediately synchronize all discovered slash commands
    /// to the specified DevelopmentGuildIds on Start/StartAsync. This uses per-guild
    /// commands (PUT /applications/{app}/guilds/{guild}/commands) which propagate
    /// instantly and are ideal for development.
    /// </summary>
    public bool DevelopmentMode { get; init; } = false;

    /// <summary>
    /// Guild ids that should receive immediate slash-command sync when DevelopmentMode is enabled.
    /// </summary>
    public List<string> DevelopmentGuildIds { get; init; } = [];

    /// <summary>
    /// Optional log sink to mirror internal logs to your application (e.g., to ILogger).
    /// This is in addition to the global DiscordEvents.Log event.
    /// </summary>
    public Action<LogMessage>? LogSink { get; init; }

    /// <summary>
    /// Minimum log level to emit from the library. Messages below this level are ignored.
    /// Defaults to <see cref="LogLevel.Trace"/> (emit everything) to preserve previous behavior.
    /// </summary>
    public LogLevel MinimumLogLevel { get; init; } = LogLevel.Trace;
}
