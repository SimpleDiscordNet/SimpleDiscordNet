using System.Diagnostics.CodeAnalysis;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Primitives;
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
    /// Sends a message to the channel associated with this context using a MessageBuilder.
    /// </summary>
    public Task RespondAsync(MessageBuilder builder, CancellationToken ct = default)
    {
        return _rest.PostAsync($"/channels/{ChannelId}/messages", builder.Build(), ct);
    }

    /// <summary>
    /// Sends a message to the channel associated with this context.
    /// </summary>
    public Task SendMessageAsync(string content, EmbedBuilder? embed, CancellationToken ct)
    {
        MessagePayload payload = new()
        {
            content = content,
            embeds = embed is null ? null : new[] { embed.Build() }
        };
        return _rest.PostAsync($"/channels/{ChannelId}/messages", payload, ct);
    }
}
