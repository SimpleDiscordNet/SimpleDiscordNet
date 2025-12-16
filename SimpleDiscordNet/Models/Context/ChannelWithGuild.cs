using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Models.Context;

public sealed record ChannelWithGuild(Channel Channel, Guild Guild);
