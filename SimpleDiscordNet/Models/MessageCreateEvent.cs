namespace SimpleDiscordNet.Models;

public sealed record MessageCreateEvent
{
    public required string Id { get; init; }
    public required string ChannelId { get; init; }
    public string? GuildId { get; init; }
    public required string Content { get; init; }
    public required Author Author { get; init; }
}

public sealed record Author
{
    public required string Id { get; init; }
    public required string Username { get; init; }
}
