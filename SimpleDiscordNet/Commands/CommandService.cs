using System.Linq;
using System.Reflection;
using System.Text.Json;
using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Rest;

namespace SimpleDiscordNet.Commands;

internal sealed class CommandService
{
    private readonly NativeLogger _logger;
    private readonly List<(string name, object instance, MethodInfo method, bool hasContext)> _commands = new();

    public CommandService(NativeLogger logger) => _logger = logger;

    public void Register(object instance)
    {
        Type type = instance.GetType();
        CommandGroupAttribute? group = type.GetCustomAttribute<CommandGroupAttribute>();
        foreach (MethodInfo m in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            CommandAttribute? attr = m.GetCustomAttribute<CommandAttribute>();
            if (attr is null) continue;
            string name = group is null ? attr.Name : $"{group.Name} {attr.Name}";
            ParameterInfo[] pars = m.GetParameters();
            bool hasContext = pars.Length > 0 && pars[0].ParameterType == typeof(CommandContext);
            _commands.Add((name, instance, m, hasContext));
        }
        _logger.Log(LogLevel.Information, $"Registered commands from {type.Name}");
    }

    public async Task TryHandleAsync(MessageCreateEvent e, RestClient rest, JsonSerializerOptions json, CancellationToken ct)
    {
        // Simple prefix based command: bang '!'
        const char prefix = '!';
        string? content = e.Content?.Trim();
        if (string.IsNullOrEmpty(content) || content![0] != prefix) return;

        string[] parts = content.Substring(1).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return;
        string cmdName = parts[0];
        string[] args = parts.Skip(1).ToArray();

        (string name, object instance, MethodInfo method, bool hasContext) match = _commands.FirstOrDefault(c => string.Equals(c.name, cmdName, StringComparison.OrdinalIgnoreCase));
        if (match.method is null) return;

        try
        {
            CommandContext ctx = new CommandContext(e.ChannelId, e, rest);
            ParameterInfo[] parameters = match.method.GetParameters();
            object?[] invokeArgs = match.hasContext ? new object?[parameters.Length] : new object?[parameters.Length];
            int offset = 0;
            if (match.hasContext)
            {
                invokeArgs[0] = ctx;
                offset = 1;
            }

            for (int i = 0; i < parameters.Length - offset; i++)
            {
                ParameterInfo p = parameters[i + offset];
                if (p.ParameterType == typeof(string))
                {
                    invokeArgs[i + offset] = i < args.Length ? args[i] : string.Empty;
                }
                else if (p.ParameterType == typeof(int) && i < args.Length && int.TryParse(args[i], out int iv))
                {
                    invokeArgs[i + offset] = iv;
                }
                else
                {
                    invokeArgs[i + offset] = null;
                }
            }

            object? result = match.method.Invoke(match.instance, invokeArgs);
            if (result is Task t)
            {
                await t.ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Error executing command '{cmdName}': {ex.Message}", ex);
        }
    }
}
