using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Internal gateway payload for GUILD_EMOJIS_UPDATE dispatch.
/// </summary>
internal sealed record GuildEmojisUpdateEvent
{
    public required ulong GuildId { get; init; }
    public required DiscordEmoji[] Emojis { get; init; }
}
