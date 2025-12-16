using SimpleDiscordNet.Models;

namespace SimpleDiscordNet;

/// <summary>
/// Simple helper to build embeds.
/// </summary>
public sealed class EmbedBuilder
{
    public string? Title { get; private set; }
    public string? Description { get; private set; }
    public DiscordColor? Color { get; private set; }

    public EmbedBuilder WithTitle(string title) { Title = title; return this; }
    public EmbedBuilder WithDescription(string description) { Description = description; return this; }
    public EmbedBuilder WithColor(DiscordColor color) { Color = color; return this; }

    internal Embed ToModel() => new()
    {
        title = Title,
        description = Description,
        color = Color?.Value
    };
}
