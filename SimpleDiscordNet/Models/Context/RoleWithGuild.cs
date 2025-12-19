using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Models.Context;

/// <summary>
/// A role enriched with its guild context.
/// </summary>
public sealed record RoleWithGuild(DiscordRole Role, DiscordGuild Guild)
{
    public ulong Id => Role.Id;
    public string Name => Role.Name;
    public int Color => Role.Color;
    public int Position => Role.Position;
    public ulong Permissions => Role.Permissions;
    public ulong GuildId => Guild.Id;
    public string GuildName => Guild.Name;

    /// <summary>Checks if this role has a specific permission</summary>
    public bool HasPermission(PermissionFlags permission) => Role.HasPermission(permission);

    /// <summary>Returns true if this role has Administrator permission</summary>
    public bool IsAdministrator => Role.IsAdministrator;
}
