using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Payload for channel-related events, including the channel and its guild.
/// For channel creation and deletion, only After/Channel is populated.
/// For channel updates, Before may contain the previous state if available in cache.
/// </summary>
public sealed record ChannelEvent
{
    /// <summary>The channel entity (for create/delete) or the current state (for updates).</summary>
    public required DiscordChannel Channel { get; init; }

    /// <summary>The guild this channel belongs to.</summary>
    public required DiscordGuild Guild { get; init; }

    /// <summary>
    /// The previous state of the channel before the update.
    /// Only populated for ChannelUpdated events when the channel was previously cached.
    /// Null for ChannelCreated and ChannelDeleted events.
    /// </summary>
    public DiscordChannel? BeforeUpdate { get; init; }

    /// <summary>Returns true if this is an update event with before state available.</summary>
    public bool HasBeforeUpdate => BeforeUpdate is not null;
}
