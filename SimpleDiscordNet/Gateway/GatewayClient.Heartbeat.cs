using System.Net.WebSockets;
using System.Text.Json;

namespace SimpleDiscordNet.Gateway;

internal sealed partial class GatewayClient
{
    private void StartHeartbeat()
    {
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = new Timer(HeartbeatCallback, null, _heartbeatIntervalMs, _heartbeatIntervalMs);
    }

    private async void HeartbeatCallback(object? _)
    {
        try
        {
            if (_ws.State != WebSocketState.Open) return;
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
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            using (var writer = new System.Text.Json.Utf8JsonWriter(buffer))
            {
                JsonSerializer.Serialize(writer, hb, json);
            }
            await _ws.SendAsync(buffer.WrittenMemory, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
            _awaitingHeartbeatAck = true;
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, ex);
        }
    }
}
