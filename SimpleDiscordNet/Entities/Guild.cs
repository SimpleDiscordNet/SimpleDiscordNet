namespace SimpleDiscordNet.Entities;

public sealed record class Guild
{
    public required string Id { get; init; }
    public required string Name { get; init; }
}
