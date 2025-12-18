using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Text.Json;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Events;

namespace SimpleDiscordNet.Gateway;

[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "GatewayClient uses JsonSerializerOptions configured with source-generated DiscordJsonContext for all gateway payload types.")]
[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "GatewayClient uses JsonSerializerOptions configured with source-generated DiscordJsonContext for all gateway payload types.")]
internal sealed partial class GatewayClient(string token, DiscordIntents intents, JsonSerializerOptions json, NativeLogger logger, TimeProvider time)
    : IDisposable
{
    private readonly NativeLogger _logger = logger;
    private ClientWebSocket _ws = new();
    private readonly TimeProvider _time = time;
    private readonly CancellationTokenSource _internalCts = new();
    private Task? _loopTask;
    private long _seq;
    private string? _sessionId;
    private int _heartbeatIntervalMs;
    private Timer? _heartbeatTimer;
    private volatile bool _awaitingHeartbeatAck;
    private int _missedHeartbeatAcks;
    private readonly Random _rand = new();
    private int _reconnectAttempt;
    private volatile int _reconnecting; // 0 = no, 1 = yes
    private volatile bool _autoReconnect = true;

    public event EventHandler? Connected;
    public event EventHandler<Exception?>? Disconnected;
    public event EventHandler<Exception>? Error;
    public event EventHandler<MessageCreateEvent>? MessageCreate;
    public event EventHandler<InteractionCreateEvent>? InteractionCreate;

    // Domain events (internal) to be surfaced by DiscordBot
    public event EventHandler<Guild>? GuildCreate;
    public event EventHandler<Guild>? GuildUpdate;
    public event EventHandler<string>? GuildDelete; // guild id

    public event EventHandler<Channel>? ChannelCreate;
    public event EventHandler<Channel>? ChannelUpdate;
    public event EventHandler<Channel>? ChannelDelete;

    public event EventHandler<Role>? GuildRoleCreate;
    public event EventHandler<Role>? GuildRoleUpdate;
    public event EventHandler<Role>? GuildRoleDelete;

    public event EventHandler<Channel>? ThreadCreate;
    public event EventHandler<Channel>? ThreadUpdate;
    public event EventHandler<Channel>? ThreadDelete;

    public event EventHandler<GatewayMemberEvent>? GuildMemberAdd;
    public event EventHandler<GatewayMemberEvent>? GuildMemberUpdate;
    public event EventHandler<GatewayMemberEvent>? GuildMemberRemove; // Member user-only on remove

    public event EventHandler<GatewayUserEvent>? GuildBanAdd;
    public event EventHandler<GatewayUserEvent>? GuildBanRemove;

    public event EventHandler<User>? UserUpdate; // Bot user

    public event EventHandler<MessageUpdateEvent>? MessageUpdate;
    public event EventHandler<MessageEvent>? MessageDelete;
    public event EventHandler<MessageEvent>? MessageDeleteBulk;

    public event EventHandler<ReactionEvent>? MessageReactionAdd;
    public event EventHandler<ReactionEvent>? MessageReactionRemove;
    public event EventHandler<MessageEvent>? MessageReactionRemoveAll;
    public event EventHandler<ReactionEvent>? MessageReactionRemoveEmoji;

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await ConnectSocketAsync(cancellationToken).ConfigureAwait(false);
        _loopTask = Task.Run(() => ReceiveLoop(_internalCts.Token));
        Connected?.Invoke(this, EventArgs.Empty);
    }

    public async Task DisconnectAsync()
    {
        try
        {
            _autoReconnect = false;
            await _internalCts.CancelAsync();
            if (_ws.State == WebSocketState.Open)
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "shutdown", CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, ex);
        }
        finally
        {
            try { _heartbeatTimer?.Dispose(); } catch { // ignored
            }

            Disconnected?.Invoke(this, null);
        }
    }

    // moved to partial: ReceiveLoop

    // moved to partial: StartHeartbeat

    // moved to partial: IdentifyAsync

    // moved to partial: ResumeAsync

    // moved to partial: ConnectSocketAsync

    // moved to partial: GetBackoffDelayMs

    // moved to partial: SafeReconnectAsync

    private void HandleDispatch(string? eventName, JsonElement data)
    {
        if (string.Equals(eventName, "READY", StringComparison.Ordinal))
        {
            if (data.TryGetProperty("session_id", out JsonElement sid))
                _sessionId = sid.GetString();
            return;
        }

        if (string.Equals(eventName, "MESSAGE_CREATE", StringComparison.Ordinal))
        {
            try
            {
                string id = data.GetProperty("id").GetString()!;
                string channelId = data.GetProperty("channel_id").GetString()!;
                string content = data.GetProperty("content").GetString() ?? string.Empty;
                JsonElement authorObj = data.GetProperty("author");
                Author author = new() { Id = authorObj.GetProperty("id").GetString()!, Username = authorObj.GetProperty("username").GetString()! };
                string? guildId = null;
                if (data.TryGetProperty("guild_id", out JsonElement gidEl))
                {
                    guildId = gidEl.GetString();
                }

                MessageCreateEvent evt = new()
                {
                    Id = id,
                    ChannelId = channelId,
                    GuildId = guildId,
                    Content = content,
                    Author = author
                };
                MessageCreate?.Invoke(this, evt);
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, ex);
            }
        }

        // Guild events
        else if (string.Equals(eventName, "GUILD_CREATE", StringComparison.Ordinal))
        {
            try
            {
                Guild g = new Guild
                {
                    Id = data.GetProperty("id").GetString()!,
                    Name = data.GetProperty("name").GetString() ?? string.Empty
                };
                GuildCreate?.Invoke(this, g);
            }
            catch (Exception ex) { Error?.Invoke(this, ex); }
        }
        else if (string.Equals(eventName, "GUILD_UPDATE", StringComparison.Ordinal))
        {
            try
            {
                Guild g = new Guild
                {
                    Id = data.GetProperty("id").GetString()!,
                    Name = data.GetProperty("name").GetString() ?? string.Empty
                };
                GuildUpdate?.Invoke(this, g);
            }
            catch (Exception ex) { Error?.Invoke(this, ex); }
        }
        else if (string.Equals(eventName, "GUILD_DELETE", StringComparison.Ordinal))
        {
            try
            {
                string gid = data.GetProperty("id").GetString()!;
                GuildDelete?.Invoke(this, gid);
            }
            catch (Exception ex) { Error?.Invoke(this, ex); }
        }

        // Channel events
        else if (string.Equals(eventName, "CHANNEL_CREATE", StringComparison.Ordinal))
        {
            TryEmitChannelEvent(data, ChannelCreate);
        }
        else if (string.Equals(eventName, "CHANNEL_UPDATE", StringComparison.Ordinal))
        {
            TryEmitChannelEvent(data, ChannelUpdate);
        }
        else if (string.Equals(eventName, "CHANNEL_DELETE", StringComparison.Ordinal))
        {
            TryEmitChannelEvent(data, ChannelDelete);
        }

        // Role events
        else if (string.Equals(eventName, "GUILD_ROLE_CREATE", StringComparison.Ordinal))
        {
            TryEmitRoleEvent(data, GuildRoleCreate);
        }
        else if (string.Equals(eventName, "GUILD_ROLE_UPDATE", StringComparison.Ordinal))
        {
            TryEmitRoleEvent(data, GuildRoleUpdate);
        }
        else if (string.Equals(eventName, "GUILD_ROLE_DELETE", StringComparison.Ordinal))
        {
            TryEmitRoleDeleteEvent(data, GuildRoleDelete);
        }

        // Thread events
        else if (string.Equals(eventName, "THREAD_CREATE", StringComparison.Ordinal))
        {
            TryEmitChannelEvent(data, ThreadCreate);
        }
        else if (string.Equals(eventName, "THREAD_UPDATE", StringComparison.Ordinal))
        {
            TryEmitChannelEvent(data, ThreadUpdate);
        }
        else if (string.Equals(eventName, "THREAD_DELETE", StringComparison.Ordinal))
        {
            TryEmitChannelEvent(data, ThreadDelete);
        }

        // Member events
        else if (string.Equals(eventName, "GUILD_MEMBER_ADD", StringComparison.Ordinal))
        {
            TryEmitMemberEvent(data, GuildMemberAdd);
        }
        else if (string.Equals(eventName, "GUILD_MEMBER_UPDATE", StringComparison.Ordinal))
        {
            TryEmitMemberEvent(data, GuildMemberUpdate);
        }
        else if (string.Equals(eventName, "GUILD_MEMBER_REMOVE", StringComparison.Ordinal))
        {
            TryEmitMemberRemoveEvent(data, GuildMemberRemove);
        }

        // Ban events
        else if (string.Equals(eventName, "GUILD_BAN_ADD", StringComparison.Ordinal))
        {
            TryEmitBanEvent(data, GuildBanAdd);
        }
        else if (string.Equals(eventName, "GUILD_BAN_REMOVE", StringComparison.Ordinal))
        {
            TryEmitBanEvent(data, GuildBanRemove);
        }

        // Bot user update
        else if (string.Equals(eventName, "USER_UPDATE", StringComparison.Ordinal))
        {
            try
            {
                User u = ParseUser(data);
                UserUpdate?.Invoke(this, u);
            }
            catch (Exception ex) { Error?.Invoke(this, ex); }
        }

        // Message events
        else if (string.Equals(eventName, "MESSAGE_UPDATE", StringComparison.Ordinal))
        {
            TryEmitMessageUpdateEvent(data, MessageUpdate);
        }
        else if (string.Equals(eventName, "MESSAGE_DELETE", StringComparison.Ordinal))
        {
            TryEmitMessageDeleteEvent(data, MessageDelete);
        }
        else if (string.Equals(eventName, "MESSAGE_DELETE_BULK", StringComparison.Ordinal))
        {
            TryEmitMessageDeleteBulkEvent(data, MessageDeleteBulk);
        }

        // Reaction events
        else if (string.Equals(eventName, "MESSAGE_REACTION_ADD", StringComparison.Ordinal))
        {
            TryEmitReactionEvent(data, MessageReactionAdd);
        }
        else if (string.Equals(eventName, "MESSAGE_REACTION_REMOVE", StringComparison.Ordinal))
        {
            TryEmitReactionEvent(data, MessageReactionRemove);
        }
        else if (string.Equals(eventName, "MESSAGE_REACTION_REMOVE_ALL", StringComparison.Ordinal))
        {
            TryEmitMessageDeleteEvent(data, MessageReactionRemoveAll);
        }
        else if (string.Equals(eventName, "MESSAGE_REACTION_REMOVE_EMOJI", StringComparison.Ordinal))
        {
            TryEmitReactionRemoveEmojiEvent(data, MessageReactionRemoveEmoji);
        }

        // Interactions (slash commands, components, modals)
        else if (string.Equals(eventName, "INTERACTION_CREATE", StringComparison.Ordinal))
        {
            try
            {
                int type = data.GetProperty("type").GetInt32(); // 2=command, 3=component, 5=modal submit
                string id = data.GetProperty("id").GetString()!;
                string token = data.GetProperty("token").GetString()!;
                string appId = data.GetProperty("application_id").GetString()!;
                string? guildId = data.TryGetProperty("guild_id", out JsonElement gid) && gid.ValueKind != JsonValueKind.Null ? gid.GetString() : null;
                string? channelId = data.TryGetProperty("channel_id", out JsonElement chIdEl) && chIdEl.ValueKind != JsonValueKind.Null ? chIdEl.GetString() : null;

                Author? author = null;
                if (data.TryGetProperty("member", out JsonElement memberObj) && memberObj.TryGetProperty("user", out JsonElement mu))
                {
                    author = new Author { Id = mu.GetProperty("id").GetString()!, Username = mu.TryGetProperty("username", out JsonElement un) ? (un.GetString() ?? string.Empty) : string.Empty };
                }
                else if (data.TryGetProperty("user", out JsonElement uo))
                {
                    author = new Author { Id = uo.GetProperty("id").GetString()!, Username = uo.TryGetProperty("username", out JsonElement un) ? (un.GetString() ?? string.Empty) : string.Empty };
                }

                // Switch by interaction type
                if (type == 2)
                {
                    // command data
                    JsonElement d = data.GetProperty("data");
                    string name = d.GetProperty("name").GetString()!;
                    string? sub = null;
                    List<InteractionOption> options = new System.Collections.Generic.List<InteractionOption>();
                    if (d.TryGetProperty("options", out JsonElement opts) && opts.ValueKind == JsonValueKind.Array)
                    {
                        if (opts.GetArrayLength() > 0)
                        {
                            JsonElement first = opts[0];
                            if (first.TryGetProperty("type", out JsonElement tProp) && tProp.ValueKind == JsonValueKind.Number && tProp.GetInt32() == 1)
                            {
                                // SUB_COMMAND case
                                sub = first.GetProperty("name").GetString();
                                if (first.TryGetProperty("options", out JsonElement subOpts) && subOpts.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (JsonElement o in subOpts.EnumerateArray())
                                    {
                                        string oname = o.GetProperty("name").GetString()!;
                                        string? s = null; long? i = null; bool? b = null;
                                        if (o.TryGetProperty("value", out JsonElement val))
                                        {
                                            switch (val.ValueKind)
                                            {
                                                case JsonValueKind.String: s = val.GetString(); break;
                                                case JsonValueKind.Number: if (val.TryGetInt64(out long li)) i = li; break;
                                                case JsonValueKind.True: b = true; break;
                                                case JsonValueKind.False: b = false; break;
                                            }
                                        }
                                        options.Add(new InteractionOption { Name = oname, String = s, Integer = i, Boolean = b });
                                    }
                                }
                            }
                            else
                            {
                                // Flat options (no subcommand)
                                foreach (JsonElement o in opts.EnumerateArray())
                                {
                                    string oname = o.GetProperty("name").GetString()!;
                                    string? s = null; long? i = null; bool? b = null;
                                    if (o.TryGetProperty("value", out JsonElement val))
                                    {
                                        switch (val.ValueKind)
                                        {
                                            case JsonValueKind.String: s = val.GetString(); break;
                                            case JsonValueKind.Number: if (val.TryGetInt64(out long li)) i = li; break;
                                            case JsonValueKind.True: b = true; break;
                                            case JsonValueKind.False: b = false; break;
                                        }
                                    }
                                    options.Add(new InteractionOption { Name = oname, String = s, Integer = i, Boolean = b });
                                }
                            }
                        }
                    }

                    ApplicationCommandData cmd = new() { Name = name, Subcommand = sub, Options = options };
                    InteractionCreateEvent evt = new()
                    {
                        Id = id,
                        Token = token,
                        ApplicationId = appId,
                        Type = InteractionType.ApplicationCommand,
                        GuildId = guildId,
                        ChannelId = channelId,
                        Author = author,
                        Data = cmd
                    };
                    InteractionCreate?.Invoke(this, evt);
                }
                else if (type == 3)
                {
                    // message component
                    JsonElement d = data.GetProperty("data");
                    string customId = d.GetProperty("custom_id").GetString()!;
                    int componentType = d.TryGetProperty("component_type", out JsonElement ctEl) && ctEl.ValueKind == JsonValueKind.Number ? ctEl.GetInt32() : 0;
                    string[]? values = null;
                    if (d.TryGetProperty("values", out JsonElement vEl) && vEl.ValueKind == JsonValueKind.Array)
                    {
                        values = vEl.EnumerateArray().Select(x => x.GetString()!).Where(s => s is not null).ToArray();
                    }
                    string? messageId = null;
                    if (data.TryGetProperty("message", out JsonElement msgEl) && msgEl.TryGetProperty("id", out JsonElement mid))
                        messageId = mid.GetString();

                    MessageComponentData comp = new() { CustomId = customId, ComponentType = componentType, Values = values, MessageId = messageId };
                    InteractionCreateEvent evt = new()
                    {
                        Id = id,
                        Token = token,
                        ApplicationId = appId,
                        Type = InteractionType.MessageComponent,
                        GuildId = guildId,
                        ChannelId = channelId,
                        Author = author,
                        Component = comp
                    };
                    InteractionCreate?.Invoke(this, evt);
                }
                else if (type == 5)
                {
                    // modal submit
                    JsonElement d = data.GetProperty("data");
                    string customId = d.GetProperty("custom_id").GetString()!;
                    System.Collections.Generic.List<TextInputValue> inputs = new System.Collections.Generic.List<TextInputValue>();
                    if (d.TryGetProperty("components", out JsonElement rows) && rows.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement row in rows.EnumerateArray())
                        {
                            if (row.TryGetProperty("components", out JsonElement comps) && comps.ValueKind == JsonValueKind.Array)
                            {
                                foreach (JsonElement c in comps.EnumerateArray())
                                {
                                    // text input
                                    string? customIdField = c.TryGetProperty("custom_id", out JsonElement cidEl) ? cidEl.GetString() : null;
                                    string? val = c.TryGetProperty("value", out JsonElement valEl) ? (valEl.GetString() ?? string.Empty) : null;
                                    if (!string.IsNullOrEmpty(customIdField))
                                        inputs.Add(new TextInputValue { CustomId = customIdField!, Value = val });
                                }
                            }
                        }
                    }

                    ModalSubmitData modal = new() { CustomId = customId, Inputs = inputs };
                    InteractionCreateEvent evt = new()
                    {
                        Id = id,
                        Token = token,
                        ApplicationId = appId,
                        Type = InteractionType.ModalSubmit,
                        GuildId = guildId,
                        ChannelId = channelId,
                        Author = author,
                        Modal = modal
                    };
                    InteractionCreate?.Invoke(this, evt);
                }
            }
            catch (Exception ex) { Error?.Invoke(this, ex); }
        }
    }

    // moved to partial: TryEmitChannelEvent

    // moved to partial: TryEmitMemberEvent

    // moved to partial: TryEmitMemberRemoveEvent

    // moved to partial: TryEmitBanEvent

    // moved to partial: ParseUser

    public void Dispose()
    {
        try { _heartbeatTimer?.Dispose(); } catch { }
        try { _ws.Dispose(); } catch { }
        _internalCts.Dispose();
    }
}
