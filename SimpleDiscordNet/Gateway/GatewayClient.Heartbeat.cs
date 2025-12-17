using System.Net.WebSockets;
using System.Text.Json;

namespace SimpleDiscordNet.Gateway;

internal sealed partial class GatewayClient
{
    private void StartHeartbeat()
    {
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = new System.Threading.Timer(HeartbeatCallback, null, _heartbeatIntervalMs, _heartbeatIntervalMs);
    }

    private async void HeartbeatCallback(object? _)
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
    }
}
