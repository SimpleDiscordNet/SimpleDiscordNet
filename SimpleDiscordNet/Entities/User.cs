namespace SimpleDiscordNet.Entities;

public sealed record User
{
    public required string Id { get; init; }
    public required string Username { get; init; }

    /// <summary>User's 4-digit discriminator (deprecated by Discord, may be "0" for new usernames)</summary>
    public string? Discriminator { get; init; }

    /// <summary>User's display name (new username system)</summary>
    public string? Global_Name { get; init; }

    /// <summary>User's avatar hash</summary>
    public string? Avatar { get; init; }

    /// <summary>Whether the user is a bot</summary>
    public bool? Bot { get; init; }

    /// <summary>Whether the user is an Official Discord System user</summary>
    public bool? System { get; init; }

    /// <summary>User's banner hash</summary>
    public string? Banner { get; init; }

    /// <summary>User's banner color (as integer)</summary>
    public int? Accent_Color { get; init; }

    /// <summary>User's public flags</summary>
    public int? Public_Flags { get; init; }

    /// <summary>
    /// Gets the user's avatar URL. Returns null if no custom avatar.
    /// </summary>
    /// <param name="size">Image size (power of 2, between 16 and 4096)</param>
    /// <param name="format">Image format (png, jpg, webp, gif). Defaults to png, use gif for animated avatars.</param>
    public string? GetAvatarUrl(int size = 256, string format = "png")
    {
        if (string.IsNullOrEmpty(Avatar)) return null;
        return $"https://cdn.discordapp.com/avatars/{Id}/{Avatar}.{format}?size={size}";
    }

    /// <summary>
    /// Gets the default avatar URL (for users without custom avatars).
    /// </summary>
    public string GetDefaultAvatarUrl()
    {
        // New system: uses (user_id >> 22) % 6
        // Old system: uses discriminator % 5
        int index = string.IsNullOrEmpty(Discriminator) || Discriminator == "0"
            ? (int)((ulong.Parse(Id) >> 22) % 6)
            : int.Parse(Discriminator) % 5;
        return $"https://cdn.discordapp.com/embed/avatars/{index}.png";
    }

    /// <summary>
    /// Gets the display name to show for this user (global_name if available, otherwise username).
    /// </summary>
    public string DisplayName => Global_Name ?? Username;

    /// <summary>
    /// Gets the full username with discriminator if available (e.g., "Username#1234" or just "Username").
    /// </summary>
    public string FullUsername => string.IsNullOrEmpty(Discriminator) || Discriminator == "0"
        ? Username
        : $"{Username}#{Discriminator}";
}
