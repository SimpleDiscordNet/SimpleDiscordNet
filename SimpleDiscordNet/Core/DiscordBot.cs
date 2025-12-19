using System.Collections.Concurrent;
using System.Text.Json;
using SimpleDiscordNet.Context;
using SimpleDiscordNet.Core;
using SimpleDiscordNet.Commands;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Gateway;
using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Models.Requests;
using SimpleDiscordNet.Rest;
using SimpleDiscordNet.Events;

namespace SimpleDiscordNet;

/// <summary>
/// Entry point for creating and running a Discord bot. Use <see cref="Builder"/> to configure.
/// </summary>
public sealed class DiscordBot : IDiscordBot
{
    private readonly string _token;
    private readonly DiscordIntents _intents;
    private readonly JsonSerializerOptions _json;
    private readonly NativeLogger _logger;
    private readonly RestClient _rest;
    private readonly GatewayClient _gateway;
    private readonly SlashCommandService _slashCommands;
    private readonly ComponentService _components;
    private readonly CancellationTokenSource _cts = new();
    private readonly EntityCache _cache = new();

    private readonly bool _preloadGuilds;
    private readonly bool _preloadChannels;
    private readonly bool _preloadMembers;
    private readonly bool _autoLoadFullGuildData;

    private readonly bool _developmentMode;
    private readonly string[] _developmentGuildIds;

    private readonly ConcurrentDictionary<string, object> _services = new();
    private readonly List<IGeneratedManifest> _generatedManifests = [];

    private volatile bool _started;

    // Track pending member chunk requests to know when the guild is fully loaded
    private readonly ConcurrentDictionary<string, int> _pendingMemberChunks = new();

    private DiscordBot(
        string token,
        DiscordIntents intents,
        JsonSerializerOptions json,
        NativeLogger logger,
        TimeProvider? timeProvider,
        bool preloadGuilds,
        bool preloadChannels,
        bool preloadMembers,
        bool autoLoadFullGuildData,
        bool developmentMode,
        IEnumerable<string>? developmentGuildIds)
    {
        _token = token;
        _intents = intents;
        _json = json;
        _logger = logger;
        _preloadGuilds = preloadGuilds;
        _preloadChannels = preloadChannels;
        _preloadMembers = preloadMembers;
        _autoLoadFullGuildData = autoLoadFullGuildData;
        _developmentMode = developmentMode;
        _developmentGuildIds = developmentGuildIds?.Where(static s => !string.IsNullOrWhiteSpace(s))
                                                  .Select(static s => s.Trim())
                                                  .Distinct(StringComparer.Ordinal)
                                                  .ToArray() ?? [];

        HttpClient httpClient = new(new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
            EnableMultipleHttp2Connections = true,
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            ConnectTimeout = TimeSpan.FromSeconds(20)
        })
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        RateLimiter rateLimiter = new (timeProvider ?? TimeProvider.System);
        _rest = new RestClient(httpClient, token, json, logger, rateLimiter);
        _gateway = new GatewayClient(token, intents, json, logger, timeProvider ?? TimeProvider.System);
        _slashCommands = new SlashCommandService(logger);
        _components = new ComponentService(logger);

