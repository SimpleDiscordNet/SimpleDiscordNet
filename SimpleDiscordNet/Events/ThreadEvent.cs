using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Payload for thread-related events.
/// For thread creation and deletion, only After/Thread is populated.
/// For thread updates, Before may contain the previous state if available in cache.
/// </summary>
public sealed record ThreadEvent
{
    /// <summary>The thread (channel) entity (for create/delete) or the current state (for updates).</summary>
    public required DiscordChannel Thread { get; init; }

    /// <summary>The guild this thread belongs to.</summary>
    public required DiscordGuild Guild { get; init; }

    /// <summary>
    /// The previous state of the thread before the update.
    /// Only populated for ThreadUpdated events when the thread was previously cached.
    /// Null for ThreadCreated and ThreadDeleted events.
    /// </summary>
    public DiscordChannel? BeforeUpdate { get; init; }

    /// <summary>Returns true if this is an update event with before state available.</summary>
    public bool HasBeforeUpdate => BeforeUpdate is not null;
}
