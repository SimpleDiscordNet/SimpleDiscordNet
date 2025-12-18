using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Models.Context;

/// <summary>
/// A channel enriched with its guild context.
/// </summary>
public sealed record ChannelWithGuild(Channel Channel, Guild Guild)
{
    public string Id => Channel.Id;
    public string Name => Channel.Name;
    public int Type => Channel.Type;
    public string? ParentId => Channel.Parent_Id;
    public string GuildId => Guild.Id;
    public string GuildName => Guild.Name;
}
