using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Internal gateway payload for GUILD_EMOJIS_UPDATE dispatch.
/// </summary>
internal sealed record GuildEmojisUpdateEvent
{
    public required string GuildId { get; init; }
    public required Emoji[] Emojis { get; init; }
}
