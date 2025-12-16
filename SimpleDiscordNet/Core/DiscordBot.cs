using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using SimpleDiscordNet.Context;
using SimpleDiscordNet.Core;
using SimpleDiscordNet.Commands;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Gateway;
using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Models;
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

    private readonly bool _developmentMode;
    private readonly string[] _developmentGuildIds;

    private readonly ConcurrentDictionary<string, object> _services = new();

    private volatile bool _started;

    private DiscordBot(
        string token,
        DiscordIntents intents,
        JsonSerializerOptions json,
        NativeLogger logger,
        TimeProvider? timeProvider,
        bool preloadGuilds,
        bool preloadChannels,
        bool preloadMembers,
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
        _developmentMode = developmentMode;
        _developmentGuildIds = developmentGuildIds?.Where(static s => !string.IsNullOrWhiteSpace(s))
                                                  .Select(static s => s.Trim())
                                                  .Distinct(StringComparer.Ordinal)
                                                  .ToArray() ?? [];

        HttpClient httpClient = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
            EnableMultipleHttp2Connections = true,
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            ConnectTimeout = TimeSpan.FromSeconds(20)
        })
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        RouteRateLimiter rateLimiter = new (timeProvider ?? TimeProvider.System);
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
        DiscordContext.SetProvider(_cache.SnapshotGuilds, _cache.SnapshotChannels, _cache.SnapshotMembers, _cache.SnapshotUsers);

        // Optionally preload caches using REST (runs in background)
        if (_preloadGuilds || _preloadChannels || _preloadMembers)
        {
            _ = Task.Run(() => PreloadAsync(_cts.Token), cancellationToken);
        }

        // Auto-register slash commands discovered via reflection before any potential sync
        try
        {
            AutoRegisterSlashCommands();
            AutoRegisterComponentHandlers();
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Auto-registration of handlers failed: {ex.Message}", ex);
        }

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

    /// <summary>
    /// Scans loaded assemblies for classes containing methods marked with <see cref="SlashCommandAttribute"/>
    /// and automatically registers them. Classes must be non-abstract with a public parameterless constructor.
    /// </summary>
    private void AutoRegisterSlashCommands()
    {
        // Gather candidate types: non-abstract classes that contain at least one method with SlashCommandAttribute
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        int registeredTypes = 0;
        foreach (Assembly assembly in assemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException typeLoadEx)
            {
                types = typeLoadEx.Types.Where(t => t is not null).Cast<Type>().ToArray();
            }
            catch
            {
                continue; // skip assemblies we cannot inspect
            }

            foreach (Type type in types)
            {
                if (!type.IsClass || type.IsAbstract) continue;

                // Quick check: any instance method has a SlashCommandAttribute
                MethodInfo? anySlash = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.GetCustomAttribute<SlashCommandAttribute>() is not null);
                if (anySlash is null) continue;

                // Require public parameterless ctor
                ConstructorInfo? publicParameterlessCtor = type.GetConstructor(Type.EmptyTypes);
                if (publicParameterlessCtor is null || !publicParameterlessCtor.IsPublic)
                {
                    _logger.Log(LogLevel.Warning, $"Type '{type.FullName}' has slash commands but no public parameterless constructor. Skipping.");
                    continue;
                }

                try
                {
                    object? instance = Activator.CreateInstance(type);
                    if (instance is null)
                    {
                        _logger.Log(LogLevel.Warning, $"Failed to create instance of '{type.FullName}'. Skipping.");
                        continue;
                    }
                    _slashCommands.Register(instance);
                    registeredTypes++;
                }
                catch (Exception exception)
                {
                    _logger.Log(LogLevel.Warning, $"Error instantiating '{type.FullName}': {exception.Message}");
                }
            }
        }

        if (registeredTypes > 0)
        {
            _logger.Log(LogLevel.Information, $"Auto-registered slash command handlers from {registeredTypes} type(s).");
        }
    }

    /// <summary>
    /// Synchronizes all registered slash commands to the specified guild ids.
    /// </summary>
    public async Task SyncSlashCommandsAsync(IEnumerable<string> guildIds, CancellationToken ct = default)
    {
        if (guildIds is null) throw new ArgumentNullException(nameof(guildIds));
        ApplicationInfo app = await _rest.GetApplicationAsync(ct).ConfigureAwait(false)
                  ?? throw new InvalidOperationException("Failed to fetch application info");
        object[] defs = _slashCommands.BuildCommandDefinitions();
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
        object payload = new
        {
            content,
            embeds = embed is null ? null : new[] { embed.ToModel() }
        };
        return _rest.PostAsync($"/channels/{channelId}/messages", payload, ct);
    }

    /// <summary>
    /// Sends a message with a single attachment.
    /// </summary>
    public Task SendAttachmentAsync(string channelId, string content, string fileName, ReadOnlyMemory<byte> data, EmbedBuilder? embed = null, CancellationToken ct = default)
    {
        object payload = new
        {
            content,
            embeds = embed is null ? null : new[] { embed.ToModel() },
            attachments = new[] { new { id = 0, filename = fileName } }
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

    private void OnInteractionCreate(object? sender, InteractionCreateEvent e)
    {
        if (e.Type == InteractionType.ApplicationCommand && e.Data is not null)
        {
            _ = _slashCommands.HandleAsync(e, _rest, _cts.Token);
        }
        else if ((e.Type == InteractionType.MessageComponent && e.Component is not null)
              || (e.Type == InteractionType.ModalSubmit && e.Modal is not null))
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
        _gateway.GuildCreate += (_, g) =>
        {
            _cache.UpsertGuild(g);
            DiscordEvents.RaiseGuildAdded(this, new GuildEvent { Guild = g });
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
    }

    /// <summary>
    /// Scans loaded assemblies for classes containing methods marked with <see cref="ComponentHandlerAttribute"/>
    /// and automatically registers them. Classes must be non-abstract with a public parameterless constructor.
    /// </summary>
    private void AutoRegisterComponentHandlers()
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        int registeredTypes = 0;
        foreach (Assembly assembly in assemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException typeLoadEx)
            {
                types = typeLoadEx.Types.Where(t => t is not null).Cast<Type>().ToArray();
            }
            catch
            {
                continue;
            }

            foreach (Type type in types)
            {
                if (!type.IsClass || type.IsAbstract) continue;

                MethodInfo? any = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.GetCustomAttribute<ComponentHandlerAttribute>() is not null);
                if (any is null) continue;

                ConstructorInfo? ctor = type.GetConstructor(Type.EmptyTypes);
                if (ctor is null || !ctor.IsPublic)
                {
                    _logger.Log(LogLevel.Warning, $"Type '{type.FullName}' has component handlers but no public parameterless constructor. Skipping.");
                    continue;
                }

                try
                {
                    object? instance = Activator.CreateInstance(type);
                    if (instance is null)
                    {
                        _logger.Log(LogLevel.Warning, $"Failed to create instance of '{type.FullName}'. Skipping.");
                        continue;
                    }
                    _components.Register(instance);
                    registeredTypes++;
                }
                catch (Exception exception)
                {
                    _logger.Log(LogLevel.Warning, $"Error instantiating '{type.FullName}': {exception.Message}");
                }
            }
        }

        if (registeredTypes > 0)
        {
            _logger.Log(LogLevel.Information, $"Auto-registered component handlers from {registeredTypes} type(s).");
        }
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
            options.DevelopmentMode,
            options.DevelopmentGuildIds);

        return bot;
    }

    public sealed class Builder
    {
        private string? _token;
        private DiscordIntents _intents = DiscordIntents.Guilds | DiscordIntents.GuildMessages | DiscordIntents.DirectMessages | DiscordIntents.MessageContent;
        private JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };
        private NativeLogger _logger = new();
        private TimeProvider? _timeProvider;
        private bool _preloadGuilds;
        private bool _preloadChannels;
        private bool _preloadMembers;
        private bool _developmentMode;
        private readonly List<string> _developmentGuildIds = new();

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
        /// Builds a configured <see cref="DiscordBot"/> instance.
        /// </summary>
        public DiscordBot Build()
        {
            return string.IsNullOrWhiteSpace(_token) 
                ? throw new InvalidOperationException("Token is required") 
                : new DiscordBot(_token!, _intents, _json, _logger, _timeProvider, _preloadGuilds, _preloadChannels, _preloadMembers, _developmentMode, _developmentGuildIds);
        }
    }

    private async Task PreloadAsync(CancellationToken ct)
    {
        try
        {
            if (_preloadGuilds || _preloadChannels || _preloadMembers)
            {
                Guild[] guilds = await _rest.GetAsync<Guild[]>("/users/@me/guilds", ct).ConfigureAwait(false) ?? Array.Empty<Guild>();
                _cache.ReplaceGuilds(guilds);

                if (_preloadChannels)
                {
                    foreach (Guild g in guilds)
                    {
                        Channel[] ch = await GetGuildChannelsAsync(g.Id, ct).ConfigureAwait(false) ?? Array.Empty<Channel>();
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
}
