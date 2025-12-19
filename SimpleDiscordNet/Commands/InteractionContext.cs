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

    /// <summary>
    /// The shard ID that received this interaction (0-based).
    /// Null if bot is not using sharding.
    /// Example: int? shard = ctx.ShardId;
    /// </summary>
    public int? ShardId { get; internal set; }

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
    /// Sends an immediate response to the interaction with just text.
    /// Example: await ctx.RespondAsync("Hello, world!");
    /// </summary>
    public Task RespondAsync(string content, EmbedBuilder? embed = null, bool ephemeral = false, CancellationToken ct = default)
    {
        // If we already deferred the interaction, Discord requires using the follow-up webhook endpoint
        if (_deferred || _deferredUpdate)
        {
            return FollowupAsync(content, embed, ephemeral, ct);
        }

        InteractionResponseData data = new()
        {
            content = content,
            embeds = embed is null ? null : [embed.ToModel()],
            flags = ephemeral ? 1 << 6 : null // EPHEMERAL flag
        };
        InteractionResponse resp = new() { type = 4, data = data }; // CHANNEL_MESSAGE_WITH_SOURCE
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
                embeds = embed is null ? null : [embed.ToModel()],
                flags = ephemeral ? 1 << 6 : null,
                components = [new ActionRow(components.Cast<object>().ToArray())]
            };
            return _rest.PostWebhookFollowupAsync(ApplicationId, InteractionToken, payload, ct);
        }

        InteractionResponseData data = new()
        {
            content = content,
            embeds = embed is null ? null : [embed.ToModel()],
            flags = ephemeral ? 1 << 6 : null,
            components = [new ActionRow(components.Cast<object>().ToArray())]
        };
        InteractionResponse resp = new() { type = 4, data = data };
        return _rest.PostInteractionCallbackAsync(InteractionId, InteractionToken, resp, ct);
    }

    /// <summary>
    /// Defers the interaction response to allow more processing time (type 5).
    /// Use this for slash commands when you need longer than 3 seconds before sending a message.
    /// Prefer calling this explicitly or annotating the handler with [Defer] if you want the SDK to do it automatically.
    /// </summary>
    public Task DeferAsync(bool ephemeral = false, CancellationToken ct = default)
    {
        InteractionResponseData data = new InteractionResponseData { flags = ephemeral ? 1 << 6 : null };
        InteractionResponse resp = new() { type = 5, data = data }; // DEFERRED_CHANNEL_MESSAGE_WITH_SOURCE
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
        WebhookMessageRequest payload = new()
        {
            content = content,
            embeds = embed is null ? null : [embed.ToModel()],
            flags = ephemeral ? 1 << 6 : null
        };
        return _rest.PostWebhookFollowupAsync(ApplicationId, InteractionToken, payload, ct);
    }

    /// <summary>
    /// Defers a component interaction update (responds with a loading state on the message).
    /// </summary>
    public Task DeferUpdateAsync(CancellationToken ct = default)
    {
        InteractionResponse resp = new() { type = 6, data = null }; // DEFERRED_UPDATE_MESSAGE
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
            WebhookMessageRequest payload = new() { content = content, components = (object[]?)comps };
            return _rest.PatchWebhookOriginalAsync(ApplicationId, InteractionToken, payload, ct);
        }

        InteractionResponseData data = new() { content = content, components = (object[]?)comps };
        InteractionResponse resp = new() { type = 7, data = data }; // UPDATE_MESSAGE
        return _rest.PostInteractionCallbackAsync(InteractionId, InteractionToken, resp, ct);
    }

    /// <summary>
    /// Opens a modal in response to an interaction.
    /// This must be the initial response. Do not defer before opening a modal.
    /// Example: await ctx.OpenModalAsync("modal_id", "Form Title", new ActionRow(new TextInput("input_id", "Label")));
    /// </summary>
    public Task OpenModalAsync(string customId, string title, params object[] actionRows)
    {
        if (_deferred || _deferredUpdate)
        {
            throw new InvalidOperationException("Cannot open a modal after the interaction has been deferred. Do not apply [Defer] to this handler and avoid calling ctx.DeferResponseAsync before opening the modal.");
        }
        OpenModalRequest modal = new()
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

    /// <summary>
    /// Sends a simple text-only response.
    /// Example: await ctx.ReplyAsync("Done!");
    /// </summary>
    public Task ReplyAsync(string content, bool ephemeral = false, CancellationToken ct = default)
        => RespondAsync(content, null, ephemeral, ct);

    /// <summary>
    /// Sends an ephemeral (only visible to user) response.
    /// Example: await ctx.ReplyEphemeralAsync("This is private!");
    /// </summary>
    public Task ReplyEphemeralAsync(string content, EmbedBuilder? embed = null, CancellationToken ct = default)
        => RespondAsync(content, embed, ephemeral: true, ct);

    /// <summary>
    /// Sends a response with an embed.
    /// Example: await ctx.ReplyWithEmbedAsync("Check this out", new EmbedBuilder().WithTitle("Cool Embed"));
    /// </summary>
    public Task ReplyWithEmbedAsync(string content, EmbedBuilder embed, bool ephemeral = false, CancellationToken ct = default)
        => RespondAsync(content, embed, ephemeral, ct);

    /// <summary>
    /// Sends a response with buttons.
    /// Example: await ctx.ReplyWithButtonsAsync("Choose:", new Button ("Yes", "yes_id"), new Button("No", "no_id"));
    /// </summary>
    public Task ReplyWithButtonsAsync(string content, params Button[] buttons)
        => RespondAsync(content, buttons, null, false, CancellationToken.None);

    /// <summary>
    /// Gets an option value as a string from a slash command.
    /// Returns null if the option doesn't exist.
    /// Example: string? name = ctx.GetOption("name");
    /// </summary>
    public string? GetOption(string optionName)
    {
        InteractionOption? opt = Command?.Options?.FirstOrDefault(o => o.Name.Equals(optionName, StringComparison.OrdinalIgnoreCase));
        return opt?.String;
    }

    /// <summary>
    /// Gets an option value as an integer from a slash command.
    /// Returns null if the option doesn't exist.
    /// Example: long? count = ctx.GetOptionInt("count");
    /// </summary>
    public long? GetOptionInt(string optionName)
    {
        InteractionOption? opt = Command?.Options?.FirstOrDefault(o => o.Name.Equals(optionName, StringComparison.OrdinalIgnoreCase));
        return opt?.Integer;
    }

    /// <summary>
    /// Gets an option value as a boolean from a slash command.
    /// Returns null if the option doesn't exist.
    /// Example: bool? enabled = ctx.GetOptionBool("enabled");
    /// </summary>
    public bool? GetOptionBool(string optionName)
    {
        InteractionOption? opt = Command?.Options?.FirstOrDefault(o => o.Name.Equals(optionName, StringComparison.OrdinalIgnoreCase));
        return opt?.Boolean;
    }

    /// <summary>
    /// Gets an option value as a string, or returns a default value if not found.
    /// Example: string name = ctx.GetOptionOrDefault("name", "Anonymous");
    /// </summary>
    public string GetOptionOrDefault(string optionName, string defaultValue)
        => GetOption(optionName) ?? defaultValue;

    /// <summary>
    /// Gets the first selected value from a select menu interaction.
    /// Returns null if no values selected.
    /// Example: string? choice = ctx.GetSelectedValue();
    /// </summary>
    public string? GetSelectedValue()
        => Component?.Values?.FirstOrDefault();

    /// <summary>
    /// Gets the value from a modal text input by custom_id.
    /// Returns null if not found.
    /// Example: string? feedback = ctx.GetModalValue("feedback_input");
    /// </summary>
    public string? GetModalValue(string customId)
    {
        TextInputValue? input = Modal?.Inputs?.FirstOrDefault(i => i.CustomId.Equals(customId, StringComparison.OrdinalIgnoreCase));
        return input?.Value;
    }

    /// <summary>
    /// Returns true if this interaction is from a guild (server), false if from DMs.
    /// Example: if (ctx.IsInGuild) { ... }
    /// </summary>
    public bool IsInGuild => GuildId is not null;

    /// <summary>
    /// Gets the user's ID who triggered this interaction.
    /// Example: string userId = ctx.UserId;
    /// </summary>
    public string UserId => User?.Id ?? string.Empty;

    /// <summary>
    /// Gets the username who triggered this interaction.
    /// Example: string username = ctx.Username;
    /// </summary>
    public string Username => User?.Username ?? "Unknown";
}
