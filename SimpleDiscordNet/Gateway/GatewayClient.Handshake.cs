using System.Net.WebSockets;
using System.Text.Json;

namespace SimpleDiscordNet.Gateway;

internal sealed partial class GatewayClient
{
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

    /// <summary>
    /// Request all members for a guild via gateway. Discord will respond with GUILD_MEMBERS_CHUNK events.
    /// Requires GuildMembers intent.
    /// </summary>
    public async Task RequestGuildMembersAsync(string guildId)
    {
        if (_ws.State != WebSocketState.Open) return;

        RequestGuildMembers request = new()
        {
            d = new RequestGuildMembersPayload
            {
                guild_id = guildId,
                query = string.Empty, // empty = all members
                limit = 0 // 0 = no limit
            }
        };
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, json));
        await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
    }
}
