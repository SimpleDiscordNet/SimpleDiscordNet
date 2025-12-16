namespace SimpleDiscordNet.Entities;

public sealed record class Member
{
    public required User User { get; init; }
    public string? Nick { get; init; }
    public string[] Roles { get; init; } = Array.Empty<string>();
}
