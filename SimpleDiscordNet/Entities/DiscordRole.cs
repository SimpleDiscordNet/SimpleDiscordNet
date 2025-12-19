namespace SimpleDiscordNet.Entities;

public sealed record DiscordRole
{
    public required ulong Id { get; init; }
    public required string Name { get; init; }
    public int Color { get; init; }
    public int Position { get; init; }
    public ulong Permissions { get; init; }

    /// <summary>Checks if this role has a specific permission</summary>
    public bool HasPermission(PermissionFlags permission) => (Permissions & (ulong)permission) != 0;

    /// <summary>Returns true if this role has Administrator permission (grants all permissions)</summary>
    public bool IsAdministrator => HasPermission(PermissionFlags.Administrator);
}
