using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Internal gateway payload for GUILD_MEMBERS_CHUNK dispatch.
/// Contains a chunk of members from REQUEST_GUILD_MEMBERS.
/// </summary>
internal sealed record GuildMembersChunkEvent
{
    public required string GuildId { get; init; }
    public required Member[] Members { get; init; }
    public int ChunkIndex { get; init; }
    public int ChunkCount { get; init; }
}
