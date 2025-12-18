using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Models.Context;

/// <summary>
/// A role enriched with its guild context.
/// </summary>
public sealed record RoleWithGuild(Role Role, Guild Guild)
{
    public string Id => Role.Id;
    public string Name => Role.Name;
    public int Color => Role.Color;
    public int Position => Role.Position;
    public string? Permissions => Role.Permissions;
    public string GuildId => Guild.Id;
    public string GuildName => Guild.Name;

    /// <summary>Checks if this role has a specific permission</summary>
    public bool HasPermission(PermissionFlags permission) => Role.HasPermission(permission);

    /// <summary>Returns true if this role has Administrator permission</summary>
    public bool IsAdministrator => Role.IsAdministrator;
}
