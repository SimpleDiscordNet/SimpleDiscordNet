namespace SimpleDiscordNet.Entities;

public sealed record Channel
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required int Type { get; init; }
    public string? Parent_Id { get; init; }
    public string? Guild_Id { get; init; }
    public ChannelPermissionOverwrite[]? Permission_Overwrites { get; init; }

    /// <summary>Discord channel type constants</summary>
    public static class ChannelType
    {
        public const int GuildText = 0;
        public const int DM = 1;
        public const int GuildVoice = 2;
        public const int GroupDM = 3;
        public const int GuildCategory = 4;
        public const int GuildAnnouncement = 5;
        public const int AnnouncementThread = 10;
        public const int PublicThread = 11;
        public const int PrivateThread = 12;
        public const int GuildStageVoice = 13;
        public const int GuildDirectory = 14;
        public const int GuildForum = 15;
        public const int GuildMedia = 16;
    }

    /// <summary>Returns true if this channel is a category (Type = 4)</summary>
    public bool IsCategory => Type == ChannelType.GuildCategory;

    /// <summary>Returns true if this channel is a text channel (Type = 0)</summary>
    public bool IsTextChannel => Type == ChannelType.GuildText;

    /// <summary>Returns true if this channel is a voice channel (Type = 2)</summary>
    public bool IsVoiceChannel => Type == ChannelType.GuildVoice;

    /// <summary>Returns true if this channel is a thread (Types 10, 11, 12)</summary>
    public bool IsThread => Type is ChannelType.AnnouncementThread or ChannelType.PublicThread or ChannelType.PrivateThread;

    /// <summary>Returns true if this channel is in a category (has Parent_Id)</summary>
    public bool HasParent => !string.IsNullOrEmpty(Parent_Id);

    /// <summary>Gets the permission overwrite for a specific role or member ID, or null if not found</summary>
    public ChannelPermissionOverwrite? GetOverwrite(string id)
        => Permission_Overwrites?.FirstOrDefault(o => o.Id == id);

    /// <summary>Gets all role permission overwrites</summary>
    public IEnumerable<ChannelPermissionOverwrite> GetRoleOverwrites()
        => Permission_Overwrites?.Where(o => o.IsRole) ?? [];

    /// <summary>Gets all member permission overwrites</summary>
    public IEnumerable<ChannelPermissionOverwrite> GetMemberOverwrites()
        => Permission_Overwrites?.Where(o => o.IsMember) ?? [];
}

public sealed record ChannelPermissionOverwrite
{
    public required string Id { get; init; }
    /// <summary>0 = role, 1 = member</summary>
    public required int Type { get; init; }
    /// <summary>Bitset as string per Discord API</summary>
    public required string Allow { get; init; }
    /// <summary>Bitset as string per Discord API</summary>
    public required string Deny { get; init; }

    /// <summary>Returns true if this overwriting is for a role (Type = 0)</summary>
    public bool IsRole => Type == 0;
    /// <summary>Returns true if this overwriting is for a member (Type = 1)</summary>
    public bool IsMember => Type == 1;

    /// <summary>Parses the Allow permissions as a ulong bitset</summary>
    public ulong GetAllowBits() => ulong.TryParse(Allow, out ulong val) ? val : 0UL;
    /// <summary>Parses the Deny permissions as a ulong bitset</summary>
    public ulong GetDenyBits() => ulong.TryParse(Deny, out ulong val) ? val : 0UL;

    /// <summary>Checks if a specific permission is explicitly allowed</summary>
    public bool HasAllow(PermissionFlags permission) => (GetAllowBits() & (ulong)permission) != 0;
    /// <summary>Checks if a specific permission is explicitly denied</summary>
    public bool HasDeny(PermissionFlags permission) => (GetDenyBits() & (ulong)permission) != 0;
}
