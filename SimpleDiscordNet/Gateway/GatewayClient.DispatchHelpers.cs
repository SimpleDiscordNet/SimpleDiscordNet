using System.Text.Json;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Events;

namespace SimpleDiscordNet.Gateway;

internal sealed partial class GatewayClient
{
    private void TryEmitChannelEvent(JsonElement data, EventHandler<DiscordChannel>? evt)
    {
        try
        {
            // Ignore if not a guild channel (e.g., DM has no guild_id)
            if (!data.TryGetProperty("guild_id", out JsonElement gidProp)) return;
            ulong id = data.GetProperty("id").GetUInt64();
            string name = data.TryGetProperty("name", out JsonElement n) ? (n.GetString() ?? string.Empty) : string.Empty;
            int type = data.TryGetProperty("type", out JsonElement t) ? t.GetInt32() : 0;
            ulong? parent = data.TryGetProperty("parent_id", out JsonElement p) && p.ValueKind != JsonValueKind.Null ? p.GetUInt64() : null;
            ulong? guildId = gidProp.GetUInt64();

            DiscordChannel ch = new()
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
            ulong guildId = gidProp.GetUInt64();
            DiscordUser user = ParseUser(data.GetProperty("user"));
            ulong[] roles = data.TryGetProperty("roles", out JsonElement r) && r.ValueKind == JsonValueKind.Array
                ? r.EnumerateArray().Select(static x => x.GetUInt64()).ToArray()
                : [];
            string? nick = data.TryGetProperty("nick", out JsonElement n) && n.ValueKind != JsonValueKind.Null ? n.GetString() : null;

            // Create placeholder guild (will be replaced with actual guild in cache)
            DiscordGuild guild = new() { Id = guildId, Name = string.Empty };
            DiscordMember member = new() { User = user, Guild = guild, Nick = nick, Roles = roles };
            evt?.Invoke(this, new GatewayMemberEvent { GuildId = guildId, Member = member });
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitMemberRemoveEvent(JsonElement data, EventHandler<GatewayMemberEvent>? evt)
    {
        try
        {
            if (!data.TryGetProperty("guild_id", out JsonElement gidProp)) return;
            ulong guildId = gidProp.GetUInt64();
            DiscordUser user = ParseUser(data.GetProperty("user"));
            // Create placeholder guild (member is being removed anyway)
            DiscordGuild guild = new() { Id = guildId, Name = string.Empty };
            DiscordMember member = new() { User = user, Guild = guild, Nick = null, Roles = [] };
            evt?.Invoke(this, new GatewayMemberEvent { GuildId = guildId, Member = member });
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitBanEvent(JsonElement data, EventHandler<GatewayUserEvent>? evt)
    {
        try
        {
            ulong guildId = data.GetProperty("guild_id").GetUInt64();
            DiscordUser user = ParseUser(data.GetProperty("user"));
            evt?.Invoke(this, new GatewayUserEvent { GuildId = guildId, User = user });
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitRoleEvent(JsonElement data, EventHandler<GatewayRoleEvent>? evt)
    {
        try
        {
            if (!data.TryGetProperty("guild_id", out JsonElement gidProp)) return;
            ulong guildId = gidProp.GetUInt64();
            JsonElement roleData = data.GetProperty("role");

            // Create placeholder guild (will be replaced with actual guild in cache)
            DiscordGuild guild = new() { Id = guildId, Name = string.Empty };

            DiscordRole role = new()
            {
                Id = roleData.GetProperty("id").GetUInt64(),
                Name = roleData.GetProperty("name").GetString() ?? string.Empty,
                Guild = guild,
                Color = roleData.TryGetProperty("color", out JsonElement c) ? c.GetInt32() : 0,
                Position = roleData.TryGetProperty("position", out JsonElement p) ? p.GetInt32() : 0,
                Permissions = roleData.TryGetProperty("permissions", out JsonElement perms) ? perms.GetUInt64() : 0UL
            };
            evt?.Invoke(this, new GatewayRoleEvent { GuildId = guildId, Role = role });
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitRoleDeleteEvent(JsonElement data, EventHandler<GatewayRoleEvent>? evt)
    {
        try
        {
            if (!data.TryGetProperty("guild_id", out JsonElement gidProp)) return;
            ulong guildId = gidProp.GetUInt64();
            ulong roleId = data.GetProperty("role_id").GetUInt64();

            // Create placeholder guild (role is being deleted anyway)
            DiscordGuild guild = new() { Id = guildId, Name = string.Empty };

            DiscordRole role = new()
            {
                Id = roleId,
                Name = string.Empty,
                Guild = guild
            };
            evt?.Invoke(this, new GatewayRoleEvent { GuildId = guildId, Role = role });
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitMessageUpdateEvent(JsonElement data, EventHandler<MessageUpdateEvent>? evt)
    {
        try
        {
            ulong messageId = data.GetProperty("id").GetUInt64();
            ulong channelId = data.GetProperty("channel_id").GetUInt64();
            ulong? guildId = data.TryGetProperty("guild_id", out JsonElement gid) ? gid.GetUInt64() : null;
            string? content = data.TryGetProperty("content", out JsonElement c) ? c.GetString() : null;
            DateTimeOffset? editedTimestamp = data.TryGetProperty("edited_timestamp", out JsonElement et) && et.ValueKind != JsonValueKind.Null
                ? DateTimeOffset.Parse(et.GetString()!) : null;

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
            ulong messageId = data.GetProperty("id").GetUInt64();
            ulong channelId = data.GetProperty("channel_id").GetUInt64();
            ulong? guildId = data.TryGetProperty("guild_id", out JsonElement gid) ? gid.GetUInt64() : null;

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
                ulong channelId = data.GetProperty("channel_id").GetUInt64();
                ulong? guildId = data.TryGetProperty("guild_id", out JsonElement gid) ? gid.GetUInt64() : null;

                foreach (JsonElement id in ids.EnumerateArray())
                {
                    MessageEvent e = new()
                    {
                        MessageId = id.GetUInt64(),
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
            ulong userId = data.GetProperty("user_id").GetUInt64();
            ulong channelId = data.GetProperty("channel_id").GetUInt64();
            ulong messageId = data.GetProperty("message_id").GetUInt64();
            ulong? guildId = data.TryGetProperty("guild_id", out JsonElement gid) ? gid.GetUInt64() : null;

            JsonElement emojiData = data.GetProperty("emoji");
            string? emojiId = emojiData.TryGetProperty("id", out JsonElement eid) ? eid.GetString() : null;
            string? emojiName = emojiData.TryGetProperty("name", out JsonElement en) ? en.GetString() : null;

            DiscordEmoji emoji = new() { Id = emojiId, Name = emojiName };

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
            ulong channelId = data.GetProperty("channel_id").GetUInt64();
            ulong messageId = data.GetProperty("message_id").GetUInt64();
            ulong? guildId = data.TryGetProperty("guild_id", out JsonElement gid) ? gid.GetUInt64() : null;

            JsonElement emojiData = data.GetProperty("emoji");
            string? emojiId = emojiData.TryGetProperty("id", out JsonElement eid) ? eid.GetString() : null;
            string? emojiName = emojiData.TryGetProperty("name", out JsonElement en) ? en.GetString() : null;

            DiscordEmoji emoji = new() { Id = emojiId, Name = emojiName };

            ReactionEvent e = new()
            {
                UserId = 0UL, // No specific user for this event
                ChannelId = channelId,
                MessageId = messageId,
                GuildId = guildId,
                Emoji = emoji
            };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private static DiscordUser ParseUser(JsonElement obj)
    {
        return new DiscordUser
        {
            Id = obj.GetProperty("id").GetUInt64(),
            Username = obj.TryGetProperty("username", out JsonElement un) ? (un.GetString() ?? string.Empty) : string.Empty
        };
    }
}
