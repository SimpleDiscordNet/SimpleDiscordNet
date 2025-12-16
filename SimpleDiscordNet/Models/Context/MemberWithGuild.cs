using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Models.Context;

public sealed record MemberWithGuild(Member Member, Guild Guild, User User);
