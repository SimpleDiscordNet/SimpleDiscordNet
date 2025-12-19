using System.Collections.Generic;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Models.Requests;
using SimpleDiscordNet.Primitives;
using SimpleDiscordNet.Rest;

namespace SimpleDiscordNet.Commands;

public sealed class InteractionContext
{
    private readonly RestClient _rest;
    private readonly InteractionCreateEvent _evt;
    private bool _deferred;
    private bool _deferredUpdate;

    public string InteractionId { get; }
    public string InteractionToken { get; }
    public string ApplicationId { get; }
    public string? GuildId { get; }
    public string? ChannelId { get; }
    public Author? User { get; }
    public InteractionType Type { get; }

    // Expose the raw event for maximum flexibility
    public InteractionCreateEvent Event => _evt;

    // Convenience accessors for specific interaction shapes
    public ApplicationCommandData? Command => _evt.Data;
    public MessageComponentData? Component => _evt.Component;
    public ModalSubmitData? Modal => _evt.Modal;

    // Common helpers
    public string? CustomId => Type switch
    {
        InteractionType.MessageComponent => _evt.Component?.CustomId,
        InteractionType.ModalSubmit => _evt.Modal?.CustomId,
        _ => null
    };

    public string? MessageId => _evt.Component?.MessageId;
    public IReadOnlyList<string> SelectedValues => _evt.Component?.Values ?? [];

    internal InteractionContext(RestClient rest, InteractionCreateEvent evt)
    {
        _rest = rest;
        _evt = evt;
        InteractionId = evt.Id;
        InteractionToken = evt.Token;
        ApplicationId = evt.ApplicationId;
        GuildId = evt.GuildId;
        ChannelId = evt.ChannelId;
        User = evt.Author;
        Type = evt.Type;
    }

    /// <summary>
    /// Sends an immediate response to the interaction.
    /// </summary>
    public Task RespondAsync(string content, EmbedBuilder? embed = null, bool ephemeral = false, CancellationToken ct = default)
    {
        // If we already deferred the interaction, Discord requires using the follow-up webhook endpoint
        if (_deferred || _deferredUpdate)
        {
            return FollowupAsync(content, embed, ephemeral, ct);
        }

        InteractionResponseData data = new InteractionResponseData
        {
            content = content,
            embeds = embed is null ? null : new[] { embed.ToModel() },
            flags = ephemeral ? 1 << 6 : (int?)null // EPHEMERAL flag
        };
        InteractionResponse resp = new InteractionResponse { type = 4, data = data }; // CHANNEL_MESSAGE_WITH_SOURCE
        return _rest.PostInteractionCallbackAsync(InteractionId, InteractionToken, resp, ct);
    }

    /// <summary>
    /// Sends an immediate response with components (buttons/selects). If already deferred, uses a follow-up.
    /// </summary>
    public Task RespondAsync(string content, IEnumerable<IComponent> components, EmbedBuilder? embed = null, bool ephemeral = false, CancellationToken ct = default)
    {
        if (_deferred || _deferredUpdate)
        {
            var payload = new WebhookMessageRequest
            {
                content = content,
                embeds = embed is null ? null : new[] { embed.ToModel() },
                flags = ephemeral ? 1 << 6 : (int?)null,
                components = new object[] { new ActionRow(components.Cast<object>().ToArray()) }
            };
            return _rest.PostWebhookFollowupAsync(ApplicationId, InteractionToken, payload, ct);
        }

        InteractionResponseData data = new InteractionResponseData
        {
            content = content,
            embeds = embed is null ? null : new[] { embed.ToModel() },
            flags = ephemeral ? 1 << 6 : (int?)null,
            components = new object[] { new ActionRow(components.Cast<object>().ToArray()) }
        };
        InteractionResponse resp = new InteractionResponse { type = 4, data = data };
        return _rest.PostInteractionCallbackAsync(InteractionId, InteractionToken, resp, ct);
    }

