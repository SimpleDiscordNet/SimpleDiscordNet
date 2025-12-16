using System.Net.WebSockets;
using System.Text.Json;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Events;

namespace SimpleDiscordNet.Gateway;

internal sealed class GatewayClient(string token, DiscordIntents intents, JsonSerializerOptions json, NativeLogger logger, TimeProvider time)
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

    public event EventHandler<GatewayMemberEvent>? GuildMemberAdd;
    public event EventHandler<GatewayMemberEvent>? GuildMemberUpdate;
    public event EventHandler<GatewayMemberEvent>? GuildMemberRemove; // Member user-only on remove

    public event EventHandler<GatewayUserEvent>? GuildBanAdd;
    public event EventHandler<GatewayUserEvent>? GuildBanRemove;

    public event EventHandler<User>? UserUpdate; // Bot user

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

    private async Task ReceiveLoop(CancellationToken ct)
    {
        byte[] buffer = new byte[1 << 16];
        System.Text.StringBuilder sb = new(4096);
        ArraySegment<byte> seg = new(buffer);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (_ws.State != WebSocketState.Open)
                {
                    if (!_autoReconnect) break;
                    await SafeReconnectAsync(ct).ConfigureAwait(false);
                    if (_ws.State != WebSocketState.Open) break;
                }
                sb.Clear();
                WebSocketReceiveResult? result;
                do
                {
                    result = await _ws.ReceiveAsync(seg, ct).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        // Attempt to reconnect, according to gateway policy
                        if (!_autoReconnect)
                        {
                            await DisconnectAsync().ConfigureAwait(false);
                            return;
                        }
                        await SafeReconnectAsync(ct).ConfigureAwait(false);
                        // continue to next iteration with new socket
                        goto ContinueLoop;
                    }
                    sb.Append(System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count));
                } while (!result.EndOfMessage);

                string json1 = sb.ToString();
                GatewayPayload? payload = JsonSerializer.Deserialize<GatewayPayload>(json1, json);
                if (payload == null) continue;
                if (payload.s.HasValue) _seq = payload.s.Value;

                switch (payload.op)
                {
                    case 10: // Hello
                        int interval = payload.d.GetProperty("heartbeat_interval").GetInt32();
                        _heartbeatIntervalMs = interval;
                        StartHeartbeat();
                        if (!string.IsNullOrEmpty(_sessionId) && _seq > 0)
                        {
                            await ResumeAsync().ConfigureAwait(false);
                        }
                        else
                        {
                            await IdentifyAsync().ConfigureAwait(false);
                        }
                        break;
                    case 11: // Heartbeat ACK
                        _awaitingHeartbeatAck = false;
                        _missedHeartbeatAcks = 0;
                        break;
                    case 0: // Dispatch
                        HandleDispatch(payload.t, payload.d);
                        break;
                    case 7: // RECONNECT
                        if (_autoReconnect)
                        {
                            await SafeReconnectAsync(ct).ConfigureAwait(false);
                        }
                        break;
                    case 9: // INVALID_SESSION
                        bool canResume = false;
                        try { canResume = payload.d.ValueKind == JsonValueKind.True || (payload.d.ValueKind == JsonValueKind.False ? false : payload.d.GetBoolean()); }
                        catch { }
                        // Random jitter 1-5s as per Discord suggestion
                        int delay = _rand.Next(1000, 5000);
                        await Task.Delay(delay, ct).ConfigureAwait(false);
                        if (canResume && !string.IsNullOrEmpty(_sessionId) && _seq > 0)
                        {
                            await ResumeAsync().ConfigureAwait(false);
                        }
                        else
                        {
                            _sessionId = null; _seq = 0;
                            await IdentifyAsync().ConfigureAwait(false);
                        }
                        break;
                }
            ContinueLoop: ;
            }
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, ex);
            if (_autoReconnect && !ct.IsCancellationRequested)
            {
                try { await SafeReconnectAsync(ct).ConfigureAwait(false); } catch { }
                // After reconnection, loop continues
                if (!ct.IsCancellationRequested) await ReceiveLoop(ct).ConfigureAwait(false);
            }
        }
    }

    private void StartHeartbeat()
    {
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = new Timer(async _ =>
        {
            try
            {
                if (_ws.State == WebSocketState.Open)
                {
                    // detect missed ack from the previous heartbeat
                    if (_awaitingHeartbeatAck)
                    {
                        _missedHeartbeatAcks++;
                        if (_missedHeartbeatAcks >= 2 && _autoReconnect)
                        {
                            await SafeReconnectAsync(_internalCts.Token).ConfigureAwait(false);
                            return;
                        }
                    }
                    Heartbeat hb = new() { d = _seq };
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(hb, json));
                    await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
                    _awaitingHeartbeatAck = true;
                }
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, ex);
            }
        }, null, _heartbeatIntervalMs, _heartbeatIntervalMs);
    }

    private async Task IdentifyAsync()
    {
        Identify identify = new()
        {
            d = new IdentifyPayload
            {
                token = token,
                intents = (int)intents,
                properties = new IdentifyConnectionProperties()
            }
        };
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(identify, json));
        await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task ResumeAsync()
    {
        if (string.IsNullOrEmpty(_sessionId))
        {
            await IdentifyAsync().ConfigureAwait(false);
            return;
        }
        Resume resume = new()
        {
            d = new ResumePayload
            {
                token = token,
                session_id = _sessionId!,
                seq = _seq
            }
        };
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(resume, json));
        await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task ConnectSocketAsync(CancellationToken ct)
    {
        _ws = new ClientWebSocket();
        _ws.Options.SetRequestHeader("User-Agent", "SimpleDiscordNet (https://example, 1.0)");
        await _ws.ConnectAsync(new Uri("wss://gateway.discord.gg/?v=10&encoding=json"), ct).ConfigureAwait(false);
        _reconnectAttempt = 0;
        _awaitingHeartbeatAck = false;
        _missedHeartbeatAcks = 0;
    }

    private int GetBackoffDelayMs()
    {
        // Exponential backoff capped at 30s with jitter
        int baseMs = (int)Math.Min(30000, 1000 * Math.Pow(2, Math.Min(8, _reconnectAttempt)));
        int jitter = _rand.Next(0, 500);
        return baseMs + jitter;
    }

    private async Task SafeReconnectAsync(CancellationToken ct)
    {
        if (Interlocked.Exchange(ref _reconnecting, 1) == 1) return;
        try
        {
            try { _heartbeatTimer?.Dispose(); } catch { }
            try
            {
                if (_ws.State == WebSocketState.Open || _ws.State == WebSocketState.CloseReceived)
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "reconnect", CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch { }

            // Backoff before reconnection
            _reconnectAttempt++;
            int delay = GetBackoffDelayMs();
            await Task.Delay(delay, ct).ConfigureAwait(false);

            await ConnectSocketAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _reconnecting, 0);
        }
    }

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

    private void TryEmitChannelEvent(JsonElement data, EventHandler<Channel>? evt)
    {
        try
        {
            // Ignore if not a guild channel (e.g., DM has no guild_id)
            if (!data.TryGetProperty("guild_id", out JsonElement gidProp)) return;
            string id = data.GetProperty("id").GetString()!;
            string name = data.TryGetProperty("name", out JsonElement n) ? (n.GetString() ?? string.Empty) : string.Empty;
            int type = data.TryGetProperty("type", out JsonElement t) ? t.GetInt32() : 0;
            string? parent = data.TryGetProperty("parent_id", out JsonElement p) && p.ValueKind != JsonValueKind.Null ? p.GetString() : null;
            string? guildId = gidProp.GetString();

            Channel ch = new()
            {
                Id = id,
                Name = name,
                Type = type,
                Parent_Id = parent,
                Guild_Id = guildId
            };
            evt?.Invoke(this, ch);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitMemberEvent(JsonElement data, EventHandler<GatewayMemberEvent>? evt)
    {
        try
        {
            if (!data.TryGetProperty("guild_id", out JsonElement gidProp)) return;
            User user = ParseUser(data.GetProperty("user"));
            string[] roles = data.TryGetProperty("roles", out JsonElement r) && r.ValueKind == JsonValueKind.Array
                ? r.EnumerateArray().Select(x => x.GetString()!).Where(s => s is not null).ToArray() 
                : [];
            string? nick = data.TryGetProperty("nick", out JsonElement n) && n.ValueKind != JsonValueKind.Null ? n.GetString() : null;

            Member member = new Member { User = user, Nick = nick, Roles = roles };
            evt?.Invoke(this, new GatewayMemberEvent { GuildId = gidProp.GetString()!, Member = member });
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitMemberRemoveEvent(JsonElement data, EventHandler<GatewayMemberEvent>? evt)
    {
        try
        {
            if (!data.TryGetProperty("guild_id", out JsonElement gidProp)) return;
            User user = ParseUser(data.GetProperty("user"));
            Member member = new Member { User = user, Nick = null, Roles = Array.Empty<string>() };
            evt?.Invoke(this, new GatewayMemberEvent { GuildId = gidProp.GetString()!, Member = member });
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitBanEvent(JsonElement data, EventHandler<GatewayUserEvent>? evt)
    {
        try
        {
            string guildId = data.GetProperty("guild_id").GetString()!;
            User user = ParseUser(data.GetProperty("user"));
            evt?.Invoke(this, new GatewayUserEvent { GuildId = guildId, User = user });
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private static User ParseUser(JsonElement obj)
    {
        return new User
        {
            Id = obj.GetProperty("id").GetString()!,
            Username = obj.TryGetProperty("username", out JsonElement un) ? (un.GetString() ?? string.Empty) : string.Empty
        };
    }

    public void Dispose()
    {
        try { _heartbeatTimer?.Dispose(); } catch { }
        try { _ws.Dispose(); } catch { }
        _internalCts.Dispose();
    }
}
