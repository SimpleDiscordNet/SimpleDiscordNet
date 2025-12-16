using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Models.Context;

public sealed record UserWithGuild(User User, Guild Guild, Member? Member);