    /// <summary>
    /// Defers the interaction response to allow more processing time (type 5).
    /// Use this for slash commands when you need longer than 3 seconds before sending a message.
    /// Prefer calling this explicitly or annotating the handler with [Defer] if you want the SDK to do it automatically.
    /// </summary>
    public Task DeferAsync(bool ephemeral = false, CancellationToken ct = default)
    {
        InteractionResponseData data = new InteractionResponseData { flags = ephemeral ? 1 << 6 : (int?)null };
        InteractionResponse resp = new InteractionResponse { type = 5, data = data }; // DEFERRED_CHANNEL_MESSAGE_WITH_SOURCE
        Task task = _rest.PostInteractionCallbackAsync(InteractionId, InteractionToken, resp, ct);
        // Mark as deferred once the defer completes successfully
        return task.ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully)
            {
                _deferred = true;
            }
            // Propagate exceptions/cancellation
            t.GetAwaiter().GetResult();
        }, ct);
    }

    /// <summary>
    /// Alias for <see cref="DeferAsync(bool, System.Threading.CancellationToken)"/>.
    /// Provided for readability when working with slash commands.
    /// </summary>
    public Task DeferResponseAsync(bool ephemeral = false, CancellationToken ct = default)
        => DeferAsync(ephemeral, ct);

    /// <summary>
    /// Sends a follow-up message for a previously deferred interaction.
    /// </summary>
    public Task FollowupAsync(string content, EmbedBuilder? embed = null, bool ephemeral = false, CancellationToken ct = default)
    {
        var payload = new WebhookMessageRequest
        {
            content = content,
            embeds = embed is null ? null : new[] { embed.ToModel() },
            flags = ephemeral ? 1 << 6 : (int?)null
        };
        return _rest.PostWebhookFollowupAsync(ApplicationId, InteractionToken, payload, ct);
    }

    /// <summary>
    /// Defers a component interaction update (responds with a loading state on the message).
    /// </summary>
    public Task DeferUpdateAsync(CancellationToken ct = default)
    {
        InteractionResponse resp = new InteractionResponse { type = 6, data = null }; // DEFERRED_UPDATE_MESSAGE
        Task task = _rest.PostInteractionCallbackAsync(InteractionId, InteractionToken, resp, ct);
        return task.ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully)
            {
                _deferredUpdate = true;
            }
            t.GetAwaiter().GetResult();
        }, ct);
    }

    /// <summary>
    /// Updates the original message in response to a component interaction.
    /// </summary>
    public Task UpdateMessageAsync(string content, IEnumerable<IComponent>? components = null, CancellationToken ct = default)
    {
        object? comps = components is null ? null : new object[] { new ActionRow(components.Cast<object>().ToArray()) };

        if (_deferredUpdate)
        {
            // After a DEFERRED_UPDATE_MESSAGE, we must edit the original via webhook
            var payload = new WebhookMessageRequest
            {
                content = content,
                components = (object[]?)comps
            };
            return _rest.PatchWebhookOriginalAsync(ApplicationId, InteractionToken, payload, ct);
        }

        InteractionResponseData data = new InteractionResponseData
        {
            content = content,
            components = (object[]?)comps
        };
        InteractionResponse resp = new InteractionResponse { type = 7, data = data }; // UPDATE_MESSAGE
        return _rest.PostInteractionCallbackAsync(InteractionId, InteractionToken, resp, ct);
    }

    /// <summary>
    /// Opens a modal in response to an interaction.
    /// This must be the initial response. Do not defer before opening a modal.
    /// </summary>
    public Task OpenModalAsync(string customId, string title, params object[] actionRows)
    {
        if (_deferred || _deferredUpdate)
        {
            throw new InvalidOperationException("Cannot open a modal after the interaction has been deferred. Do not apply [Defer] to this handler and avoid calling ctx.DeferResponseAsync before opening the modal.");
        }
        var modal = new OpenModalRequest
        {
            type = 9,
            data = new ModalData
            {
                custom_id = customId,
                title = title,
                components = actionRows
            }
        };
        return _rest.PostInteractionCallbackAsync(InteractionId, InteractionToken, modal, CancellationToken.None);
    }
}
