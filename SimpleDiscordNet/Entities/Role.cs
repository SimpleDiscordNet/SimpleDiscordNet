namespace SimpleDiscordNet.Entities;

public sealed record class Role
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public int Color { get; init; }
    public int Position { get; init; }
}
