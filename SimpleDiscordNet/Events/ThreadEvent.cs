using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Payload for thread-related events.
/// </summary>
public sealed record ThreadEvent
{
    public required Channel Thread { get; init; }
    public required Guild Guild { get; init; }
}
