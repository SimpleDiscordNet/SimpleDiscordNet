namespace SimpleDiscordNet.Entities;

public sealed record Role
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public int Color { get; init; }
    public int Position { get; init; }
    // Permissions bitset as string per Discord HTTP API (to avoid ulong overflow in JSON)
    public string? Permissions { get; init; }

    /// <summary>Parses the Permissions string as a ulong bitset</summary>
    public ulong GetPermissionBits() => string.IsNullOrEmpty(Permissions) ? 0UL : (ulong.TryParse(Permissions, out ulong val) ? val : 0UL);

    /// <summary>Checks if this role has a specific permission</summary>
    public bool HasPermission(PermissionFlags permission) => (GetPermissionBits() & (ulong)permission) != 0;

    /// <summary>Returns true if this role has Administrator permission (grants all permissions)</summary>
    public bool IsAdministrator => HasPermission(PermissionFlags.Administrator);
}
