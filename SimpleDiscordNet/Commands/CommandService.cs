using System.Reflection;
using SimpleDiscordNet.Logging;

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
}
