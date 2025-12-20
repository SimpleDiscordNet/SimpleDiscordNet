namespace SimpleDiscordNet.Entities;

public sealed class DiscordRole
{
    public required ulong Id { get; init; }
    public required string Name { get; init; }

    /// <summary>The guild this role belongs to</summary>
    public required DiscordGuild Guild { get; init; }

    public int Color { get; internal set; }
    public int Position { get; internal set; }
    public ulong Permissions { get; internal set; }

    /// <summary>Checks if this role has a specific permission</summary>
    public bool HasPermission(PermissionFlags permission) => (Permissions & (ulong)permission) != 0;

    /// <summary>Returns true if this role has Administrator permission (grants all permissions)</summary>
    public bool IsAdministrator => HasPermission(PermissionFlags.Administrator);
}
