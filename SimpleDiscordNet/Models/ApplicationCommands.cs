namespace SimpleDiscordNet.Models;

/// <summary>
/// Minimal definition model for Discord application commands used for sync (guild-scoped during development).
/// Matches the previous anonymous shape used by the runtime.
/// </summary>
public sealed class ApplicationCommandDefinition
{
    public required string name { get; set; }
    public int type { get; set; }
    public required string description { get; set; }
    public ApplicationCommandDefinition[]? options { get; set; }
}