        // Centralized wiring
        WireGatewayEvents();
    }

    // Events are surfaced via static SimpleDiscordNet.Events.DiscordEvents

    /// <summary>
    /// Starts the bot: connects to the gateway and begins processing events.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started) return;
        _started = true;

        // Diagnostics: surface missing DM intent early
        if ((_intents & DiscordIntents.DirectMessages) == 0)
        {
            _logger.Log(LogLevel.Warning, "DirectMessages intent is not enabled; MessageCreate will not fire for direct messages (DMs). Enable DiscordIntents.DirectMessages to receive DM events.");
        }

        // Register ambient provider so consumers can access cached data.
        DiscordContext.SetProvider(_cache.SnapshotGuilds, _cache.SnapshotChannels, _cache.SnapshotMembers, _cache.SnapshotUsers, _cache.SnapshotRoles);

        // Optionally preload caches using REST (runs in background)
        if (_preloadGuilds || _preloadChannels || _preloadMembers)
        {
            _ = Task.Run(() => PreloadAsync(_cts.Token), cancellationToken);
        }

        // Generated handlers (if any) are registered during Build() via GeneratedRegistry providers.

        // In development mode, immediately sync all current slash commands to configured guilds
        if (_developmentMode)
        {
            try
            {
                if (_developmentGuildIds.Length == 0)
                {
                    _logger.Log(LogLevel.Warning, "DevelopmentMode is enabled but no DevelopmentGuildIds were provided. Skipping command sync.");
                }
                else
                {
                    await SyncSlashCommandsAsync(_developmentGuildIds, cancellationToken).ConfigureAwait(false);
                    _logger.Log(LogLevel.Information, $"Development sync complete for { _developmentGuildIds.Length } guild(s).");
                }
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, $"Development command sync failed: {ex.Message}", ex);
            }
        }

        await _gateway.ConnectAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Synchronous helper to start the bot. Prefer <see cref="StartAsync"/> in async contexts.
    /// </summary>
    public void Start()
    {
        StartAsync(_cts.Token).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Stops the bot and disposes of resources.
    /// </summary>
    public async Task StopAsync()
    {
        try
        {
            await _gateway.DisconnectAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DiscordEvents.RaiseError(this, ex);
        }
        finally
        {
            await DisposeAsync();
        }
    }

    // ---- Slash Commands ----

    // Reflection-based auto registration removed for pure source-generator mode

    /// <summary>
    /// Synchronizes all registered slash commands to the specified guild ids.
    /// </summary>
    public async Task SyncSlashCommandsAsync(IEnumerable<string> guildIds, CancellationToken ct = default)
    {
        if (guildIds is null) throw new ArgumentNullException(nameof(guildIds));
        ApplicationInfo app = await _rest.GetApplicationAsync(ct).ConfigureAwait(false)
                  ?? throw new InvalidOperationException("Failed to fetch application info");
        if (_generatedManifests.Count == 0)
            throw new InvalidOperationException("No generated command manifests were found. Ensure the source generator is referenced in the application project.");

        var typed = _generatedManifests.SelectMany(m => m.Definitions).ToArray();
        object[] defs = typed.Cast<object>().ToArray();
        foreach (string gid in guildIds)
        {
            await _rest.PutGuildCommandsAsync(app.Id, gid, defs, ct).ConfigureAwait(false);
        }
    }

    // ----- Convenience REST APIs -----

    /// <summary>
    /// Sends a simple message to a channel with optional embed.
    /// </summary>
    public Task SendMessageAsync(string channelId, string content, EmbedBuilder? embed = null, CancellationToken ct = default)
    {
        var payload = new CreateMessageRequest
        {
            content = content,
            embeds = embed is null ? null : [embed.ToModel()]
        };
        return _rest.PostAsync($"/channels/{channelId}/messages", payload, ct);
    }

    /// <summary>
    /// Sends a message with a single attachment.
    /// </summary>
    public Task SendAttachmentAsync(string channelId, string content, string fileName, ReadOnlyMemory<byte> data, EmbedBuilder? embed = null, CancellationToken ct = default)
    {
        var payload = new CreateMessageRequest
        {
            content = content,
            embeds = embed is null ? null : [embed.ToModel()],
            attachments = [new { id = 0, filename = fileName }]
        };
        return _rest.PostMultipartAsync($"/channels/{channelId}/messages", payload, (fileName, data), ct);
    }

    /// <summary>
    /// Gets a guild by id.
    /// </summary>
    public Task<Guild?> GetGuildAsync(string guildId, CancellationToken ct = default)
        => _rest.GetAsync<Guild>($"/guilds/{guildId}", ct);

    /// <summary>
    /// Gets channels of a guild.
    /// </summary>
    public Task<Channel[]?> GetGuildChannelsAsync(string guildId, CancellationToken ct = default)
        => _rest.GetAsync<Channel[]>($"/guilds/{guildId}/channels", ct);

    /// <summary>
    /// Gets roles of a guild.
    /// </summary>
    public Task<Role[]?> GetGuildRolesAsync(string guildId, CancellationToken ct = default)
        => _rest.GetAsync<Role[]>($"/guilds/{guildId}/roles", ct);

    /// <summary>
    /// Lists members of a guild with pagination support.
    /// </summary>
    public Task<Member[]?> ListGuildMembersAsync(string guildId, int limit = 1000, string? after = null, CancellationToken ct = default)
    {
        if (limit is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(limit));
        string route = $"/guilds/{guildId}/members?limit={limit}" + (after is null ? string.Empty : $"&after={after}");
        return _rest.GetAsync<Member[]>(route, ct);
    }

    /// <summary>
    /// Sets or updates a channel permission overwrite for a role or member.
    /// </summary>
    /// <param name="channelId">Channel ID</param>
    /// <param name="targetId">Role ID or User ID</param>
    /// <param name="type">0 = role, 1 = member</param>
    /// <param name="allow">Permission bits to allow (as ulong)</param>
    /// <param name="deny">Permission bits to deny (as ulong)</param>
    /// <param name="ct">Cancellation token</param>
    public Task SetChannelPermissionAsync(string channelId, string targetId, int type, ulong allow, ulong deny, CancellationToken ct = default)
    {
        var payload = new { type, allow = allow.ToString(), deny = deny.ToString() };
        return _rest.PutChannelPermissionAsync(channelId, targetId, payload, ct);
    }

    /// <summary>
    /// Deletes a channel permission overwrite.
    /// </summary>
    public Task DeleteChannelPermissionAsync(string channelId, string overwriteId, CancellationToken ct = default)
        => _rest.DeleteChannelPermissionAsync(channelId, overwriteId, ct);

    /// <summary>
    /// Creates a new role in a guild.
    /// </summary>
    /// <param name="guildId">Guild ID</param>
    /// <param name="name">Role name</param>
    /// <param name="permissions">Permission bits (as ulong)</param>
    /// <param name="color">Role color (RGB integer)</param>
    /// <param name="hoist">Whether to display role separately in sidebar</param>
    /// <param name="mentionable">Whether role is mentionable</param>
    /// <param name="ct">Cancellation token</param>
    public Task<Role?> CreateRoleAsync(string guildId, string? name = null, ulong? permissions = null, int? color = null, bool? hoist = null, bool? mentionable = null, CancellationToken ct = default)
    {
        var payload = new
        {
            name,
            permissions = permissions?.ToString(),
            color,
            hoist,
            mentionable
        };
        return _rest.PostGuildRoleAsync<Role>(guildId, payload, ct);
    }

    /// <summary>
    /// Modifies a role in a guild.
    /// </summary>
    public Task<Role?> ModifyRoleAsync(string guildId, string roleId, string? name = null, ulong? permissions = null, int? color = null, bool? hoist = null, bool? mentionable = null, CancellationToken ct = default)
    {
        var payload = new
        {
            name,
            permissions = permissions?.ToString(),
            color,
            hoist,
            mentionable
        };
        return _rest.PatchGuildRoleAsync<Role>(guildId, roleId, payload, ct);
    }

    /// <summary>
    /// Deletes a role from a guild.
    /// </summary>
    public Task DeleteRoleAsync(string guildId, string roleId, CancellationToken ct = default)
        => _rest.DeleteGuildRoleAsync(guildId, roleId, ct);

    /// <summary>
    /// Creates a new channel in a guild.
    /// </summary>
    /// <param name="guildId">Guild ID</param>
    /// <param name="name">Channel name</param>
    /// <param name="type">Channel type (0=text, 2=voice, 4=category, etc.)</param>
    /// <param name="parentId">Parent category ID (optional)</param>
    /// <param name="permissionOverwrites">Permission overwrites (optional)</param>
    /// <param name="ct">Cancellation token</param>
    public Task<Channel?> CreateChannelAsync(string guildId, string name, int type, string? parentId = null, object[]? permissionOverwrites = null, CancellationToken ct = default)
    {
        var payload = new
        {
            name,
            type,
            parent_id = parentId,
            permission_overwrites = permissionOverwrites
        };
        return _rest.PostGuildChannelAsync<Channel>(guildId, payload, ct);
    }

    /// <summary>
    /// Creates a new text channel in a guild.
    /// </summary>
    public Task<Channel?> CreateTextChannelAsync(string guildId, string name, string? parentId = null, CancellationToken ct = default)
        => CreateChannelAsync(guildId, name, Channel.ChannelType.GuildText, parentId, ct: ct);

    /// <summary>
    /// Creates a new voice channel in a guild.
    /// </summary>
    public Task<Channel?> CreateVoiceChannelAsync(string guildId, string name, string? parentId = null, CancellationToken ct = default)
        => CreateChannelAsync(guildId, name, Channel.ChannelType.GuildVoice, parentId, ct: ct);

    /// <summary>
    /// Creates a new category channel in a guild.
    /// </summary>
    public Task<Channel?> CreateCategoryAsync(string guildId, string name, CancellationToken ct = default)
        => CreateChannelAsync(guildId, name, Channel.ChannelType.GuildCategory, ct: ct);

    /// <summary>
    /// Modifies a channel.
    /// </summary>
    public Task<Channel?> ModifyChannelAsync(string channelId, string? name = null, int? type = null, string? parentId = null, CancellationToken ct = default)
    {
        var payload = new
        {
            name,
            type,
            parent_id = parentId
        };
        return _rest.PatchChannelAsync<Channel>(channelId, payload, ct);
    }

    /// <summary>
    /// Deletes a channel.
    /// </summary>
    public Task DeleteChannelAsync(string channelId, CancellationToken ct = default)
        => _rest.DeleteChannelAsync(channelId, ct);

    // ---- Message management ----

    /// <summary>
    /// Gets a specific message from a channel.
    /// Example: var message = await bot.GetMessageAsync(channelId, messageId);
    /// </summary>
    public Task<Message?> GetMessageAsync(string channelId, string messageId, CancellationToken ct = default)
        => _rest.GetChannelMessageAsync<Message>(channelId, messageId, ct);

    /// <summary>
    /// Gets recent messages from a channel (up to 100).
    /// Example: var messages = await bot.GetMessagesAsync(channelId, limit: 50);
    /// </summary>
    /// <param name="channelId">Channel ID</param>
    /// <param name="limit">Number of messages to retrieve (1-100, default 50)</param>
    /// <param name="before">Get messages before this message ID</param>
    /// <param name="after">Get messages after this message ID</param>
    public Task<Message[]?> GetMessagesAsync(string channelId, int limit = 50, string? before = null, string? after = null, CancellationToken ct = default)
    {
        if (limit < 1 || limit > 100) throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 100");
        return _rest.GetChannelMessagesAsync<Message[]>(channelId, limit, before, after, ct);
    }

    /// <summary>
    /// Edits a message. Only works on messages sent by the bot.
    /// Example: await bot.EditMessageAsync(channelId, messageId, "Updated content");
    /// </summary>
    public Task<Message?> EditMessageAsync(string channelId, string messageId, string content, EmbedBuilder? embed = null, CancellationToken ct = default)
    {
        var payload = new
        {
            content,
            embeds = embed is null ? null : new[] { embed.ToModel() }
        };
        return _rest.PatchMessageAsync<Message>(channelId, messageId, payload, ct);
    }

    /// <summary>
    /// Deletes a message. Requires appropriate permissions.
    /// Example: await bot.DeleteMessageAsync(channelId, messageId);
    /// </summary>
    public Task DeleteMessageAsync(string channelId, string messageId, CancellationToken ct = default)
        => _rest.DeleteMessageAsync(channelId, messageId, ct);

    /// <summary>
    /// Bulk deletes multiple messages (2-100 messages, must be less than 14 days old).
    /// Example: await bot.BulkDeleteMessagesAsync(channelId, new[] { msgId1, msgId2, msgId3 });
    /// </summary>
    public Task BulkDeleteMessagesAsync(string channelId, string[] messageIds, CancellationToken ct = default)
    {
        if (messageIds.Length < 2 || messageIds.Length > 100)
            throw new ArgumentException("Must provide between 2 and 100 message IDs", nameof(messageIds));
        return _rest.BulkDeleteMessagesAsync(channelId, messageIds, ct);
    }

    // ---- Reaction management ----

    /// <summary>
    /// Adds a reaction to a message.
    /// Example (Unicode emoji): await bot.AddReactionAsync(channelId, messageId, "👍");
    /// Example (custom emoji): await bot.AddReactionAsync(channelId, messageId, "custom_emoji:123456789");
    /// </summary>
    /// <param name="channelId">Channel ID</param>
    /// <param name="messageId">Message ID</param>
    /// <param name="emoji">Unicode emoji or custom emoji in format "name:id"</param>
    public Task AddReactionAsync(string channelId, string messageId, string emoji, CancellationToken ct = default)
    {
        string encoded = System.Web.HttpUtility.UrlEncode(emoji);
        return _rest.AddReactionAsync(channelId, messageId, encoded, ct);
    }

    /// <summary>
    /// Adds a reaction to a message using an Emoji object.
    /// Example: await bot.AddReactionAsync(channelId, messageId, Emoji.Unicode("👍"));
    /// Example: await bot.AddReactionAsync(channelId, messageId, Emoji.Custom("custom", "123456789"));
    /// </summary>
    public Task AddReactionAsync(string channelId, string messageId, Emoji emoji, CancellationToken ct = default)
    {
        string encoded = System.Web.HttpUtility.UrlEncode(emoji.GetReactionFormat());
        return _rest.AddReactionAsync(channelId, messageId, encoded, ct);
    }

    /// <summary>
    /// Removes the bot's own reaction from a message.
    /// Example: await bot.RemoveOwnReactionAsync(channelId, messageId, "👍");
    /// </summary>
    public Task RemoveOwnReactionAsync(string channelId, string messageId, string emoji, CancellationToken ct = default)
    {
        string encoded = System.Web.HttpUtility.UrlEncode(emoji);
        return _rest.RemoveOwnReactionAsync(channelId, messageId, encoded, ct);
    }

    /// <summary>
    /// Removes a user's reaction from a message. Requires MANAGE_MESSAGES permission.
    /// Example: await bot.RemoveUserReactionAsync(channelId, messageId, "👍", userId);
    /// </summary>
    public Task RemoveUserReactionAsync(string channelId, string messageId, string emoji, string userId, CancellationToken ct = default)
    {
        string encoded = System.Web.HttpUtility.UrlEncode(emoji);
        return _rest.RemoveUserReactionAsync(channelId, messageId, encoded, userId, ct);
    }

    /// <summary>
    /// Gets users who reacted with a specific emoji (up to 100).
    /// Example: var users = await bot.GetReactionsAsync(channelId, messageId, "👍");
    /// </summary>
    public Task<User[]?> GetReactionsAsync(string channelId, string messageId, string emoji, int limit = 25, CancellationToken ct = default)
    {
        if (limit < 1 || limit > 100) throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 100");
        string encoded = System.Web.HttpUtility.UrlEncode(emoji);
        return _rest.GetReactionsAsync<User[]>(channelId, messageId, encoded, limit, ct);
    }

    /// <summary>
    /// Removes all reactions from a message. Requires MANAGE_MESSAGES permission.
    /// Example: await bot.RemoveAllReactionsAsync(channelId, messageId);
    /// </summary>
    public Task RemoveAllReactionsAsync(string channelId, string messageId, CancellationToken ct = default)
        => _rest.RemoveAllReactionsAsync(channelId, messageId, ct);

    /// <summary>
    /// Removes all reactions for a specific emoji from a message. Requires MANAGE_MESSAGES permission.
    /// Example: await bot.RemoveAllReactionsForEmojiAsync(channelId, messageId, "👍");
    /// </summary>
    public Task RemoveAllReactionsForEmojiAsync(string channelId, string messageId, string emoji, CancellationToken ct = default)
    {
        string encoded = System.Web.HttpUtility.UrlEncode(emoji);
        return _rest.RemoveAllReactionsForEmojiAsync(channelId, messageId, encoded, ct);
    }

    // ---- Pin management ----

    /// <summary>
    /// Pins a message in a channel. Requires MANAGE_MESSAGES permission. Max 50 pins per channel.
    /// Example: await bot.PinMessageAsync(channelId, messageId);
    /// </summary>
    public Task PinMessageAsync(string channelId, string messageId, CancellationToken ct = default)
        => _rest.PinMessageAsync(channelId, messageId, ct);

    /// <summary>
    /// Unpins a message in a channel. Requires MANAGE_MESSAGES permission.
    /// Example: await bot.UnpinMessageAsync(channelId, messageId);
    /// </summary>
    public Task UnpinMessageAsync(string channelId, string messageId, CancellationToken ct = default)
        => _rest.UnpinMessageAsync(channelId, messageId, ct);

    /// <summary>
    /// Gets all pinned messages in a channel (up to 50).
    /// Example: var pinnedMessages = await bot.GetPinnedMessagesAsync(channelId);
    /// </summary>
    public Task<Message[]?> GetPinnedMessagesAsync(string channelId, CancellationToken ct = default)
        => _rest.GetPinnedMessagesAsync<Message[]>(channelId, ct);

    // ---- Member moderation ----

    /// <summary>
    /// Kicks a member from the guild. Requires KICK_MEMBERS permission.
    /// Example: await bot.KickMemberAsync(guildId, userId);
    /// </summary>
    public Task KickMemberAsync(string guildId, string userId, CancellationToken ct = default)
        => _rest.KickMemberAsync(guildId, userId, ct);

    /// <summary>
    /// Bans a member from the guild. Requires BAN_MEMBERS permission.
    /// Example: await bot.BanMemberAsync(guildId, userId, deleteMessageDays: 7);
    /// </summary>
    /// <param name="guildId">Guild ID</param>
    /// <param name="userId">User ID to ban</param>
    /// <param name="deleteMessageDays">Number of days of messages to delete (0-7, optional)</param>
    public Task BanMemberAsync(string guildId, string userId, int? deleteMessageDays = null, CancellationToken ct = default)
    {
        if (deleteMessageDays.HasValue && (deleteMessageDays.Value < 0 || deleteMessageDays.Value > 7))
            throw new ArgumentOutOfRangeException(nameof(deleteMessageDays), "Must be between 0 and 7 days");
        return _rest.BanMemberAsync(guildId, userId, deleteMessageDays, ct);
    }

    /// <summary>
    /// Unbans a user from the guild. Requires BAN_MEMBERS permission.
    /// Example: await bot.UnbanMemberAsync(guildId, userId);
    /// </summary>
    public Task UnbanMemberAsync(string guildId, string userId, CancellationToken ct = default)
        => _rest.UnbanMemberAsync(guildId, userId, ct);

    // ---- Role assignment ----

    /// <summary>
    /// Adds a role to a guild member. Requires MANAGE_ROLES permission.
    /// Example: await bot.AddRoleToMemberAsync(guildId, userId, roleId);
    /// </summary>
    public Task AddRoleToMemberAsync(string guildId, string userId, string roleId, CancellationToken ct = default)
        => _rest.AddMemberRoleAsync(guildId, userId, roleId, ct);

    /// <summary>
    /// Removes a role from a guild member. Requires MANAGE_ROLES permission.
    /// Example: await bot.RemoveRoleFromMemberAsync(guildId, userId, roleId);
    /// </summary>
    public Task RemoveRoleFromMemberAsync(string guildId, string userId, string roleId, CancellationToken ct = default)
        => _rest.RemoveMemberRoleAsync(guildId, userId, roleId, ct);

    // ---- Typing indicator ----

    /// <summary>
    /// Triggers the typing indicator in a channel. Lasts 10 seconds or until a message is sent.
    /// Example: await bot.TriggerTypingAsync(channelId);
    /// </summary>
    public Task TriggerTypingAsync(string channelId, CancellationToken ct = default)
        => _rest.TriggerTypingIndicatorAsync(channelId, ct);

    // ---- Thread operations ----

    /// <summary>
    /// Joins a thread channel.
    /// Example: await bot.JoinThreadAsync(threadId);
    /// </summary>
    public Task JoinThreadAsync(string threadId, CancellationToken ct = default)
        => _rest.JoinThreadAsync(threadId, ct);

    /// <summary>
    /// Leaves a thread channel.
    /// Example: await bot.LeaveThreadAsync(threadId);
    /// </summary>
    public Task LeaveThreadAsync(string threadId, CancellationToken ct = default)
        => _rest.LeaveThreadAsync(threadId, ct);

    /// <summary>
    /// Adds a member to a thread. Requires MANAGE_THREADS permission.
    /// Example: await bot.AddThreadMemberAsync(threadId, userId);
    /// </summary>
    public Task AddThreadMemberAsync(string threadId, string userId, CancellationToken ct = default)
        => _rest.AddThreadMemberAsync(threadId, userId, ct);

    /// <summary>
    /// Removes a member from a thread. Requires MANAGE_THREADS permission.
    /// Example: await bot.RemoveThreadMemberAsync(threadId, userId);
    /// </summary>
    public Task RemoveThreadMemberAsync(string threadId, string userId, CancellationToken ct = default)
        => _rest.RemoveThreadMemberAsync(threadId, userId, ct);

    private void OnInteractionCreate(object? sender, InteractionCreateEvent e)
    {
        if (e is { Type: InteractionType.ApplicationCommand, Data: not null })
        {
            _ = _slashCommands.HandleAsync(e, _rest, _cts.Token);
        }
        else if (e is { Type: InteractionType.MessageComponent, Component: not null } or { Type: InteractionType.ModalSubmit, Modal: not null })
        {
            _ = _components.HandleAsync(e, _rest, _cts.Token);
        }
    }

    private void WireGatewayEvents()
    {
        // Forward logger to static event hub
        _logger.Logged += (_, msg) => DiscordEvents.RaiseLog(this, msg);

        // Basic lifecycle and message routing
        _gateway.Connected += (_, __) => DiscordEvents.RaiseConnected(this);
        _gateway.Disconnected += (_, ex) => DiscordEvents.RaiseDisconnected(this, ex);
        _gateway.Error += (_, ex) => DiscordEvents.RaiseError(this, ex);
        // Direct messages
        _gateway.MessageCreate += (_, msg) =>
        {
            // DM when no guild id present
            if (msg.GuildId is null)
            {
                var ctx = new CommandContext(msg.ChannelId, msg, _rest);
                DiscordEvents.RaiseDirectMessageReceived(this, new DirectMessageEvent { Message = msg, Context = ctx });
            }
        };
        // Slash interactions
        _gateway.InteractionCreate += OnInteractionCreate;

        // Guild events
        _gateway.GuildCreate += (_, evt) =>
        {
            _cache.UpsertGuild(evt.Guild);

            // Cache channels from GUILD_CREATE
            if (evt.Channels is not null && evt.Channels.Length > 0)
            {
                _cache.SetChannels(evt.Guild.Id, evt.Channels);
            }

            // Cache members from GUILD_CREATE (partial list)
            if (evt.Members is not null && evt.Members.Length > 0)
            {
                _cache.SetMembers(evt.Guild.Id, evt.Members);
            }

            // Cache threads from GUILD_CREATE (threads are channels)
            if (evt.Threads is not null && evt.Threads.Length > 0)
            {
                foreach (Channel thread in evt.Threads)
                {
                    _cache.UpsertChannel(evt.Guild.Id, thread);
                }
            }

            DiscordEvents.RaiseGuildAdded(this, new GuildEvent { Guild = evt.Guild });

            // Auto-load complete guild data if enabled
            if (_autoLoadFullGuildData)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await LoadCompleteGuildDataAsync(evt.Guild.Id, evt.Channels, evt.Members);
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(LogLevel.Warning, $"Failed to load complete guild data for {evt.Guild.Id}: {ex.Message}");
                    }
                });
            }
        };
        _gateway.GuildUpdate += (_, g) =>
        {
            _cache.UpsertGuild(g);
            DiscordEvents.RaiseGuildUpdated(this, new GuildEvent { Guild = g });
        };
        _gateway.GuildDelete += (_, gid) =>
        {
            _cache.RemoveGuild(gid);
            DiscordEvents.RaiseGuildRemoved(this, gid);
        };
        _gateway.GuildEmojisUpdate += (_, e) =>
        {
            _cache.SetEmojis(e.GuildId, e.Emojis);
        };

        // Channel events
        _gateway.ChannelCreate += (_, ch) =>
        {
            string? gid = ch.Guild_Id;
            if (gid is null)
            {
                _logger.Log(LogLevel.Information, $"DM channel created (id={ch.Id}, type={ch.Type}).");
                return; // do not cache DM channels
            }
            _cache.UpsertChannel(gid, ch);
            Guild guild = _cache.TryGetGuild(gid, out Guild g) ? g : new Guild { Id = gid, Name = string.Empty };
            DiscordEvents.RaiseChannelCreated(this, new ChannelEvent { Channel = ch, Guild = guild });
        };
        _gateway.ChannelUpdate += (_, ch) =>
        {
            string? gid = ch.Guild_Id;
            if (gid is null)
            {
                _logger.Log(LogLevel.Information, $"DM channel updated (id={ch.Id}, type={ch.Type}).");
                return;
            }
            _cache.UpsertChannel(gid, ch);
            Guild guild = _cache.TryGetGuild(gid, out Guild g) ? g : new Guild { Id = gid, Name = string.Empty };
            DiscordEvents.RaiseChannelUpdated(this, new ChannelEvent { Channel = ch, Guild = guild });
        };
        _gateway.ChannelDelete += (_, ch) =>
        {
            string? gid = ch.Guild_Id;
            if (gid is null)
            {
                _logger.Log(LogLevel.Information, $"DM channel deleted (id={ch.Id}, type={ch.Type}).");
                return;
            }
            _cache.RemoveChannel(gid, ch.Id);
            Guild guild = _cache.TryGetGuild(gid, out Guild g) ? g : new Guild { Id = gid, Name = string.Empty };
            DiscordEvents.RaiseChannelDeleted(this, new ChannelEvent { Channel = ch, Guild = guild });
        };

        // Role events
        _gateway.GuildRoleCreate += (_, e) =>
        {
            _cache.UpsertRole(e.GuildId, e.Role);
            Guild guild = _cache.TryGetGuild(e.GuildId, out Guild g) ? g : new Guild { Id = e.GuildId, Name = string.Empty };
            DiscordEvents.RaiseRoleCreated(this, new RoleEvent { Role = e.Role, Guild = guild });
        };
        _gateway.GuildRoleUpdate += (_, e) =>
        {
            _cache.UpsertRole(e.GuildId, e.Role);
            Guild guild = _cache.TryGetGuild(e.GuildId, out Guild g) ? g : new Guild { Id = e.GuildId, Name = string.Empty };
            DiscordEvents.RaiseRoleUpdated(this, new RoleEvent { Role = e.Role, Guild = guild });
        };
        _gateway.GuildRoleDelete += (_, e) =>
        {
            _cache.RemoveRole(e.GuildId, e.Role.Id);
            Guild guild = _cache.TryGetGuild(e.GuildId, out Guild g) ? g : new Guild { Id = e.GuildId, Name = string.Empty };
            DiscordEvents.RaiseRoleDeleted(this, new RoleEvent { Role = e.Role, Guild = guild });
        };

        // Thread events
        _gateway.ThreadCreate += (_, thread) =>
        {
            string? gid = thread.Guild_Id;
            if (gid is null) return;
            Guild guild = _cache.TryGetGuild(gid, out Guild g) ? g : new Guild { Id = gid, Name = string.Empty };
            DiscordEvents.RaiseThreadCreated(this, new ThreadEvent { Thread = thread, Guild = guild });
        };
        _gateway.ThreadUpdate += (_, thread) =>
        {
            string? gid = thread.Guild_Id;
            if (gid is null) return;
            Guild guild = _cache.TryGetGuild(gid, out Guild g) ? g : new Guild { Id = gid, Name = string.Empty };
            DiscordEvents.RaiseThreadUpdated(this, new ThreadEvent { Thread = thread, Guild = guild });
        };
        _gateway.ThreadDelete += (_, thread) =>
        {
            string? gid = thread.Guild_Id;
            if (gid is null) return;
            Guild guild = _cache.TryGetGuild(gid, out Guild g) ? g : new Guild { Id = gid, Name = string.Empty };
            DiscordEvents.RaiseThreadDeleted(this, new ThreadEvent { Thread = thread, Guild = guild });
        };

        // Member events
        _gateway.GuildMemberAdd += (_, e) =>
        {
            _cache.UpsertMember(e.GuildId, e.Member);
            Guild guild = _cache.TryGetGuild(e.GuildId, out Guild g) ? g : new Guild { Id = e.GuildId, Name = string.Empty };
            DiscordEvents.RaiseMemberJoined(this, new MemberEvent { Member = e.Member, User = e.Member.User, Guild = guild });
        };
        _gateway.GuildMemberUpdate += (_, e) =>
        {
            _cache.UpsertMember(e.GuildId, e.Member);
            Guild guild = _cache.TryGetGuild(e.GuildId, out Guild g) ? g : new Guild { Id = e.GuildId, Name = string.Empty };
            DiscordEvents.RaiseMemberUpdated(this, new MemberEvent { Member = e.Member, User = e.Member.User, Guild = guild });
        };
        _gateway.GuildMemberRemove += (_, e) =>
        {
            _cache.RemoveMember(e.GuildId, e.Member.User.Id);
            Guild guild = _cache.TryGetGuild(e.GuildId, out Guild g) ? g : new Guild { Id = e.GuildId, Name = string.Empty };
            DiscordEvents.RaiseMemberLeft(this, new MemberEvent { Member = e.Member, User = e.Member.User, Guild = guild });
        };
        _gateway.GuildMembersChunk += (_, e) =>
        {
            // Accumulate members from all chunks
            // Note: Discord may send multiple chunks for large guilds
            foreach (Member member in e.Members)
            {
                _cache.UpsertMember(e.GuildId, member);
            }

            _logger.Log(LogLevel.Debug, $"Received member chunk {e.ChunkIndex + 1}/{e.ChunkCount} for guild {e.GuildId} ({e.Members.Length} members)");

            // Check if this was the last chunk
            if (e.ChunkIndex == e.ChunkCount - 1)
            {
                // All member chunks received, mark guild as ready
                if (_pendingMemberChunks.TryRemove(e.GuildId, out int _))
                {
                    if (_cache.TryGetGuild(e.GuildId, out Guild guild))
                    {
                        _logger.Log(LogLevel.Information, $"Guild {guild.Name} ({e.GuildId}) is fully loaded");
                        DiscordEvents.RaiseGuildReady(this, new GuildEvent { Guild = guild });
                    }
                }
            }
        };

        // Ban events
        _gateway.GuildBanAdd += (_, e) =>
        {
            _cache.RemoveMember(e.GuildId, e.User.Id);
            Guild guild = _cache.TryGetGuild(e.GuildId, out Guild g) ? g : new Guild { Id = e.GuildId, Name = string.Empty };
            DiscordEvents.RaiseBanAdded(this, new BanEvent { User = e.User, Guild = guild, Member = null });
        };
        _gateway.GuildBanRemove += (_, e) =>
        {
            Guild guild = _cache.TryGetGuild(e.GuildId, out Guild g) ? g : new Guild { Id = e.GuildId, Name = string.Empty };
            DiscordEvents.RaiseBanRemoved(this, new BanEvent { User = e.User, Guild = guild, Member = null });
        };

        // Bot user change
        _gateway.UserUpdate += (_, u) => DiscordEvents.RaiseBotUserUpdated(this, new BotUserEvent { User = u });

        // Message events
        _gateway.MessageUpdate += (_, e) => DiscordEvents.RaiseMessageUpdated(this, e);
        _gateway.MessageDelete += (_, e) => DiscordEvents.RaiseMessageDeleted(this, e);
        _gateway.MessageDeleteBulk += (_, e) => DiscordEvents.RaiseMessagesBulkDeleted(this, e);

        // Reaction events
        _gateway.MessageReactionAdd += (_, e) => DiscordEvents.RaiseReactionAdded(this, e);
        _gateway.MessageReactionRemove += (_, e) => DiscordEvents.RaiseReactionRemoved(this, e);
        _gateway.MessageReactionRemoveAll += (_, e) => DiscordEvents.RaiseReactionsCleared(this, e);
        _gateway.MessageReactionRemoveEmoji += (_, e) => DiscordEvents.RaiseReactionsClearedForEmoji(this, e);
    }

    /// <summary>
    /// Disposes managed resources asynchronously.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _gateway.Dispose();
        _rest.Dispose();
        _cts.Dispose();
        DiscordContext.ClearProvider();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Disposes managed resources. Prefer <see cref="DisposeAsync"/> when possible.
    /// </summary>
    public void Dispose()
    {
        _ = DisposeAsync();
    }

    /// <summary>
    /// Create a new builder instance for configuring the bot.
    /// </summary>
    public static Builder NewBuilder() => new Builder();

    /// <summary>
    /// Factory method for DI scenarios to create a bot instance from <see cref="DiscordBotOptions"/>.
    /// No external DI packages are required; you can register this factory with your container of choice.
    /// </summary>
    public static DiscordBot FromOptions(DiscordBotOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(options.Token)) throw new InvalidOperationException("Token is required");

        DiscordBot bot = new DiscordBot(
            options.Token,
            options.Intents,
            options.JsonOptions,
            new NativeLogger(options.MinimumLogLevel, options.LogSink),
            options.TimeProvider,
            options.PreloadGuilds,
            options.PreloadChannels,
            options.PreloadMembers,
            options.AutoLoadFullGuildData,
            options.DevelopmentMode,
            options.DevelopmentGuildIds);

        return bot;
    }

    public sealed class Builder
    {
        private string? _token;
        private DiscordIntents _intents = DiscordIntents.Guilds | DiscordIntents.GuildMessages | DiscordIntents.DirectMessages | DiscordIntents.MessageContent;
        private JsonSerializerOptions _json = new(Serialization.DiscordJsonContext.Default.Options)
        {
            // Preserve prior behavior on top of source-generated options
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
            TypeInfoResolver = Serialization.DiscordJsonContext.Default
        };
        private NativeLogger _logger = new();
        private TimeProvider? _timeProvider;
        private bool _preloadGuilds;
        private bool _preloadChannels;
        private bool _preloadMembers;
        private bool _autoLoadFullGuildData = true; // Default enabled
        private bool _developmentMode;
        private readonly List<string> _developmentGuildIds = [];

        /// <summary>
        /// Sets the bot token used for authentication.
        /// </summary>
        public Builder WithToken(string token)
        {
            _token = token ?? throw new ArgumentNullException(nameof(token));
            return this;
        }

        /// <summary>
        /// Sets the Discord gateway intents for the bot.
        /// </summary>
        public Builder WithIntents(DiscordIntents intents)
        {
            _intents = intents;
            return this;
        }

        /// <summary>
        /// Sets the logger instance used by the bot.
        /// </summary>
        public Builder WithLogger(NativeLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            return this;
        }

        /// <summary>
        /// Sets JSON serialization options used for REST and gateway payloads.
        /// </summary>
        public Builder WithJsonOptions(JsonSerializerOptions options)
        {
            _json = options ?? throw new ArgumentNullException(nameof(options));
            return this;
        }

        /// <summary>
        /// Sets the time provider used for timing and rate limiting operations.
        /// </summary>
        public Builder WithTimeProvider(TimeProvider provider)
        {
            _timeProvider = provider ?? throw new ArgumentNullException(nameof(provider));
            return this;
        }

        /// <summary>
        /// Preload caches on Start using REST. Members require the GuildMembers intent and can be heavy.
        /// </summary>
        public Builder WithPreloadOnStart(bool guilds = true, bool channels = true, bool members = false)
        {
            _preloadGuilds = guilds;
            _preloadChannels = channels;
            _preloadMembers = members;
            return this;
        }

        /// <summary>
        /// Enables or disables development mode. When enabled, all registered slash commands
        /// are synchronized immediately to the configured development guilds on Start.
        /// </summary>
        public Builder WithDevelopmentMode(bool enabled = true)
        {
            _developmentMode = enabled;
            return this;
        }

        /// <summary>
        /// Adds a single development guild id for immediate slash command synchronization.
        /// </summary>
        public Builder WithDevelopmentGuild(string guildId)
        {
            if (!string.IsNullOrWhiteSpace(guildId))
                _developmentGuildIds.Add(guildId.Trim());
            return this;
        }

        /// <summary>
        /// Adds multiple development guild ids for immediate slash command synchronization.
        /// </summary>
        public Builder WithDevelopmentGuilds(IEnumerable<string> guildIds)
        {
            ArgumentNullException.ThrowIfNull(guildIds);
            foreach (string id in guildIds)
            {
                if (!string.IsNullOrWhiteSpace(id)) _developmentGuildIds.Add(id.Trim());
            }
            return this;
        }

        /// <summary>
        /// Controls whether the bot automatically loads complete guild data after GUILD_CREATE.
        /// When enabled (default), fires GuildReady event when guild is fully loaded with all members, channels, roles, etc.
        /// </summary>
        public Builder WithAutoLoadFullGuildData(bool enabled = true)
        {
            _autoLoadFullGuildData = enabled;
            return this;
        }

        /// <summary>
        /// Builds a configured <see cref="DiscordBot"/> instance.
        /// </summary>
        public DiscordBot Build()
        {
            if (string.IsNullOrWhiteSpace(_token))
                throw new InvalidOperationException("Token is required");

            DiscordBot bot = new(_token!, _intents, _json, _logger, _timeProvider, _preloadGuilds, _preloadChannels, _preloadMembers, _autoLoadFullGuildData, _developmentMode, _developmentGuildIds);

            // Auto-register any manifests provided by source-generated initializers
            foreach (IGeneratedManifestProvider provider in GeneratedRegistry.Providers)
            {
                try
                {
                    IGeneratedManifest manifest = provider.CreateManifest();
                    bot.RegisterGeneratedHandlers(manifest);
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Warning, $"Failed to register generated manifest: {ex.Message}");
                }
            }

            return bot;
        }
    }

    /// <summary>
    /// Registers generated handlers and definitions emitted by the source generator.
    /// </summary>
    public void RegisterGeneratedHandlers(IGeneratedManifest manifest)
    {
        if (manifest is null) throw new ArgumentNullException(nameof(manifest));
        _generatedManifests.Add(manifest);
        _slashCommands.RegisterGeneratedManifest(manifest);
        foreach (var ch in manifest.Components)
            _components.RegisterGenerated(ch);
    }

    private async Task PreloadAsync(CancellationToken ct)
    {
        try
        {
            if (_preloadGuilds || _preloadChannels || _preloadMembers)
            {
                Guild[] guilds = await _rest.GetAsync<Guild[]>("/users/@me/guilds", ct).ConfigureAwait(false) ?? [];
                _cache.ReplaceGuilds(guilds);

                if (_preloadChannels)
                {
                    foreach (Guild g in guilds)
                    {
                        Channel[] ch = await GetGuildChannelsAsync(g.Id, ct).ConfigureAwait(false) ?? [];
                        _cache.SetChannels(g.Id, ch);
                    }
                }

                if (_preloadMembers)
                {
                    foreach (Guild g in guilds)
                    {
                        List<Member> all = new(1024);
                        string? after = null;
                        while (true)
                        {
                            Member[]? page = await ListGuildMembersAsync(g.Id, 1000, after, ct).ConfigureAwait(false);
                            if (page is null || page.Length == 0) break;
                            all.AddRange(page);
                            after = page[^1].User.Id;
                            if (page.Length < 1000) break;
                        }
                        _cache.SetMembers(g.Id, all);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Cache preload failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads complete guild data after GUILD_CREATE, ensuring all channels and members are cached.
    /// Fires GuildReady event when complete.
    /// </summary>
    private async Task LoadCompleteGuildDataAsync(string guildId, Channel[]? guildCreateChannels, Member[]? guildCreateMembers)
    {
        await Task.Delay(100); // Small delay to let connection stabilize

        bool needsMembers = false;
        bool needsChannels = false;

        // Check if we need to load channels via REST (if not provided in GUILD_CREATE)
        if (guildCreateChannels is null || guildCreateChannels.Length == 0)
        {
            _logger.Log(LogLevel.Debug, $"Loading channels for guild {guildId} via REST");
            try
            {
                Channel[] channels = await GetGuildChannelsAsync(guildId, _cts.Token).ConfigureAwait(false) ?? [];
                _cache.SetChannels(guildId, channels);
                needsChannels = true;
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Warning, $"Failed to load channels for guild {guildId}: {ex.Message}");
            }
        }

        // Check if we need to load full member list via gateway chunking
        if ((_intents & DiscordIntents.GuildMembers) == DiscordIntents.GuildMembers)
        {
            _logger.Log(LogLevel.Debug, $"Requesting full member list for guild {guildId}");
            try
            {
                // Register that we're waiting for member chunks
                _pendingMemberChunks.TryAdd(guildId, 1);
                await _gateway.RequestGuildMembersAsync(guildId);
                needsMembers = true;
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Warning, $"Failed to request members for guild {guildId}: {ex.Message}");
                _pendingMemberChunks.TryRemove(guildId, out _);
            }
        }

        // If we didn't need to load anything asynchronously, fire GuildReady immediately
        if (!needsMembers && !needsChannels)
        {
            if (_cache.TryGetGuild(guildId, out Guild guild))
            {
                _logger.Log(LogLevel.Information, $"Guild {guild.Name} ({guildId}) is fully loaded");
                DiscordEvents.RaiseGuildReady(this, new GuildEvent { Guild = guild });
            }
        }
        else if (!needsMembers)
        {
            // We loaded channels but don't need members (no GuildMembers intent)
            // Fire GuildReady after a small delay to ensure channels are cached
            await Task.Delay(50);
            if (_cache.TryGetGuild(guildId, out Guild guild))
            {
                _logger.Log(LogLevel.Information, $"Guild {guild.Name} ({guildId}) is fully loaded");
                DiscordEvents.RaiseGuildReady(this, new GuildEvent { Guild = guild });
            }
        }
        // If we need members, GuildReady will fire when the last chunk arrives (handled in GuildMembersChunk event)
    }
}
