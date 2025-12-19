using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Internal gateway payload for GUILD_CREATE dispatch.
/// Includes the guild along with channels, members, and threads.
/// </summary>
internal sealed record GuildCreateEvent
{
    public required Guild Guild { get; init; }
    public Channel[]? Channels { get; init; }
    public Member[]? Members { get; init; }
    public Channel[]? Threads { get; init; }
}
