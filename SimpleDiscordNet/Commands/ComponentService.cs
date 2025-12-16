using System.Reflection;
using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Rest;

namespace SimpleDiscordNet.Commands;

internal sealed class ComponentService
{
    private readonly NativeLogger _logger;

    private readonly List<(string id, bool prefix, object instance, MethodInfo method, bool hasContext)> _handlers = new();

    public ComponentService(NativeLogger logger)
    {
        _logger = logger;
    }

    public void Register(object instance)
    {
        Type type = instance.GetType();
        foreach (MethodInfo m in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            foreach (ComponentHandlerAttribute attr in m.GetCustomAttributes<ComponentHandlerAttribute>())
            {
                ParameterInfo[] parameters = m.GetParameters();
                bool hasCtx = parameters.Length > 0 && parameters[0].ParameterType == typeof(InteractionContext);
                _handlers.Add((attr.CustomId, attr.Prefix, instance, m, hasCtx));
            }
        }

        if (_handlers.Any(h => h.instance == instance))
            _logger.Log(LogLevel.Information, $"Registered component handlers from {type.Name}");
    }

    public async Task HandleAsync(InteractionCreateEvent e, RestClient rest, CancellationToken ct)
    {
        // Determine custom_id based on interaction type
        string? customId = e.Type switch
        {
            InteractionType.MessageComponent => e.Component?.CustomId,
            InteractionType.ModalSubmit => e.Modal?.CustomId,
            _ => null
        };
        if (string.IsNullOrEmpty(customId))
            return;

        (string id, bool prefix, object instance, MethodInfo method, bool hasContext) match = _handlers.FirstOrDefault(h =>
            (!h.prefix && string.Equals(h.id, customId, StringComparison.Ordinal)) ||
            (h.prefix && customId.StartsWith(h.id, StringComparison.Ordinal)));

        if (match.method is null)
        {
            _logger.Log(LogLevel.Debug, $"No component handler found for custom_id '{customId}'.");
            return;
        }

        try
        {
            InteractionContext ctx = new InteractionContext(rest, e);

            // For message component clicks, defer update by default unless disabled via attribute
            bool autoDefer = match.method.GetCustomAttribute<AutoDeferAttribute>()?.Enabled ?? true;
            if (e.Type == InteractionType.MessageComponent && autoDefer)
            {
                await ctx.DeferUpdateAsync(ct).ConfigureAwait(false);
            }

            object?[] args = match.hasContext ? new object?[] { ctx } : Array.Empty<object?>();
            object? result = match.method.Invoke(match.instance, args);
            if (result is Task task)
            {
                await task.ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Error executing component handler for '{customId}': {ex.Message}", ex);
        }
    }
}
