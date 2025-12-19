using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Models.Context;

namespace SimpleDiscordNet.Context;

/// <summary>
/// Ambient access to cached Discord data for the currently running bot instance.
/// Consumers can read Guilds/Channels/Members/Users anywhere in their application.
/// </summary>
public static class DiscordContext
{
    private static Func<IReadOnlyList<Guild>>? s_guildsProvider;
    private static Func<IReadOnlyList<ChannelWithGuild>>? s_channelsProvider;
    private static Func<IReadOnlyList<MemberWithGuild>>? s_membersProvider;
    private static Func<IReadOnlyList<UserWithGuild>>? s_usersProvider;
    private static Func<IReadOnlyList<RoleWithGuild>>? s_rolesProvider;

    private static readonly Guild[] s_emptyGuilds = [];
    private static readonly ChannelWithGuild[] s_emptyChannels = [];
    private static readonly MemberWithGuild[] s_emptyMembers = [];
    private static readonly UserWithGuild[] s_emptyUsers = [];
    private static readonly RoleWithGuild[] s_emptyRoles = [];

    /// <summary>All guilds known to the bot</summary>
    public static IReadOnlyList<Guild> Guilds => s_guildsProvider?.Invoke() ?? s_emptyGuilds;

    /// <summary>All channels known to the bot (each enriched with guild context)</summary>
    public static IReadOnlyList<ChannelWithGuild> Channels => s_channelsProvider?.Invoke() ?? s_emptyChannels;

    /// <summary>All members known to the bot (each enriched with guild context)</summary>
    public static IReadOnlyList<MemberWithGuild> Members => s_membersProvider?.Invoke() ?? s_emptyMembers;

    /// <summary>All users known to the bot (each enriched with guild context)</summary>
    public static IReadOnlyList<UserWithGuild> Users => s_usersProvider?.Invoke() ?? s_emptyUsers;

    /// <summary>All roles known to the bot (each enriched with guild context)</summary>
    public static IReadOnlyList<RoleWithGuild> Roles => s_rolesProvider?.Invoke() ?? s_emptyRoles;

    /// <summary>All category channels</summary>
    public static IReadOnlyList<ChannelWithGuild> Categories => Channels.Where(c => c.Channel.IsCategory).ToArray();

    /// <summary>All text channels (excludes categories, threads, and voice)</summary>
    public static IReadOnlyList<ChannelWithGuild> TextChannels => Channels.Where(c => c.Channel.IsTextChannel).ToArray();

    /// <summary>All voice channels</summary>
    public static IReadOnlyList<ChannelWithGuild> VoiceChannels => Channels.Where(c => c.Channel.IsVoiceChannel).ToArray();

    /// <summary>All thread channels</summary>
    public static IReadOnlyList<ChannelWithGuild> Threads => Channels.Where(c => c.Channel.IsThread).ToArray();

    /// <summary>Get all channels in a specific guild</summary>
    public static IReadOnlyList<ChannelWithGuild> GetChannelsInGuild(string guildId)
        => Channels.Where(c => c.GuildId == guildId).ToArray();

    /// <summary>Get all category channels in a specific guild</summary>
    public static IReadOnlyList<ChannelWithGuild> GetCategoriesInGuild(string guildId)
        => Channels.Where(c => c.GuildId == guildId && c.Channel.IsCategory).ToArray();

    /// <summary>Get all channels within a specific category</summary>
    public static IReadOnlyList<ChannelWithGuild> GetChannelsInCategory(string categoryId)
        => Channels.Where(c => c.Channel.Parent_Id == categoryId).ToArray();

    /// <summary>Get all members in a specific guild</summary>
    public static IReadOnlyList<MemberWithGuild> GetMembersInGuild(string guildId)
        => Members.Where(m => m.GuildId == guildId).ToArray();

    /// <summary>Get all roles in a specific guild</summary>
    public static IReadOnlyList<RoleWithGuild> GetRolesInGuild(string guildId)
        => Roles.Where(r => r.GuildId == guildId).ToArray();

    /// <summary>Find a specific guild by ID</summary>
    public static Guild? GetGuild(string guildId)
        => Guilds.FirstOrDefault(g => g.Id == guildId);

    /// <summary>Find a specific channel by ID</summary>
    public static ChannelWithGuild? GetChannel(string channelId)
        => Channels.FirstOrDefault(c => c.Channel.Id == channelId);

    /// <summary>Find a specific member by user ID and guild ID</summary>
    public static MemberWithGuild? GetMember(string guildId, string userId)
        => Members.FirstOrDefault(m => m.GuildId == guildId && m.Member.User.Id == userId);

    /// <summary>Find a specific role by ID</summary>
    public static RoleWithGuild? GetRole(string roleId)
        => Roles.FirstOrDefault(r => r.Role.Id == roleId);

    internal static void SetProvider(
        Func<IReadOnlyList<Guild>> guilds,
        Func<IReadOnlyList<ChannelWithGuild>> channels,
        Func<IReadOnlyList<MemberWithGuild>> members,
        Func<IReadOnlyList<UserWithGuild>> users,
        Func<IReadOnlyList<RoleWithGuild>> roles)
    {
        s_guildsProvider = guilds;
        s_channelsProvider = channels;
        s_membersProvider = members;
        s_usersProvider = users;
        s_rolesProvider = roles;
    }

    internal static void ClearProvider()
    {
        s_guildsProvider = null;
        s_channelsProvider = null;
        s_membersProvider = null;
        s_usersProvider = null;
        s_rolesProvider = null;
    }
}
