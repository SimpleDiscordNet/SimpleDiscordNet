namespace SimpleDiscordNet.Commands;

/// <summary>
/// Marks a method as a handler for component or modal interactions.
/// Matches on <c>custom_id</c> exactly or by prefix depending on <see cref="Prefix"/>.
/// Methods may optionally take an <see cref="InteractionContext"/> as the first parameter.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ComponentHandlerAttribute : Attribute
{
    /// <summary>The custom_id to match (exact or as a prefix).</summary>
    public string CustomId { get; }

    /// <summary>When true, matches any interaction whose custom_id starts with <see cref="CustomId"/>.</summary>
    public bool Prefix { get; }

    public ComponentHandlerAttribute(string customId, bool prefix = false)
    {
        if (string.IsNullOrWhiteSpace(customId)) throw new ArgumentException("customId is required", nameof(customId));
        CustomId = customId;
        Prefix = prefix;
    }
}
