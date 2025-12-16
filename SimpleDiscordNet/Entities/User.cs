namespace SimpleDiscordNet.Entities;

public sealed record class User
{
    public required string Id { get; init; }
    public required string Username { get; init; }
}
