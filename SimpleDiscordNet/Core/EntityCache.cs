using System.Collections.Concurrent;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Models.Context;
using SimpleDiscordNet.Sharding;

namespace SimpleDiscordNet.Core;

/// <summary>
/// Thread-safe in-memory cache of Discord entities known to the bot.
/// </summary>
internal sealed class EntityCache
{
    private readonly ConcurrentDictionary<ulong, DiscordGuild> _guilds = new();
    private readonly ConcurrentDictionary<ulong, List<DiscordChannel>> _channelsByGuild = new();
    private readonly ConcurrentDictionary<ulong, List<DiscordMember>> _membersByGuild = new();

    public void ReplaceGuilds(IEnumerable<DiscordGuild> guilds)
    {
        _guilds.Clear();
        foreach (DiscordGuild g in guilds)
        {
            _guilds[g.Id] = g;
        }
    }

    public void SetChannels(ulong guildId, IEnumerable<DiscordChannel> channels)
    {
        _channelsByGuild[guildId] = channels.ToList();
    }

    public void SetMembers(ulong guildId, IEnumerable<DiscordMember> members)
    {
        _membersByGuild[guildId] = members.ToList();
    }

    public IReadOnlyList<DiscordGuild> SnapshotGuilds() => _guilds.Values.OrderBy(g => g.Id).ToArray();

    public IReadOnlyList<ChannelWithGuild> SnapshotChannels()
    {
        List<ChannelWithGuild> list = new(1024);
        foreach ((ulong gid, DiscordGuild guild) in _guilds)
        {
            if (!_channelsByGuild.TryGetValue(gid, out List<DiscordChannel>? chs)) continue;
            list.EnsureCapacity(list.Count + chs.Count);
            for (int index = chs.Count - 1; index >= 0; index--)
            {
                DiscordChannel c = chs[index];
                list.Add(new ChannelWithGuild(c, guild));
            }
        }
        return list;
    }

    public IReadOnlyList<MemberWithGuild> SnapshotMembers()
    {
        List<MemberWithGuild> list = new(2048);
        foreach ((ulong gid, DiscordGuild guild) in _guilds)
        {
            if (!_membersByGuild.TryGetValue(gid, out List<DiscordMember>? members)) continue;
            list.EnsureCapacity(list.Count + members.Count);
            for (int index = members.Count - 1; index >= 0; index--)
            {
                DiscordMember member = members[index];
                list.Add(new MemberWithGuild(member, guild, member.User));
            }
        }
        return list;
    }

    public IReadOnlyList<UserWithGuild> SnapshotUsers()
    {
        List<UserWithGuild> list = new(2048);
        foreach ((ulong gid, DiscordGuild guild) in _guilds)
        {
            if (!_membersByGuild.TryGetValue(gid, out List<DiscordMember>? members)) continue;
            list.EnsureCapacity(list.Count + members.Count);
            for (int index = members.Count - 1; index >= 0; index--)
            {
                DiscordMember member = members[index];
                list.Add(new UserWithGuild(member.User, guild, member));
            }
        }
        return list;
    }

    public IReadOnlyList<RoleWithGuild> SnapshotRoles()
    {
        List<RoleWithGuild> list = new(1024);
        foreach ((ulong gid, DiscordGuild guild) in _guilds)
        {
            if (guild.Roles == null) continue;
            list.EnsureCapacity(list.Count + guild.Roles.Length);
            foreach (var role in guild.Roles)
            {
                list.Add(new RoleWithGuild(role, guild));
            }
        }
        return list;
    }

    // --- Shard-aware snapshot methods ---

    /// <summary>
    /// Returns guilds that belong to a specific shard.
    /// Example: var guilds = cache.SnapshotGuildsForShard(0, 4);
    /// </summary>
    public IReadOnlyList<DiscordGuild> SnapshotGuildsForShard(int shardId, int totalShards)
    {
        return _guilds.Values
            .Where(g => ShardCalculator.CalculateShardId(g.Id.ToString().AsSpan(), totalShards) == shardId)
            .OrderBy(g => g.Id)
            .ToArray();
    }

    /// <summary>
    /// Returns channels that belong to guilds in a specific shard.
    /// Example: var channels = cache.SnapshotChannelsForShard(0, 4);
    /// </summary>
    public IReadOnlyList<ChannelWithGuild> SnapshotChannelsForShard(int shardId, int totalShards)
    {
        List<ChannelWithGuild> list = new(1024);
        foreach ((ulong gid, DiscordGuild guild) in _guilds)
        {
            if (ShardCalculator.CalculateShardId(gid.ToString().AsSpan(), totalShards) != shardId)
                continue;

            if (!_channelsByGuild.TryGetValue(gid, out List<DiscordChannel>? chs)) continue;
            list.EnsureCapacity(list.Count + chs.Count);
            foreach (DiscordChannel c in chs)
            {
                list.Add(new ChannelWithGuild(c, guild));
            }
        }
        return list;
    }

    /// <summary>
    /// Returns members that belong to guilds in a specific shard.
    /// Example: var members = cache.SnapshotMembersForShard(0, 4);
    /// </summary>
    public IReadOnlyList<MemberWithGuild> SnapshotMembersForShard(int shardId, int totalShards)
    {
        List<MemberWithGuild> list = new(2048);
        foreach ((ulong gid, DiscordGuild guild) in _guilds)
        {
            if (ShardCalculator.CalculateShardId(gid.ToString().AsSpan(), totalShards) != shardId)
                continue;

            if (!_membersByGuild.TryGetValue(gid, out List<DiscordMember>? members)) continue;
            list.EnsureCapacity(list.Count + members.Count);
            foreach (DiscordMember member in members)
            {
                list.Add(new MemberWithGuild(member, guild, member.User));
            }
        }
        return list;
    }

