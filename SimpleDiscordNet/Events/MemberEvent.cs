using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Payload for member-related events, including the member, user, and guild.
/// </summary>
public sealed record MemberEvent
{
    public required Member Member { get; init; }
    public required User User { get; init; }
    public required Guild Guild { get; init; }
}
