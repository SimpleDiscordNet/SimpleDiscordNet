using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Models.Context;

/// <summary>
/// A channel enriched with its guild context.
/// </summary>
public sealed record ChannelWithGuild(DiscordChannel Channel, DiscordGuild Guild)
{
    public ulong Id => Channel.Id;
    public string Name => Channel.Name;
    public int Type => Channel.Type;
    public ulong? ParentId => Channel.Parent_Id;
    public ulong GuildId => Guild.Id;
    public string GuildName => Guild.Name;
}