    /// <summary>
    /// Returns roles that belong to guilds in a specific shard.
    /// Example: var roles = cache.SnapshotRolesForShard(0, 4);
    /// </summary>
    public IReadOnlyList<RoleWithGuild> SnapshotRolesForShard(int shardId, int totalShards)
    {
        List<RoleWithGuild> list = new(1024);
        foreach ((ulong gid, DiscordGuild guild) in _guilds)
        {
            if (ShardCalculator.CalculateShardId(gid.ToString().AsSpan(), totalShards) != shardId)
                continue;

            if (guild.Roles == null) continue;
            list.EnsureCapacity(list.Count + guild.Roles.Length);
            foreach (DiscordRole role in guild.Roles)
            {
                list.Add(new RoleWithGuild(role, guild));
            }
        }
        return list;
    }

    // --- Incremental mutation helpers for gateway events ---

    public void UpsertGuild(DiscordGuild guild)
    {
        _guilds[guild.Id] = guild;
    }

    public void RemoveGuild(ulong guildId)
    {
        _guilds.TryRemove(guildId, out _);
        _channelsByGuild.TryRemove(guildId, out _);
        _membersByGuild.TryRemove(guildId, out _);
    }

    public void UpsertChannel(ulong guildId, DiscordChannel channel)
    {
        List<DiscordChannel> list = _channelsByGuild.GetOrAdd(guildId, static _ => new List<DiscordChannel>());
        lock (list)
        {
            int idx = list.FindIndex(c => c.Id == channel.Id);
            if (idx >= 0) list[idx] = channel; else list.Add(channel);
        }
    }

    public void RemoveChannel(ulong guildId, ulong channelId)
    {
        if (_channelsByGuild.TryGetValue(guildId, out List<DiscordChannel>? list))
        {
            lock (list)
            {
                int idx = list.FindIndex(c => c.Id == channelId);
                if (idx >= 0) list.RemoveAt(idx);
            }
        }
    }

    public void UpsertMember(ulong guildId, DiscordMember member)
    {
        List<DiscordMember> list = _membersByGuild.GetOrAdd(guildId, static _ => []);
        lock (list)
        {
            int idx = list.FindIndex(m => m.User.Id == member.User.Id);
            if (idx >= 0) list[idx] = member; else list.Add(member);
        }
    }

    public void RemoveMember(ulong guildId, ulong userId)
    {
        if (!_membersByGuild.TryGetValue(guildId, out List<DiscordMember>? list)) return;
        lock (list)
        {
            int idx = list.FindIndex(m => m.User.Id == userId);
            if (idx >= 0) list.RemoveAt(idx);
        }
    }

    public void UpsertRole(ulong guildId, DiscordRole role)
    {
        if (!_guilds.TryGetValue(guildId, out DiscordGuild? guild)) return;
        DiscordRole[] currentRoles = guild.Roles ?? [];
        int idx = Array.FindIndex(currentRoles, r => r.Id == role.Id);

        DiscordRole[] newRoles;
        if (idx >= 0)
        {
            // Update existing role
            newRoles = new DiscordRole[currentRoles.Length];
            currentRoles.AsSpan().CopyTo(newRoles.AsSpan());
            newRoles[idx] = role;
        }
        else
        {
            // Add new role
            newRoles = new DiscordRole[currentRoles.Length + 1];
            currentRoles.AsSpan().CopyTo(newRoles.AsSpan());
            newRoles[^1] = role;
        }

        // Update guild with new roles array
        _guilds[guildId] = guild with { Roles = newRoles };
    }

    public void RemoveRole(ulong guildId, ulong roleId)
    {
        if (!_guilds.TryGetValue(guildId, out DiscordGuild? guild) || guild.Roles is null) return;
        DiscordRole[] currentRoles = guild.Roles;
        int idx = Array.FindIndex(currentRoles, r => r.Id == roleId);

        if (idx < 0) return;
        DiscordRole[] newRoles = new DiscordRole[currentRoles.Length - 1];
        if (idx > 0)
            currentRoles.AsSpan(0, idx).CopyTo(newRoles.AsSpan());
        if (idx < currentRoles.Length - 1)
            currentRoles.AsSpan(idx + 1).CopyTo(newRoles.AsSpan(idx));

        _guilds[guildId] = guild with { Roles = newRoles };
    }

    public void SetEmojis(ulong guildId, DiscordEmoji[] emojis)
    {
        if (_guilds.TryGetValue(guildId, out DiscordGuild? guild))
        {
            _guilds[guildId] = guild with { Emojis = emojis };
        }
    }

    public bool TryGetGuild(ulong guildId, out DiscordGuild guild) => _guilds.TryGetValue(guildId, out guild!);
    public bool TryGetUser(ulong userId, out DiscordUser user)
    {
        foreach (List<DiscordMember> members in _membersByGuild.Values)
        {
            DiscordMember? member = members.FirstOrDefault(m => m.User.Id == userId);
            if (member == null) continue;
            user = member.User;
            return true;
        }
        user = null!;
        return false;
    }
}
