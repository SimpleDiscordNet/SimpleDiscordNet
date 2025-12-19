using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Models.Context;

/// <summary>
/// A user enriched with its guild context.
/// </summary>
public sealed record UserWithGuild(DiscordUser User, DiscordGuild Guild, DiscordMember? Member)
{
    public ulong Id => User.Id;
    public string Username => User.Username;
    public string DisplayName => Member?.DisplayName ?? User.DisplayName;
    public ulong GuildId => Guild.Id;
    public string GuildName => Guild.Name;
}
