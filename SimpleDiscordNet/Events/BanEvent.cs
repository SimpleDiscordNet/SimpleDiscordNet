using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Payload for ban-related events.
/// </summary>
public sealed record BanEvent
{
    public required User User { get; init; }
    public required Guild Guild { get; init; }
    public Member? Member { get; init; }
}
