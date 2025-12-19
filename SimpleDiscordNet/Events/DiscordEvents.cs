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
    /// <summary>
    /// Fired when a guild is fully loaded with all members, channels, roles, and emojis.
    /// Only fires when AutoLoadFullGuildData is enabled (default).
    /// </summary>
    public static event EventHandler<GuildEvent>? GuildReady;

    public static event EventHandler<ChannelEvent>? ChannelCreated;
    public static event EventHandler<ChannelEvent>? ChannelUpdated;
    public static event EventHandler<ChannelEvent>? ChannelDeleted;

    public static event EventHandler<RoleEvent>? RoleCreated;
    public static event EventHandler<RoleEvent>? RoleUpdated;
    public static event EventHandler<RoleEvent>? RoleDeleted;

    public static event EventHandler<ThreadEvent>? ThreadCreated;
    public static event EventHandler<ThreadEvent>? ThreadUpdated;
    public static event EventHandler<ThreadEvent>? ThreadDeleted;

    public static event EventHandler<MessageUpdateEvent>? MessageUpdated;
    public static event EventHandler<MessageEvent>? MessageDeleted;
    public static event EventHandler<MessageEvent>? MessagesBulkDeleted;

    public static event EventHandler<ReactionEvent>? ReactionAdded;
    public static event EventHandler<ReactionEvent>? ReactionRemoved;
    public static event EventHandler<ReactionEvent>? ReactionsClearedForEmoji;
    public static event EventHandler<MessageEvent>? ReactionsCleared;

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
    internal static void RaiseGuildReady(object? sender, GuildEvent e) => GuildReady?.Invoke(sender, e);

    internal static void RaiseChannelCreated(object? sender, ChannelEvent e) => ChannelCreated?.Invoke(sender, e);
    internal static void RaiseChannelUpdated(object? sender, ChannelEvent e) => ChannelUpdated?.Invoke(sender, e);
    internal static void RaiseChannelDeleted(object? sender, ChannelEvent e) => ChannelDeleted?.Invoke(sender, e);

    internal static void RaiseRoleCreated(object? sender, RoleEvent e) => RoleCreated?.Invoke(sender, e);
    internal static void RaiseRoleUpdated(object? sender, RoleEvent e) => RoleUpdated?.Invoke(sender, e);
    internal static void RaiseRoleDeleted(object? sender, RoleEvent e) => RoleDeleted?.Invoke(sender, e);

    internal static void RaiseThreadCreated(object? sender, ThreadEvent e) => ThreadCreated?.Invoke(sender, e);
    internal static void RaiseThreadUpdated(object? sender, ThreadEvent e) => ThreadUpdated?.Invoke(sender, e);
    internal static void RaiseThreadDeleted(object? sender, ThreadEvent e) => ThreadDeleted?.Invoke(sender, e);

    internal static void RaiseMessageUpdated(object? sender, MessageUpdateEvent e) => MessageUpdated?.Invoke(sender, e);
    internal static void RaiseMessageDeleted(object? sender, MessageEvent e) => MessageDeleted?.Invoke(sender, e);
    internal static void RaiseMessagesBulkDeleted(object? sender, MessageEvent e) => MessagesBulkDeleted?.Invoke(sender, e);

    internal static void RaiseReactionAdded(object? sender, ReactionEvent e) => ReactionAdded?.Invoke(sender, e);
    internal static void RaiseReactionRemoved(object? sender, ReactionEvent e) => ReactionRemoved?.Invoke(sender, e);
    internal static void RaiseReactionsClearedForEmoji(object? sender, ReactionEvent e) => ReactionsClearedForEmoji?.Invoke(sender, e);
    internal static void RaiseReactionsCleared(object? sender, MessageEvent e) => ReactionsCleared?.Invoke(sender, e);

    internal static void RaiseMemberJoined(object? sender, MemberEvent e) => MemberJoined?.Invoke(sender, e);
    internal static void RaiseMemberUpdated(object? sender, MemberEvent e) => MemberUpdated?.Invoke(sender, e);
    internal static void RaiseMemberLeft(object? sender, MemberEvent e) => MemberLeft?.Invoke(sender, e);

    internal static void RaiseBanAdded(object? sender, BanEvent e) => BanAdded?.Invoke(sender, e);
    internal static void RaiseBanRemoved(object? sender, BanEvent e) => BanRemoved?.Invoke(sender, e);

    internal static void RaiseBotUserUpdated(object? sender, BotUserEvent e) => BotUserUpdated?.Invoke(sender, e);

    internal static void RaiseDirectMessageReceived(object? sender, DirectMessageEvent e) => DirectMessageReceived?.Invoke(sender, e);
}
