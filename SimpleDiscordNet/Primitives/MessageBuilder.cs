using SimpleDiscordNet.Primitives;

namespace SimpleDiscordNet;

/// <summary>
/// Simple builder for creating Discord messages with content, embeds, and components.
/// Example: var msg = new MessageBuilder().WithContent("Hello").WithEmbed(embed).Build();
/// </summary>
public sealed class MessageBuilder
{
    private string? _content;
    private EmbedBuilder? _embed;
    private List<EmbedBuilder>? _embeds;
    private List<IComponent>? _components;

    /// <summary>
    /// Sets the message text content.
    /// Example: builder.WithContent("Hello, world!");
    /// </summary>
    public MessageBuilder WithContent(string content)
    {
        _content = content;
        return this;
    }

    /// <summary>
    /// Adds an embed to the message.
    /// Example: builder.WithEmbed(new EmbedBuilder().WithTitle("Title"));
    /// </summary>
    public MessageBuilder WithEmbed(EmbedBuilder embed)
    {
        _embed = embed;
        return this;
    }

    /// <summary>
    /// Adds multiple embeds to the message (up to 10).
    /// Example: builder.WithEmbeds(embed1, embed2, embed3);
    /// </summary>
    public MessageBuilder WithEmbeds(params EmbedBuilder[] embeds)
    {
        _embeds ??= [];
        _embeds.AddRange(embeds);
        return this;
    }

    /// <summary>
    /// Adds a button to the message.
    /// Example: builder.WithButton("Click me", "button_id");
    /// </summary>
    public MessageBuilder WithButton(string label, string customId, int style = 1)
    {
        _components ??= [];
        _components.Add(new Button(label, customId, style));
        return this;
    }

    /// <summary>
    /// Adds a link button to the message.
    /// Example: builder.WithLinkButton("Visit Website", "https://example.com");
    /// </summary>
    public MessageBuilder WithLinkButton(string label, string url)
    {
        _components ??= [];
        _components.Add(new Button(label, url));
        return this;
    }

    /// <summary>
    /// Adds multiple buttons to the message.
    /// Example: builder.WithButtons(new Button("Yes", "yes"), new Button("No", "no"));
    /// </summary>
    public MessageBuilder WithButtons(params Button[] buttons)
    {
        _components ??= [];
        foreach (var btn in buttons)
            _components.Add(btn);
        return this;
    }

    /// <summary>
    /// Adds a string select menu to the message.
    /// Example: builder.WithSelect("select_id", new SelectOption("Label", "value"));
    /// </summary>
    public MessageBuilder WithSelect(string customId, params SelectOption[] options)
    {
        _components ??= [];
        _components.Add(new StringSelect(customId, options));
        return this;
    }

    /// <summary>
    /// Adds components to the message.
    /// </summary>
    public MessageBuilder WithComponents(params IComponent[] components)
    {
        _components ??= [];
        _components.AddRange(components);
        return this;
    }

    /// <summary>
    /// Builds the message payload for sending via the Discord API.
    /// </summary>
    internal object Build()
    {
        List<object> embedList = [];
        if (_embed is not null)
            embedList.Add(_embed.ToModel());
        if (_embeds is not null)
        {
            foreach (var e in _embeds)
                embedList.Add(e.ToModel());
        }

        object[]? components = null;
        if (_components is not null && _components.Count > 0)
        {
            var componentArray = new object[_components.Count];
            for (int i = 0; i < _components.Count; i++)
                componentArray[i] = _components[i];
            components = [new ActionRow(componentArray)];
        }

        return new
        {
            content = _content,
            embeds = embedList.Count > 0 ? embedList.ToArray() : null,
            components
        };
    }

    /// <summary>
    /// Gets the message content text.
    /// </summary>
    public string? Content => _content;

    /// <summary>
    /// Clears all message content.
    /// </summary>
    public MessageBuilder Clear()
    {
        _content = null;
        _embed = null;
        _embeds?.Clear();
        _components?.Clear();
        return this;
    }
}
