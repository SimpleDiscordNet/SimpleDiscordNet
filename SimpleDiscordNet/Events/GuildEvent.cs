using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Payload for guild-related events.
/// For guild creation and deletion, only After/Guild is populated.
/// For guild updates, Before may contain the previous state if available in cache.
/// </summary>
public sealed record GuildEvent
{
    /// <summary>The guild entity (for create/delete) or the current state (for updates).</summary>
    public required DiscordGuild Guild { get; init; }

    /// <summary>
    /// The previous state of the guild before the update.
    /// Only populated for GuildUpdated events when the guild was previously cached.
    /// Null for GuildAdded and GuildRemoved events.
    /// </summary>
    public DiscordGuild? BeforeUpdate { get; init; }

    /// <summary>Returns true if this is an update event with before state available.</summary>
    public bool HasBeforeUpdate => BeforeUpdate is not null;
}
