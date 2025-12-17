using System.Text.Json;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Events;

namespace SimpleDiscordNet.Gateway;

internal sealed partial class GatewayClient
{
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
}
