using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Models.Context;

/// <summary>
/// A member enriched with its guild context.
/// </summary>
public sealed record MemberWithGuild(Member Member, Guild Guild, User User)
{
    public string UserId => User.Id;
    public string Username => User.Username;
    public string DisplayName => Member.DisplayName;
    public string? Nick => Member.Nick;
    public string[] Roles => Member.Roles;
    public string GuildId => Guild.Id;
    public string GuildName => Guild.Name;

    /// <summary>Check if this member has a specific role</summary>
    public bool HasRole(string roleId) => Member.HasRole(roleId);
}
