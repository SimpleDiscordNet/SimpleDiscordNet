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

    private static readonly Guild[] s_emptyGuilds = Array.Empty<Guild>();
    private static readonly ChannelWithGuild[] s_emptyChannels = Array.Empty<ChannelWithGuild>();
    private static readonly MemberWithGuild[] s_emptyMembers = Array.Empty<MemberWithGuild>();
    private static readonly UserWithGuild[] s_emptyUsers = Array.Empty<UserWithGuild>();

    public static IReadOnlyList<Guild> Guilds => s_guildsProvider?.Invoke() ?? s_emptyGuilds;
    public static IReadOnlyList<ChannelWithGuild> Channels => s_channelsProvider?.Invoke() ?? s_emptyChannels;
    public static IReadOnlyList<MemberWithGuild> Members => s_membersProvider?.Invoke() ?? s_emptyMembers;
    public static IReadOnlyList<UserWithGuild> Users => s_usersProvider?.Invoke() ?? s_emptyUsers;

    internal static void SetProvider(
        Func<IReadOnlyList<Guild>> guilds,
        Func<IReadOnlyList<ChannelWithGuild>> channels,
        Func<IReadOnlyList<MemberWithGuild>> members,
        Func<IReadOnlyList<UserWithGuild>> users)
    {
        s_guildsProvider = guilds;
        s_channelsProvider = channels;
        s_membersProvider = members;
        s_usersProvider = users;
    }

    internal static void ClearProvider()
    {
        s_guildsProvider = null;
        s_channelsProvider = null;
        s_membersProvider = null;
        s_usersProvider = null;
    }
}
