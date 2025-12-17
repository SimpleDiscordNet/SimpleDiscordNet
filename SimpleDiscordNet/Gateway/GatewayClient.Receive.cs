using System.Net.WebSockets;
using System.Text.Json;

namespace SimpleDiscordNet.Gateway;

internal sealed partial class GatewayClient
{
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
}
