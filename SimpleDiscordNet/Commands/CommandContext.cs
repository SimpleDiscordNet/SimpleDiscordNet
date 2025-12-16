using System.Diagnostics.CodeAnalysis;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Rest;

namespace SimpleDiscordNet.Commands;

public sealed class CommandContext
{
    public required string ChannelId { get; init; }
    public required MessageCreateEvent Message { get; init; }

    private readonly RestClient _rest;

    [SetsRequiredMembers]
    internal CommandContext(string channelId, MessageCreateEvent message, RestClient rest)
    {
        ChannelId = channelId;
        Message = message;
        _rest = rest;
    }

    /// <summary>
    /// Sends a message to the channel associated with this context.
    /// </summary>
    public Task SendMessageAsync(string content, EmbedBuilder? embed, CancellationToken ct)
    {
        object payload = new
        {
            content,
            embeds = embed is null ? null : new[] { embed.ToModel() }
        };
        return _rest.PostAsync($"/channels/{ChannelId}/messages", payload, ct);
    }
}
