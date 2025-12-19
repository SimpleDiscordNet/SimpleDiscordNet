using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Models.Context;

/// <summary>
/// A member enriched with its guild context.
/// </summary>
public sealed record MemberWithGuild(DiscordMember Member, DiscordGuild Guild, DiscordUser User)
{
    public ulong UserId => User.Id;
    public string Username => User.Username;
    public string DisplayName => Member.DisplayName;
    public string? Nick => Member.Nick;
    public ulong[] Roles => Member.Roles;
    public ulong GuildId => Guild.Id;
    public string GuildName => Guild.Name;

    /// <summary>Check if this member has a specific role</summary>
    public bool HasRole(ulong roleId) => Member.HasRole(roleId);
}
