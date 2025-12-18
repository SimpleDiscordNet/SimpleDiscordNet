using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Models.Context;

/// <summary>
/// A user enriched with its guild context.
/// </summary>
public sealed record UserWithGuild(User User, Guild Guild, Member? Member)
{
    public string Id => User.Id;
    public string Username => User.Username;
    public string DisplayName => Member?.DisplayName ?? User.DisplayName;
    public string GuildId => Guild.Id;
    public string GuildName => Guild.Name;
}
