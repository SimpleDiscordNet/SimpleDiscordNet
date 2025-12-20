using SimpleDiscordNet.Primitives;

namespace SimpleDiscordNet.Entities;

/// <summary>Discord channel types</summary>
public enum ChannelType
{
    GuildText = 0,
    DM = 1,
    GuildVoice = 2,
    GroupDM = 3,
    GuildCategory = 4,
    GuildAnnouncement = 5,
    AnnouncementThread = 10,
    PublicThread = 11,
    PrivateThread = 12,
    GuildStageVoice = 13,
    GuildDirectory = 14,
    GuildForum = 15,
    GuildMedia = 16
}

public sealed class DiscordChannel
{
    public required ulong Id { get; init; }
    public required string Name { get; init; }
    public required int Type { get; init; }

    /// <summary>The guild this channel belongs to. Null for DM channels.</summary>
    public DiscordGuild? Guild { get; internal set; }

    public ulong? Parent_Id { get; internal set; }
    public ulong? Guild_Id { get; internal set; }
    public ChannelPermissionOverwrite[]? Permission_Overwrites { get; internal set; }

    /// <summary>Gets the channel type as an enum</summary>
    public ChannelType ChannelType => (ChannelType)Type;

    /// <summary>Returns true if this channel is a category (Type = 4)</summary>
    public bool IsCategory => Type == (int)Entities.ChannelType.GuildCategory;

    /// <summary>Returns true if this channel is a text channel (Type = 0)</summary>
    public bool IsTextChannel => Type == (int)Entities.ChannelType.GuildText;

    /// <summary>Returns true if this channel is a voice channel (Type = 2)</summary>
    public bool IsVoiceChannel => Type == (int)Entities.ChannelType.GuildVoice;

    /// <summary>Returns true if this channel is a thread (Types 10, 11, 12)</summary>
    public bool IsThread => Type is (int)Entities.ChannelType.AnnouncementThread or (int)Entities.ChannelType.PublicThread or (int)Entities.ChannelType.PrivateThread;

    /// <summary>Returns true if this channel is in a category (has Parent_Id)</summary>
    public bool HasParent => Parent_Id.HasValue;

    /// <summary>Gets the permission overwrite for a specific role or member ID, or null if not found</summary>
    public ChannelPermissionOverwrite? GetOverwrite(ulong id)
        => Permission_Overwrites?.FirstOrDefault(o => o.Id == id);

    /// <summary>Gets all role permission overwrites</summary>
    public IEnumerable<ChannelPermissionOverwrite> GetRoleOverwrites()
        => Permission_Overwrites?.Where(o => o.IsRole) ?? [];

    /// <summary>Gets all member permission overwrites</summary>
    public IEnumerable<ChannelPermissionOverwrite> GetMemberOverwrites()
        => Permission_Overwrites?.Where(o => o.IsMember) ?? [];

    /// <summary>
    /// Sends a message to this channel.
    /// Example: await channel.SendMessageAsync("Hello!");
    /// </summary>
    public Task<DiscordMessage?> SendMessageAsync(string content, EmbedBuilder? embed = null, CancellationToken ct = default)
        => Context.DiscordContext.Operations.SendMessageAsync(Id.ToString(), content, embed, ct);

    /// <summary>
    /// Sends a message to this channel using a MessageBuilder.
    /// Example: await channel.SendMessageAsync(new MessageBuilder().WithContent("Hello").WithEmbed(embed));
    /// </summary>
    public Task<DiscordMessage?> SendMessageAsync(MessageBuilder builder, CancellationToken ct = default)
        => Context.DiscordContext.Operations.SendMessageAsync(Id.ToString(), builder, ct);

    /// <summary>
    /// Deletes this channel.
    /// Example: await channel.DeleteAsync();
    /// </summary>
    public Task DeleteAsync(CancellationToken ct = default)
        => Context.DiscordContext.Operations.DeleteChannelAsync(Id, ct);

    /// <summary>
    /// Moves this channel to a different category or changes its position.
    /// Example: await channel.MoveAsync(parentId: categoryId);
    /// Example: await channel.MoveAsync(position: 5);
    /// </summary>
    public Task<DiscordChannel?> MoveAsync(string? parentId = null, int? position = null, CancellationToken ct = default)
        => Context.DiscordContext.Operations.ModifyChannelAsync(Id, name: null, parentId: parentId, position: position, ct: ct);

    /// <summary>
    /// Sets the channel topic (text channels only).
    /// Example: await channel.SetTopicAsync("Welcome to the channel!");
    /// </summary>
    public Task<DiscordChannel?> SetTopicAsync(string topic, CancellationToken ct = default)
        => Context.DiscordContext.Operations.ModifyChannelAsync(Id, topic: topic, ct: ct);

    /// <summary>
    /// Sets the channel name.
    /// Example: await channel.SetNameAsync("new-channel-name");
    /// </summary>
    public Task<DiscordChannel?> SetNameAsync(string name, CancellationToken ct = default)
        => Context.DiscordContext.Operations.ModifyChannelAsync(Id, name: name, ct: ct);

    /// <summary>
    /// Sets the NSFW flag for this channel.
    /// Example: await channel.SetNsfwAsync(true);
    /// </summary>
    public Task<DiscordChannel?> SetNsfwAsync(bool nsfw, CancellationToken ct = default)
        => Context.DiscordContext.Operations.ModifyChannelAsync(Id, nsfw: nsfw, ct: ct);

    /// <summary>
    /// Sets the bitrate for voice channels (in bits per second, min 8000).
    /// Example: await voiceChannel.SetBitrateAsync(96000);
    /// </summary>
    public Task<DiscordChannel?> SetBitrateAsync(int bitrate, CancellationToken ct = default)
        => Context.DiscordContext.Operations.ModifyChannelAsync(Id, bitrate: bitrate, ct: ct);

    /// <summary>
    /// Sets the user limit for voice channels (0 = unlimited).
    /// Example: await voiceChannel.SetUserLimitAsync(10);
    /// </summary>
    public Task<DiscordChannel?> SetUserLimitAsync(int userLimit, CancellationToken ct = default)
        => Context.DiscordContext.Operations.ModifyChannelAsync(Id, userLimit: userLimit, ct: ct);

    /// <summary>
    /// Sets the slowmode rate limit (seconds between messages, 0-21600).
    /// Example: await channel.SetSlowmodeAsync(5); // 5 seconds between messages
    /// </summary>
    public Task<DiscordChannel?> SetSlowmodeAsync(int rateLimitPerUser, CancellationToken ct = default)
        => Context.DiscordContext.Operations.ModifyChannelAsync(Id, rateLimitPerUser: rateLimitPerUser, ct: ct);
}

public sealed record ChannelPermissionOverwrite
{
    public required ulong Id { get; init; }
    /// <summary>0 = role, 1 = member</summary>
    public required int Type { get; init; }
    /// <summary>Permission bitset for allowed permissions</summary>
    public required ulong Allow { get; init; }
    /// <summary>Permission bitset for denied permissions</summary>
    public required ulong Deny { get; init; }

    /// <summary>Returns true if this overwriting is for a role (Type = 0)</summary>
    public bool IsRole => Type == 0;
    /// <summary>Returns true if this overwriting is for a member (Type = 1)</summary>
    public bool IsMember => Type == 1;

    /// <summary>Checks if a specific permission is explicitly allowed</summary>
    public bool HasAllow(PermissionFlags permission) => (Allow & (ulong)permission) != 0;
    /// <summary>Checks if a specific permission is explicitly denied</summary>
    public bool HasDeny(PermissionFlags permission) => (Deny & (ulong)permission) != 0;
}
