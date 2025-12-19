using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Payload for bot user update events.
/// </summary>
public sealed record BotUserEvent
{
    public required User User { get; init; }
}
