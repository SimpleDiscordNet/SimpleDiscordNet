namespace SimpleDiscordNet;

/// <summary>
/// Represents a Discord embed color.
/// </summary>
public readonly struct DiscordColor(int value)
{
    public readonly int Value = value;

    public static DiscordColor FromRgb(byte r, byte g, byte b) => new((r << 16) | (g << 8) | b);

    /// <summary>
    /// Creates a color from RGB component values (0-255).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static DiscordColor FromRgb(int r, int g, int b)
    {
        if ((uint)r > 255u || (uint)g > 255u || (uint)b > 255u)
            throw new ArgumentOutOfRangeException("RGB components must be between 0 and 255.");
        return FromRgb((byte)r, (byte)g, (byte)b);
    }

    /// <summary>
    /// Creates a color from a hex string.
    /// Supports formats: "#RRGGBB", "0xRRGGBB", "RRGGBB", and shorthand "#RGB".
    /// Example: DiscordColor.FromHex("#FF5733")
    /// </summary>
    public static DiscordColor FromHex(string hex)
    {
        if (hex is null) throw new ArgumentNullException(nameof(hex));
        return FromHex(hex.AsSpan());
    }

    /// <summary>
    /// Creates a color from a hex string span (zero-allocation).
    /// Supports formats: "#RRGGBB", "0xRRGGBB", "RRGGBB", and shorthand "#RGB".
    /// Example: DiscordColor.FromHex("#FF5733".AsSpan())
    /// </summary>
    public static DiscordColor FromHex(ReadOnlySpan<char> hex)
    {
        hex = hex.Trim();
        if (hex.StartsWith("#")) hex = hex[1..];
        else if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || hex.StartsWith("0X", StringComparison.OrdinalIgnoreCase))
            hex = hex[2..];

        if (hex.Length == 3)
        {
            // Shorthand #RGB -> #RRGGBB
            Span<char> expanded = stackalloc char[2];
            expanded[0] = hex[0];
            expanded[1] = hex[0];
            int r = int.Parse(expanded, System.Globalization.NumberStyles.HexNumber);
            expanded[0] = hex[1];
            expanded[1] = hex[1];
            int g = int.Parse(expanded, System.Globalization.NumberStyles.HexNumber);
            expanded[0] = hex[2];
            expanded[1] = hex[2];
            int b = int.Parse(expanded, System.Globalization.NumberStyles.HexNumber);
            return FromRgb((byte)r, (byte)g, (byte)b);
        }

        if (hex.Length != 6)
            throw new FormatException("Hex color must have 3 or 6 hex digits.");

        int value = int.Parse(hex, System.Globalization.NumberStyles.HexNumber);
        return new DiscordColor(value);
    }

    /// <summary>
    /// Attempts to parse a hex color string into a DiscordColor.
    /// </summary>
    public static bool TryFromHex(string? hex, out DiscordColor color)
    {
        try { color = FromHex(hex ?? string.Empty); return true; }
        catch { color = default; return false; }
    }

    /// <summary>
    /// Implicit conversion from <see cref="DiscordColor"/> to its integer value (RRGGBB).
    /// </summary>
    public static implicit operator int(DiscordColor color) => color.Value;

    /// <summary>
    /// Implicit conversion from an integer value (RRGGBB) to <see cref="DiscordColor"/>.
    /// </summary>
    public static implicit operator DiscordColor(int value) => new(value);

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

    // Discord brand and standard palette
    public static DiscordColor Blurple => new(0x5865F2);
    public static DiscordColor OldBlurple => new(0x7289DA);
    public static DiscordColor DiscordGreen => new(0x57F287);
    public static DiscordColor DiscordYellow => new(0xFEE75C);
    public static DiscordColor Fuchsia => new(0xEB459E);
    public static DiscordColor DiscordRed => new(0xED4245);
    public static DiscordColor NotQuiteBlack => new(0x23272A);
    public static DiscordColor DarkButNotBlack => new(0x2C2F33);
    public static DiscordColor White => new(0xFFFFFF);
    public static DiscordColor Black => new(0x000000);

    // Brand-prefixed alias for clarity where overlapping names exist
    public static DiscordColor DiscordBlue => Blurple;

    // Microsoft default color set (Fluent / Office palette) — flattened and prefixed
    public static DiscordColor MicrosoftBlue => new(0x0078D4);          // Communication Blue
    public static DiscordColor MicrosoftNavy => new(0x00188F);
    public static DiscordColor MicrosoftTeal => new(0x008272);
    public static DiscordColor MicrosoftGreen => new(0x107C10);
    public static DiscordColor MicrosoftLightGreen => new(0x7FBA00);
    public static DiscordColor MicrosoftLime => new(0xBAD80A);
    public static DiscordColor MicrosoftYellow => new(0xFFB900);
    public static DiscordColor MicrosoftOrange => new(0xF7630C);
    public static DiscordColor MicrosoftRed => new(0xD13438);
    public static DiscordColor MicrosoftMagenta => new(0xB4009E);
    public static DiscordColor MicrosoftPink => new(0xE3008C);
    public static DiscordColor MicrosoftPurple => new(0x5C2D91);
    public static DiscordColor MicrosoftCyan => new(0x0099BC);
    public static DiscordColor MicrosoftSteel => new(0x7A7574);         // Neutral
    public static DiscordColor MicrosoftGold => new(0xFFB900);
    public static DiscordColor MicrosoftBlack => new(0x000000);
    public static DiscordColor MicrosoftWhite => new(0xFFFFFF);
    public static DiscordColor MicrosoftDarkGray => new(0x605E5C);      // neutralSecondary
    public static DiscordColor MicrosoftGray => new(0xA19F9D);          // neutralTertiary
    public static DiscordColor MicrosoftLightGray => new(0xE1DFDD);     // neutralQuaternaryAlt
}
