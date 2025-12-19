using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Payload for ban-related events.
/// </summary>
public sealed record BanEvent
{
    public required DiscordUser User { get; init; }
    public required DiscordGuild Guild { get; init; }
    public DiscordMember? Member { get; init; }
}
