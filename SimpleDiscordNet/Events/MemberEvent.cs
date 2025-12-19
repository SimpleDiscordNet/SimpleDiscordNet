using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Payload for member-related events, including the member, user, and guild.
/// For member join and leave, only After/Member is populated.
/// For member updates, Before may contain the previous state if available in cache.
/// </summary>
public sealed record MemberEvent
{
    /// <summary>The member entity (for join/leave) or the current state (for updates).</summary>
    public required DiscordMember Member { get; init; }

    /// <summary>The user associated with this member.</summary>
    public required DiscordUser User { get; init; }

    /// <summary>The guild this member belongs to.</summary>
    public required DiscordGuild Guild { get; init; }

    /// <summary>
    /// The previous state of the member before the update.
    /// Only populated for MemberUpdated events when the member was previously cached.
    /// Null for MemberJoined and MemberLeft events.
    /// </summary>
    public DiscordMember? BeforeUpdate { get; init; }

    /// <summary>Returns true if this is an update event with before state available.</summary>
    public bool HasBeforeUpdate => BeforeUpdate is not null;
}
