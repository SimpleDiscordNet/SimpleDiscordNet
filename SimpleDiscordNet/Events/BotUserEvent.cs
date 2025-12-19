using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Payload for bot user update events.
/// Includes before/after state when the bot's user profile is updated.
/// </summary>
public sealed record BotUserEvent
{
    /// <summary>The current state of the bot user.</summary>
    public required DiscordUser User { get; init; }

    /// <summary>
    /// The previous state of the bot user before the update, if available in cache.
    /// </summary>
    public DiscordUser? BeforeUpdate { get; init; }

    /// <summary>Returns true if we have the before state available.</summary>
    public bool HasBeforeUpdate => BeforeUpdate is not null;
}
