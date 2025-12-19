namespace SimpleDiscordNet.Entities;

public sealed record Member
{
    public required User User { get; init; }
    public string? Nick { get; init; }
    public string[] Roles { get; init; } = [];

    /// <summary>Guild-specific avatar hash</summary>
    public string? Avatar { get; init; }

    /// <summary>When the user joined the guild (ISO8601 timestamp)</summary>
    public string? Joined_At { get; init; }

    /// <summary>When the user started boosting the guild (ISO8601 timestamp)</summary>
    public string? Premium_Since { get; init; }

    /// <summary>Whether the user is deafened in voice channels</summary>
    public bool? Deaf { get; init; }

    /// <summary>Whether the user is muted in voice channels</summary>
    public bool? Mute { get; init; }

    /// <summary>Member flags (bit set)</summary>
    public int? Flags { get; init; }

    /// <summary>Whether the user has not yet passed the guild's Membership Screening requirements</summary>
    public bool? Pending { get; init; }

    /// <summary>Total permissions of the member in the channel (for interaction contexts)</summary>
    public string? Permissions { get; init; }

    /// <summary>When the user's timeout will expire (ISO8601 timestamp). Null if not timed out.</summary>
    public string? Communication_Disabled_Until { get; init; }

    /// <summary>
    /// Gets the member's display name (nickname if set, otherwise user's display name).
    /// </summary>
    public string DisplayName => Nick ?? User.DisplayName;

    /// <summary>
    /// Gets the member's guild-specific avatar URL. Returns null if no guild avatar.
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="size">Image size (power of 2, between 16 and 4096)</param>
    /// <param name="format">Image format (png, jpg, webp, gif)</param>
    public string? GetGuildAvatarUrl(string guildId, int size = 256, string format = "png")
    {
        return string.IsNullOrEmpty(Avatar) ? null : $"https://cdn.discordapp.com/guilds/{guildId}/users/{User.Id}/avatars/{Avatar}.{format}?size={size}";
    }

    /// <summary>
    /// Gets the effective avatar URL (guild avatar if available, otherwise user avatar).
    /// </summary>
    public string? GetEffectiveAvatarUrl(string guildId, int size = 256, string format = "png")
        => GetGuildAvatarUrl(guildId, size, format) ?? User.GetAvatarUrl(size, format) ?? User.GetDefaultAvatarUrl();

    /// <summary>
    /// Checks if this member has a specific role.
    /// </summary>
    public bool HasRole(string roleId) => Roles.Contains(roleId);

    /// <summary>
    /// Checks if the member is currently timed out.
    /// </summary>
    public bool IsTimedOut
    {
        get
        {
            if (string.IsNullOrEmpty(Communication_Disabled_Until)) return false;
            if (DateTimeOffset.TryParse(Communication_Disabled_Until, out DateTimeOffset until))
                return until > DateTimeOffset.UtcNow;
            return false;
        }
    }

    /// <summary>
    /// Parses the Permissions string as a ulong bitset (for interaction contexts).
    /// </summary>
    public ulong GetPermissionBits() => string.IsNullOrEmpty(Permissions) ? 0UL : (ulong.TryParse(Permissions, out ulong val) ? val : 0UL);
}
