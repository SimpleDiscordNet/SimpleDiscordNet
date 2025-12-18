namespace SimpleDiscordNet.Entities;

public sealed record Guild
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    /// <summary>Icon hash</summary>
    public string? Icon { get; init; }

    /// <summary>Splash hash</summary>
    public string? Splash { get; init; }

    /// <summary>Discovery splash hash</summary>
    public string? Discovery_Splash { get; init; }

    /// <summary>ID of owner</summary>
    public string? Owner_Id { get; init; }

    /// <summary>ID of AFK channel</summary>
    public string? Afk_Channel_Id { get; init; }

    /// <summary>AFK timeout in seconds</summary>
    public int? Afk_Timeout { get; init; }

    /// <summary>Verification level required for the guild</summary>
    public int? Verification_Level { get; init; }

    /// <summary>Default message notification level</summary>
    public int? Default_Message_Notifications { get; init; }

    /// <summary>Explicit content filter level</summary>
    public int? Explicit_Content_Filter { get; init; }

    /// <summary>Roles in the guild</summary>
    public Role[]? Roles { get; init; }

    /// <summary>Custom guild emojis</summary>
    public Emoji[]? Emojis { get; init; }

    /// <summary>Enabled guild features</summary>
    public string[]? Features { get; init; }

    /// <summary>Required MFA level for the guild</summary>
    public int? Mfa_Level { get; init; }

    /// <summary>System channel ID</summary>
    public string? System_Channel_Id { get; init; }

    /// <summary>System channel flags</summary>
    public int? System_Channel_Flags { get; init; }

    /// <summary>Rules channel ID</summary>
    public string? Rules_Channel_Id { get; init; }

    /// <summary>Maximum number of presences for the guild</summary>
    public int? Max_Presences { get; init; }

    /// <summary>Maximum number of members for the guild</summary>
    public int? Max_Members { get; init; }

    /// <summary>Vanity URL code</summary>
    public string? Vanity_Url_Code { get; init; }

    /// <summary>Description of the guild</summary>
    public string? Description { get; init; }

    /// <summary>Banner hash</summary>
    public string? Banner { get; init; }

    /// <summary>Premium tier (Server Boost level)</summary>
    public int? Premium_Tier { get; init; }

    /// <summary>Number of boosts this guild has</summary>
    public int? Premium_Subscription_Count { get; init; }

    /// <summary>Preferred locale of the guild</summary>
    public string? Preferred_Locale { get; init; }

    /// <summary>Public updates channel ID</summary>
    public string? Public_Updates_Channel_Id { get; init; }

    /// <summary>NSFW level</summary>
    public int? Nsfw_Level { get; init; }

    /// <summary>Whether the guild has the boost progress bar enabled</summary>
    public bool? Premium_Progress_Bar_Enabled { get; init; }

    /// <summary>
    /// Gets the guild's icon URL. Returns null if no icon.
    /// </summary>
    /// <param name="size">Image size (power of 2, between 16 and 4096)</param>
    /// <param name="format">Image format (png, jpg, webp, gif)</param>
    public string? GetIconUrl(int size = 256, string format = "png")
    {
        if (string.IsNullOrEmpty(Icon)) return null;
        return $"https://cdn.discordapp.com/icons/{Id}/{Icon}.{format}?size={size}";
    }

    /// <summary>
    /// Gets the guild's banner URL. Returns null if no banner.
    /// </summary>
    public string? GetBannerUrl(int size = 1024, string format = "png")
    {
        if (string.IsNullOrEmpty(Banner)) return null;
        return $"https://cdn.discordapp.com/banners/{Id}/{Banner}.{format}?size={size}";
    }

    /// <summary>
    /// Gets the guild's splash URL. Returns null if no splash.
    /// </summary>
    public string? GetSplashUrl(int size = 1024, string format = "png")
    {
        if (string.IsNullOrEmpty(Splash)) return null;
        return $"https://cdn.discordapp.com/splashes/{Id}/{Splash}.{format}?size={size}";
    }

    /// <summary>
    /// Checks if the guild has a specific feature enabled.
    /// Example features: "COMMUNITY", "VERIFIED", "PARTNERED", "ANIMATED_ICON", "BANNER", etc.
    /// </summary>
    public bool HasFeature(string feature) => Features?.Contains(feature, StringComparer.OrdinalIgnoreCase) ?? false;

    /// <summary>
    /// Returns true if this is a Community guild.
    /// </summary>
    public bool IsCommunity => HasFeature("COMMUNITY");

    /// <summary>
    /// Returns true if this guild is verified.
    /// </summary>
    public bool IsVerified => HasFeature("VERIFIED");

    /// <summary>
    /// Returns true if this guild is partnered.
    /// </summary>
    public bool IsPartnered => HasFeature("PARTNERED");
}
