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

    private void TryEmitRoleEvent(JsonElement data, EventHandler<Role>? evt)
    {
        try
        {
            if (!data.TryGetProperty("guild_id", out JsonElement gidProp)) return;
            JsonElement roleData = data.GetProperty("role");

            Role role = new()
            {
                Id = roleData.GetProperty("id").GetString()!,
                Name = roleData.GetProperty("name").GetString() ?? string.Empty,
                Color = roleData.TryGetProperty("color", out JsonElement c) ? c.GetInt32() : 0,
                Position = roleData.TryGetProperty("position", out JsonElement p) ? p.GetInt32() : 0,
                Permissions = roleData.TryGetProperty("permissions", out JsonElement perms) ? perms.GetString() : null
            };
            evt?.Invoke(this, role);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitRoleDeleteEvent(JsonElement data, EventHandler<Role>? evt)
    {
        try
        {
            if (!data.TryGetProperty("guild_id", out JsonElement gidProp)) return;
            string roleId = data.GetProperty("role_id").GetString()!;

            Role role = new()
            {
                Id = roleId,
                Name = string.Empty
            };
            evt?.Invoke(this, role);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitMessageUpdateEvent(JsonElement data, EventHandler<MessageUpdateEvent>? evt)
    {
        try
        {
            string messageId = data.GetProperty("id").GetString()!;
            string channelId = data.GetProperty("channel_id").GetString()!;
            string? guildId = data.TryGetProperty("guild_id", out JsonElement gid) ? gid.GetString() : null;
            string? content = data.TryGetProperty("content", out JsonElement c) ? c.GetString() : null;
            string? editedTimestamp = data.TryGetProperty("edited_timestamp", out JsonElement et) ? et.GetString() : null;

            MessageUpdateEvent e = new()
            {
                MessageId = messageId,
                ChannelId = channelId,
                GuildId = guildId,
                Content = content,
                EditedTimestamp = editedTimestamp
            };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitMessageDeleteEvent(JsonElement data, EventHandler<MessageEvent>? evt)
    {
        try
        {
            string messageId = data.GetProperty("id").GetString()!;
            string channelId = data.GetProperty("channel_id").GetString()!;
            string? guildId = data.TryGetProperty("guild_id", out JsonElement gid) ? gid.GetString() : null;

            MessageEvent e = new()
            {
                MessageId = messageId,
                ChannelId = channelId,
                GuildId = guildId
            };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitMessageDeleteBulkEvent(JsonElement data, EventHandler<MessageEvent>? evt)
    {
        try
        {
            // For bulk delete, we emit one event per message
            if (data.TryGetProperty("ids", out JsonElement ids) && ids.ValueKind == JsonValueKind.Array)
            {
                string channelId = data.GetProperty("channel_id").GetString()!;
                string? guildId = data.TryGetProperty("guild_id", out JsonElement gid) ? gid.GetString() : null;

                foreach (JsonElement id in ids.EnumerateArray())
                {
                    MessageEvent e = new()
                    {
                        MessageId = id.GetString()!,
                        ChannelId = channelId,
                        GuildId = guildId
                    };
                    evt?.Invoke(this, e);
                }
            }
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitReactionEvent(JsonElement data, EventHandler<ReactionEvent>? evt)
    {
        try
        {
            string userId = data.GetProperty("user_id").GetString()!;
            string channelId = data.GetProperty("channel_id").GetString()!;
            string messageId = data.GetProperty("message_id").GetString()!;
            string? guildId = data.TryGetProperty("guild_id", out JsonElement gid) ? gid.GetString() : null;

            JsonElement emojiData = data.GetProperty("emoji");
            string? emojiId = emojiData.TryGetProperty("id", out JsonElement eid) ? eid.GetString() : null;
            string? emojiName = emojiData.TryGetProperty("name", out JsonElement en) ? en.GetString() : null;

            Emoji emoji = new() { Id = emojiId, Name = emojiName };

            ReactionEvent e = new()
            {
                UserId = userId,
                ChannelId = channelId,
                MessageId = messageId,
                GuildId = guildId,
                Emoji = emoji
            };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitReactionRemoveEmojiEvent(JsonElement data, EventHandler<ReactionEvent>? evt)
    {
        try
        {
            string channelId = data.GetProperty("channel_id").GetString()!;
            string messageId = data.GetProperty("message_id").GetString()!;
            string? guildId = data.TryGetProperty("guild_id", out JsonElement gid) ? gid.GetString() : null;

            JsonElement emojiData = data.GetProperty("emoji");
            string? emojiId = emojiData.TryGetProperty("id", out JsonElement eid) ? eid.GetString() : null;
            string? emojiName = emojiData.TryGetProperty("name", out JsonElement en) ? en.GetString() : null;

            Emoji emoji = new() { Id = emojiId, Name = emojiName };

            ReactionEvent e = new()
            {
                UserId = string.Empty, // No specific user for this event
                ChannelId = channelId,
                MessageId = messageId,
                GuildId = guildId,
                Emoji = emoji
            };
            evt?.Invoke(this, e);
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
