namespace SimpleDiscordNet.Models;

// Internal payload model used when sending embeds
internal sealed class Embed
{
    public string? title { get; set; }
    public string? description { get; set; }
    public int? color { get; set; }
}
