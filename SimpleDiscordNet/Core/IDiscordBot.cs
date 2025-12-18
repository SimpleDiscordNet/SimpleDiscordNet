using System.Text.Json;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Models;

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

    // Convenience REST APIs
    /// <summary>
    /// Sends a simple text message to the specified channel.
    /// </summary>
    Task SendMessageAsync(string channelId, string content, EmbedBuilder? embed = null, CancellationToken ct = default);

    /// <summary>
    /// Sends a message with a single file attachment to the specified channel.
    /// </summary>
    Task SendAttachmentAsync(string channelId, string content, string fileName, ReadOnlyMemory<byte> data, EmbedBuilder? embed = null, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a guild by its id.
    /// </summary>
    Task<Guild?> GetGuildAsync(string guildId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all channels for a guild.
    /// </summary>
    Task<Channel[]?> GetGuildChannelsAsync(string guildId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all roles for a guild.
    /// </summary>
    Task<Role[]?> GetGuildRolesAsync(string guildId, CancellationToken ct = default);

    /// <summary>
    /// Lists members of a guild with pagination support.
    /// </summary>
    Task<Member[]?> ListGuildMembersAsync(string guildId, int limit = 1000, string? after = null, CancellationToken ct = default);
}
