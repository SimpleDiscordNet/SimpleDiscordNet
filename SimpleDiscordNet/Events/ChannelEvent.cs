using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Payload for channel-related events, including the channel and its guild.
/// </summary>
public sealed record ChannelEvent
{
    public required Channel Channel { get; init; }
    public required Guild Guild { get; init; }
}
