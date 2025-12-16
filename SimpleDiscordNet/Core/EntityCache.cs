using System.Collections.Concurrent;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Models.Context;

namespace SimpleDiscordNet.Core;

/// <summary>
/// Thread-safe in-memory cache of Discord entities known to the bot.
/// </summary>
internal sealed class EntityCache
{
    private readonly ConcurrentDictionary<string, Guild> _guilds = new();
    private readonly ConcurrentDictionary<string, List<Channel>> _channelsByGuild = new();
    private readonly ConcurrentDictionary<string, List<Member>> _membersByGuild = new();

    public void ReplaceGuilds(IEnumerable<Guild> guilds)
    {
        _guilds.Clear();
        foreach (Guild g in guilds)
        {
            _guilds[g.Id] = g;
        }
    }

    public void SetChannels(string guildId, IEnumerable<Channel> channels)
    {
        _channelsByGuild[guildId] = channels.ToList();
    }

    public void SetMembers(string guildId, IEnumerable<Member> members)
    {
        _membersByGuild[guildId] = members.ToList();
    }

    public IReadOnlyList<Guild> SnapshotGuilds()
        => _guilds.Values.OrderBy(g => g.Id, StringComparer.Ordinal).ToArray();

    public IReadOnlyList<ChannelWithGuild> SnapshotChannels()
    {
        List<ChannelWithGuild> list = new(1024);
        foreach ((string gid, Guild guild) in _guilds)
        {
            if (!_channelsByGuild.TryGetValue(gid, out List<Channel>? chs)) continue;
            list.AddRange(chs.Select(c => new ChannelWithGuild(c, guild)));
        }
        return list;
    }

    public IReadOnlyList<MemberWithGuild> SnapshotMembers()
    {
        List<MemberWithGuild> list = new(2048);
        foreach ((string gid, Guild guild) in _guilds)
        {
            if (!_membersByGuild.TryGetValue(gid, out List<Member>? members)) continue;
            list.AddRange(members.Select(member => new MemberWithGuild(member, guild, member.User)));
        }
        return list;
    }

    public IReadOnlyList<UserWithGuild> SnapshotUsers()
    {
        List<UserWithGuild> list = new(2048);
        foreach ((string gid, Guild guild) in _guilds)
        {
            if (!_membersByGuild.TryGetValue(gid, out List<Member>? members)) continue;
            list.AddRange(members.Select(member => new UserWithGuild(member.User, guild, member)));
        }
        return list;
    }

    // --- Incremental mutation helpers for gateway events ---

    public void UpsertGuild(Guild guild)
    {
        _guilds[guild.Id] = guild;
    }

    public void RemoveGuild(string guildId)
    {
        _guilds.TryRemove(guildId, out _);
        _channelsByGuild.TryRemove(guildId, out _);
        _membersByGuild.TryRemove(guildId, out _);
    }

    public void UpsertChannel(string guildId, Channel channel)
    {
        List<Channel> list = _channelsByGuild.GetOrAdd(guildId, static _ => new List<Channel>());
        lock (list)
        {
            int idx = list.FindIndex(c => c.Id == channel.Id);
            if (idx >= 0) list[idx] = channel; else list.Add(channel);
        }
    }

    public void RemoveChannel(string guildId, string channelId)
    {
        if (_channelsByGuild.TryGetValue(guildId, out List<Channel>? list))
        {
            lock (list)
            {
                int idx = list.FindIndex(c => c.Id == channelId);
                if (idx >= 0) list.RemoveAt(idx);
            }
        }
    }

    public void UpsertMember(string guildId, Member member)
    {
        List<Member> list = _membersByGuild.GetOrAdd(guildId, static _ => new List<Member>());
        lock (list)
        {
            int idx = list.FindIndex(m => m.User.Id == member.User.Id);
            if (idx >= 0) list[idx] = member; else list.Add(member);
        }
    }

    public void RemoveMember(string guildId, string userId)
    {
        if (_membersByGuild.TryGetValue(guildId, out List<Member>? list))
        {
            lock (list)
            {
                int idx = list.FindIndex(m => m.User.Id == userId);
                if (idx >= 0) list.RemoveAt(idx);
            }
        }
    }

    public bool TryGetGuild(string guildId, out Guild guild)
        => _guilds.TryGetValue(guildId, out guild!);
}
