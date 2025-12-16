namespace SimpleDiscordNet.Entities;

public sealed record class Channel
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required int Type { get; init; }
    public string? Parent_Id { get; init; }
    public string? Guild_Id { get; init; }
}
