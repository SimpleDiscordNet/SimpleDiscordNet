using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Payload for role-related events.
/// </summary>
public sealed record RoleEvent
{
    public required Role Role { get; init; }
    public required Guild Guild { get; init; }
}
