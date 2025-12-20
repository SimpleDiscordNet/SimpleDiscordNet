using System.Text.Json;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Primitives;

namespace SimpleDiscordNet;

/// <summary>
/// DI-friendly abstraction for interacting with the Discord bot.
/// Implemented by <see cref="DiscordBot"/>.
/// </summary>
public interface IDiscordBot : IAsyncDisposable, IDisposable
{
    // Lifecycle
    /// <summary>
    /// Starts the bot and begins processing events asynchronously.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronously starts the bot. Prefer <see cref="StartAsync"/> in async contexts.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops the bot and disposes underlying resources.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Synchronizes all registered slash commands to the specified guilds.
    /// </summary>
    Task SyncSlashCommandsAsync(IEnumerable<string> guildIds, CancellationToken ct = default);

    // Convenience REST APIs - Messaging
    /// <summary>
    /// Sends a simple text message to the specified channel.
    /// </summary>
    Task<DiscordMessage?> SendMessageAsync(string channelId, string content, EmbedBuilder? embed = null, CancellationToken ct = default);

    /// <summary>
    /// Sends a simple text message to the specified channel.
    /// </summary>
    Task<DiscordMessage?> SendMessageAsync(ulong channelId, string content, EmbedBuilder? embed = null, CancellationToken ct = default);

    /// <summary>
    /// Sends a simple text message to the specified channel.
    /// </summary>
    Task<DiscordMessage?> SendMessageAsync(DiscordChannel channel, string content, EmbedBuilder? embed = null, CancellationToken ct = default);

    /// <summary>
    /// Sends a message using a MessageBuilder to the specified channel.
    /// </summary>
    Task<DiscordMessage?> SendMessageAsync(string channelId, MessageBuilder builder, CancellationToken ct = default);

    /// <summary>
    /// Sends a message using a MessageBuilder to the specified channel.
    /// </summary>
    Task<DiscordMessage?> SendMessageAsync(ulong channelId, MessageBuilder builder, CancellationToken ct = default);

    /// <summary>
    /// Sends a message using a MessageBuilder to the specified channel.
    /// </summary>
    Task<DiscordMessage?> SendMessageAsync(DiscordChannel channel, MessageBuilder builder, CancellationToken ct = default);

    /// <summary>
    /// Sends a message with a single file attachment to the specified channel.
    /// </summary>
    Task<DiscordMessage?> SendAttachmentAsync(string channelId, string content, string fileName, ReadOnlyMemory<byte> data, EmbedBuilder? embed = null, CancellationToken ct = default);

    /// <summary>
    /// Sends a message with a single file attachment to the specified channel.
    /// </summary>
    Task<DiscordMessage?> SendAttachmentAsync(ulong channelId, string content, string fileName, ReadOnlyMemory<byte> data, EmbedBuilder? embed = null, CancellationToken ct = default);

    /// <summary>
    /// Sends a message with a single file attachment to the specified channel.
    /// </summary>
    Task<DiscordMessage?> SendAttachmentAsync(DiscordChannel channel, string content, string fileName, ReadOnlyMemory<byte> data, EmbedBuilder? embed = null, CancellationToken ct = default);

    // Convenience REST APIs - Retrieval
    /// <summary>
    /// Retrieves a guild by its id.
    /// </summary>
    Task<DiscordGuild?> GetGuildAsync(string guildId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a guild by its id.
    /// </summary>
    Task<DiscordGuild?> GetGuildAsync(ulong guildId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all channels for a guild.
    /// </summary>
    Task<DiscordChannel[]?> GetGuildChannelsAsync(string guildId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all channels for a guild.
    /// </summary>
    Task<DiscordChannel[]?> GetGuildChannelsAsync(ulong guildId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all channels for a guild.
    /// </summary>
    Task<DiscordChannel[]?> GetGuildChannelsAsync(DiscordGuild guild, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all roles for a guild.
    /// </summary>
    Task<DiscordRole[]?> GetGuildRolesAsync(string guildId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all roles for a guild.
    /// </summary>
    Task<DiscordRole[]?> GetGuildRolesAsync(ulong guildId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all roles for a guild.
    /// </summary>
    Task<DiscordRole[]?> GetGuildRolesAsync(DiscordGuild guild, CancellationToken ct = default);

    /// <summary>
    /// Lists members of a guild with pagination support.
    /// </summary>
    Task<DiscordMember[]?> ListGuildMembersAsync(string guildId, int limit = 1000, string? after = null, CancellationToken ct = default);

    /// <summary>
    /// Lists members of a guild with pagination support.
    /// </summary>
    Task<DiscordMember[]?> ListGuildMembersAsync(ulong guildId, int limit = 1000, string? after = null, CancellationToken ct = default);

    /// <summary>
    /// Lists members of a guild with pagination support.
    /// </summary>
    Task<DiscordMember[]?> ListGuildMembersAsync(DiscordGuild guild, int limit = 1000, string? after = null, CancellationToken ct = default);

    /// <summary>
    /// Adds a role to a guild member.
    /// </summary>
    Task AddRoleToMemberAsync(string guildId, string userId, string roleId, CancellationToken ct = default);

    /// <summary>
    /// Removes a role from a guild member.
    /// </summary>
    Task RemoveRoleFromMemberAsync(string guildId, string userId, string roleId, CancellationToken ct = default);

    /// <summary>
    /// Sends a direct message to a user by creating a DM channel and sending a message.
    /// </summary>
    Task<DiscordMessage?> SendDMAsync(string userId, string content, EmbedBuilder? embed = null, CancellationToken ct = default);

    /// <summary>
    /// Pins a message in a channel.
    /// </summary>
    Task PinMessageAsync(ulong channelId, ulong messageId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a message from a channel.
    /// </summary>
    Task DeleteMessageAsync(ulong channelId, ulong messageId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new channel in a guild.
    /// </summary>
    Task<DiscordChannel?> CreateChannelAsync(string guildId, string name, ChannelType type, string? parentId = null, object[]? permissionOverwrites = null, CancellationToken ct = default);

    /// <summary>
    /// Modifies a channel.
    /// </summary>
    Task<DiscordChannel?> ModifyChannelAsync(string channelId, string? name = null, int? type = null, string? parentId = null, int? position = null, string? topic = null, bool? nsfw = null, int? bitrate = null, int? userLimit = null, int? rateLimitPerUser = null, CancellationToken ct = default);

    /// <summary>
    /// Deletes a channel.
    /// </summary>
    Task DeleteChannelAsync(string channelId, CancellationToken ct = default);

    /// <summary>
    /// Sets or updates a channel permission overwrite for a role or member.
    /// </summary>
    Task SetChannelPermissionAsync(string channelId, string targetId, int type, ulong allow, ulong deny, CancellationToken ct = default);

    /// <summary>
    /// Deletes a channel permission overwrite.
    /// </summary>
    Task DeleteChannelPermissionAsync(string channelId, string overwriteId, CancellationToken ct = default);
}
