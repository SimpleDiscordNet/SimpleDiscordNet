using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Payload for guild-related events.
/// </summary>
public sealed record GuildEvent
{
    public required Guild Guild { get; init; }
}
