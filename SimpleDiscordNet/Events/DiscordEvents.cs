using SimpleDiscordNet.Logging;

namespace SimpleDiscordNet.Events;

/// <summary>
/// Global static event hub for Discord bot events. Consumers can subscribe
/// from any project without holding a DiscordBot instance.
/// </summary>
public static class DiscordEvents
{
    // ---- Connection and logging ----
    public static event EventHandler? Connected;
    public static event EventHandler<Exception?>? Disconnected;
    public static event EventHandler<Exception>? Error;

    public static event EventHandler<LogMessage>? Log;

    internal static void RaiseConnected(object? sender)
        => Connected?.Invoke(sender, EventArgs.Empty);
    internal static void RaiseDisconnected(object? sender, Exception? ex)
        => Disconnected?.Invoke(sender, ex);
    internal static void RaiseError(object? sender, Exception ex)
        => Error?.Invoke(sender, ex);
    internal static void RaiseLog(object? sender, LogMessage msg)
        => Log?.Invoke(sender, msg);

    // ---- Domain events ----
    public static event EventHandler<GuildEvent>? GuildAdded;
    public static event EventHandler<GuildEvent>? GuildUpdated;
    public static event EventHandler<string>? GuildRemoved; // guild id

    public static event EventHandler<ChannelEvent>? ChannelCreated;
    public static event EventHandler<ChannelEvent>? ChannelUpdated;
    public static event EventHandler<ChannelEvent>? ChannelDeleted;

    public static event EventHandler<MemberEvent>? MemberJoined;
    public static event EventHandler<MemberEvent>? MemberUpdated;
    public static event EventHandler<MemberEvent>? MemberLeft; // includes kicks/leaves

    public static event EventHandler<BanEvent>? BanAdded;
    public static event EventHandler<BanEvent>? BanRemoved;

    public static event EventHandler<BotUserEvent>? BotUserUpdated; // Only the bot user per Discord API

    // Direct messages
    public static event EventHandler<DirectMessageEvent>? DirectMessageReceived;

    internal static void RaiseGuildAdded(object? sender, GuildEvent e) => GuildAdded?.Invoke(sender, e);
    internal static void RaiseGuildUpdated(object? sender, GuildEvent e) => GuildUpdated?.Invoke(sender, e);
    internal static void RaiseGuildRemoved(object? sender, string id) => GuildRemoved?.Invoke(sender, id);

    internal static void RaiseChannelCreated(object? sender, ChannelEvent e) => ChannelCreated?.Invoke(sender, e);
    internal static void RaiseChannelUpdated(object? sender, ChannelEvent e) => ChannelUpdated?.Invoke(sender, e);
    internal static void RaiseChannelDeleted(object? sender, ChannelEvent e) => ChannelDeleted?.Invoke(sender, e);

    internal static void RaiseMemberJoined(object? sender, MemberEvent e) => MemberJoined?.Invoke(sender, e);
    internal static void RaiseMemberUpdated(object? sender, MemberEvent e) => MemberUpdated?.Invoke(sender, e);
    internal static void RaiseMemberLeft(object? sender, MemberEvent e) => MemberLeft?.Invoke(sender, e);

    internal static void RaiseBanAdded(object? sender, BanEvent e) => BanAdded?.Invoke(sender, e);
    internal static void RaiseBanRemoved(object? sender, BanEvent e) => BanRemoved?.Invoke(sender, e);

    internal static void RaiseBotUserUpdated(object? sender, BotUserEvent e) => BotUserUpdated?.Invoke(sender, e);

    internal static void RaiseDirectMessageReceived(object? sender, DirectMessageEvent e) => DirectMessageReceived?.Invoke(sender, e);
}
