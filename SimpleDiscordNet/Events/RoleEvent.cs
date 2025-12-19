using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Payload for role-related events.
/// For role creation and deletion, only After/Role is populated.
/// For role updates, Before may contain the previous state if available in cache.
/// </summary>
public sealed record RoleEvent
{
    /// <summary>The role entity (for create/delete) or the current state (for updates).</summary>
    public required DiscordRole Role { get; init; }

    /// <summary>The guild this role belongs to.</summary>
    public required DiscordGuild Guild { get; init; }

    /// <summary>
    /// The previous state of the role before the update.
    /// Only populated for RoleUpdated events when the role was previously cached.
    /// Null for RoleCreated and RoleDeleted events.
    /// </summary>
    public DiscordRole? BeforeUpdate { get; init; }

    /// <summary>Returns true if this is an update event with before state available.</summary>
    public bool HasBeforeUpdate => BeforeUpdate is not null;
}
