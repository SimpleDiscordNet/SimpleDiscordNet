namespace SimpleDiscordNet;

/// <summary>
/// Represents a Discord embed color.
/// </summary>
public readonly struct DiscordColor(int value)
{
    public readonly int Value = value;

    public static DiscordColor FromRgb(byte r, byte g, byte b) => new((r << 16) | (g << 8) | b);

    // Commonly named colors
    public static DiscordColor Default => new(0x000000);
    public static DiscordColor Blue => new(0x3498DB);
    public static DiscordColor Green => new(0x2ECC71);
    public static DiscordColor Red => new(0xE74C3C);
    public static DiscordColor Yellow => new(0xF1C40F);
    public static DiscordColor Orange => new(0xE67E22);
    public static DiscordColor Purple => new(0x9B59B6);
    public static DiscordColor Teal => new(0x1ABC9C);
    public static DiscordColor Grey => new(0x95A5A6);
}
