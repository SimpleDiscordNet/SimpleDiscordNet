using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Internal gateway payload for member + guild id dispatches.
/// </summary>
internal sealed record GatewayMemberEvent
{
    public required string GuildId { get; init; }
    public required Member Member { get; init; }
}
