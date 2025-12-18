using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Payload for message update/delete events.
/// </summary>
public sealed record MessageEvent
{
    public required string MessageId { get; init; }
    public required string ChannelId { get; init; }
    public string? GuildId { get; init; }
}

/// <summary>
/// Payload for message update events (includes partial message data).
/// </summary>
public sealed record MessageUpdateEvent
{
    public required string MessageId { get; init; }
    public required string ChannelId { get; init; }
    public string? GuildId { get; init; }
    public string? Content { get; init; }
    public string? EditedTimestamp { get; init; }
}

/// <summary>
/// Payload for reaction events.
/// </summary>
public sealed record ReactionEvent
{
    public required string MessageId { get; init; }
    public required string ChannelId { get; init; }
    public string? GuildId { get; init; }
    public required string UserId { get; init; }
    public required Emoji Emoji { get; init; }
}
