using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Models.Context;

namespace SimpleDiscordNet.Context;

/// <summary>
/// Ambient access to cached Discord data for the currently running bot instance.
/// Consumers can read Guilds/Channels/Members/Users anywhere in their application.
/// </summary>
public static class DiscordContext
{
    private static Func<IReadOnlyList<DiscordGuild>>? s_guildsProvider;
    private static Func<IReadOnlyList<ChannelWithGuild>>? s_channelsProvider;
    private static Func<IReadOnlyList<MemberWithGuild>>? s_membersProvider;
    private static Func<IReadOnlyList<UserWithGuild>>? s_usersProvider;
    private static Func<IReadOnlyList<RoleWithGuild>>? s_rolesProvider;

    private static readonly DiscordGuild[] s_emptyGuilds = [];
    private static readonly ChannelWithGuild[] s_emptyChannels = [];
    private static readonly MemberWithGuild[] s_emptyMembers = [];
    private static readonly UserWithGuild[] s_emptyUsers = [];
    private static readonly RoleWithGuild[] s_emptyRoles = [];

    /// <summary>All guilds known to the bot</summary>
    public static IReadOnlyList<DiscordGuild> Guilds => s_guildsProvider?.Invoke() ?? s_emptyGuilds;

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
    public static IReadOnlyList<ChannelWithGuild> GetChannelsInGuild(ulong guildId)
        => Channels.Where(c => c.GuildId == guildId).ToArray();

    /// <summary>Get all category channels in a specific guild</summary>
    public static IReadOnlyList<ChannelWithGuild> GetCategoriesInGuild(ulong guildId)
        => Channels.Where(c => c.GuildId == guildId && c.Channel.IsCategory).ToArray();

    /// <summary>Get all channels within a specific category</summary>
    public static IReadOnlyList<ChannelWithGuild> GetChannelsInCategory(ulong categoryId)
        => Channels.Where(c => c.Channel.Parent_Id == categoryId).ToArray();

    /// <summary>Get all members in a specific guild</summary>
    public static IReadOnlyList<MemberWithGuild> GetMembersInGuild(ulong guildId)
        => Members.Where(m => m.GuildId == guildId).ToArray();

    /// <summary>Get all roles in a specific guild</summary>
    public static IReadOnlyList<RoleWithGuild> GetRolesInGuild(ulong guildId)
        => Roles.Where(r => r.GuildId == guildId).ToArray();

    /// <summary>
    /// If the bot is in exactly one guild, returns that guild. Otherwise returns null.
    /// Useful for single-guild bots to avoid passing guild IDs everywhere.
    /// </summary>
    public static DiscordGuild? Guild => Guilds.Count == 1 ? Guilds[0] : null;

    /// <summary>Find a specific guild by ID (string - parses to ulong)</summary>
    public static DiscordGuild? GetGuild(string guildId)
        => ulong.TryParse(guildId, out ulong id) ? Guilds.FirstOrDefault(g => g.Id == id) : null;

    /// <summary>Find a specific guild by ID (ulong)</summary>
    public static DiscordGuild? GetGuild(ulong guildId)
        => Guilds.FirstOrDefault(g => g.Id == guildId);

    /// <summary>Find a specific guild by ID (ReadOnlySpan&lt;char&gt; - parses to ulong)</summary>
    public static DiscordGuild? GetGuild(ReadOnlySpan<char> guildId)
    {
        if (ulong.TryParse(guildId, out ulong id))
        {
            return Guilds.FirstOrDefault(g => g.Id == id);
        }
        return null;
    }

    /// <summary>Find a specific channel by ID (ulong)</summary>
    public static ChannelWithGuild? GetChannel(ulong channelId)
        => Channels.FirstOrDefault(c => c.Channel.Id == channelId);

    /// <summary>Find a specific member by user ID and guild ID</summary>
    public static MemberWithGuild? GetMember(ulong guildId, ulong userId)
        => Members.FirstOrDefault(m => m.GuildId == guildId && m.Member.User.Id == userId);

    /// <summary>Find a specific role by ID</summary>
    public static RoleWithGuild? GetRole(ulong roleId)
        => Roles.FirstOrDefault(r => r.Role.Id == roleId);

    internal static void SetProvider(
        Func<IReadOnlyList<DiscordGuild>> guilds,
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
