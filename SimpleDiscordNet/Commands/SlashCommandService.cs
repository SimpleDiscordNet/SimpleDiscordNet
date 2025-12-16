using System.Reflection;
using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Rest;

namespace SimpleDiscordNet.Commands;

internal sealed class SlashCommandService
{
    private readonly NativeLogger _logger;

    // groupName (null for ungrouped), commandName, description, instance, method, hasContext, groupDescription
    private readonly List<(string? group, string name, string? description, object instance, MethodInfo method, bool hasContext, string? groupDescription)> _commands = new();

    public SlashCommandService(NativeLogger logger)
    {
        _logger = logger;
    }

    public void Register(object instance)
    {
        Type type = instance.GetType();
        SlashCommandGroupAttribute? groupAttr = type.GetCustomAttribute<SlashCommandGroupAttribute>();
        string? groupName = groupAttr is null ? null : NormalizeName(groupAttr.Name, "group");
        string? groupDesc = groupAttr?.Description;

        foreach (MethodInfo m in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            SlashCommandAttribute? attr = m.GetCustomAttribute<SlashCommandAttribute>();
            if (attr is null) continue;

            string name = NormalizeName(attr.Name, "command");
            ValidateDescription(attr.Description, groupName is null ? $"/{name}" : $"/{groupName} {name}");
            string desc = string.IsNullOrWhiteSpace(attr.Description) ? "command" : attr.Description;

            ParameterInfo[] parameters = m.GetParameters();
            bool hasCtx = parameters.Length > 0 && parameters[0].ParameterType == typeof(InteractionContext);
            _commands.Add((groupName, name, desc, instance, m, hasCtx, groupDesc));
        }

        _logger.Log(LogLevel.Information, $"Registered slash commands from {type.Name}");
    }

    public object[] BuildCommandDefinitions()
    {
        // Validate duplicates and build compliant definitions
        List<string> errors = new List<string>();

        // Validate ungrouped duplicates
        System.Collections.Generic.List<(string? group, string name, string? description, object instance, MethodInfo method, bool hasContext, string? groupDescription)> ungrouped = _commands.Where(c => c.group is null).ToList();
        System.Collections.Generic.List<System.Linq.IGrouping<string, (string? group, string name, string? description, object instance, MethodInfo method, bool hasContext, string? groupDescription)>> dupUngrouped = ungrouped.GroupBy(c => c.name, StringComparer.Ordinal).Where(g => g.Count() > 1).ToList();
        foreach (System.Linq.IGrouping<string, (string? group, string name, string? description, object instance, MethodInfo method, bool hasContext, string? groupDescription)> g in dupUngrouped)
            errors.Add($"Duplicate slash command name '{g.Key}' among ungrouped commands.");

        // Validate grouped duplicates (within group)
        System.Collections.Generic.IEnumerable<System.Linq.IGrouping<string, (string? group, string name, string? description, object instance, MethodInfo method, bool hasContext, string? groupDescription)>> grouped = _commands.Where(c => c.group is not null).GroupBy(c => c.group!, StringComparer.Ordinal);
        foreach (System.Linq.IGrouping<string, (string? group, string name, string? description, object instance, MethodInfo method, bool hasContext, string? groupDescription)> grp in grouped)
        {
            System.Collections.Generic.IEnumerable<System.Linq.IGrouping<string, (string? group, string name, string? description, object instance, MethodInfo method, bool hasContext, string? groupDescription)>> dups = grp.GroupBy(c => c.name, StringComparer.Ordinal).Where(x => x.Count() > 1);
            foreach (System.Linq.IGrouping<string, (string? group, string name, string? description, object instance, MethodInfo method, bool hasContext, string? groupDescription)> d in dups)
                errors.Add($"Duplicate subcommand '{d.Key}' in group '{grp.Key}'.");
        }

        // Validate top-level name conflicts between group names and ungrouped names
        System.Collections.Generic.HashSet<string> groupNames = grouped.Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        foreach ((string? group, string name, string? description, object instance, MethodInfo method, bool hasContext, string? groupDescription) cmd in ungrouped)
        {
            if (groupNames.Contains(cmd.name))
                errors.Add($"Name conflict: ungrouped command '{cmd.name}' conflicts with group '{cmd.name}'.");
        }

        if (errors.Count > 0)
        {
            // Throw to surface clear configuration errors during sync/registration
            throw new InvalidOperationException(string.Join("\n", errors));
        }

        // Build definitions
        System.Collections.Generic.List<object> defs = new System.Collections.Generic.List<object>(capacity: _commands.Count);

        // Ungrouped top-level commands
        foreach (System.Linq.IGrouping<(string name, string? description), (string? group, string name, string? description, object instance, MethodInfo method, bool hasContext, string? groupDescription)> c in ungrouped.GroupBy(c => (c.name, c.description), new NameDescComparer()))
        {
            defs.Add(new
            {
                name = c.Key.name,
                type = 1, // CHAT_INPUT
                description = string.IsNullOrWhiteSpace(c.Key.description) ? "command" : c.Key.description
            });
        }

        // Grouped top-level command with subcommands
        foreach (System.Linq.IGrouping<string, (string? group, string name, string? description, object instance, MethodInfo method, bool hasContext, string? groupDescription)> grp in grouped)
        {
            string groupDescription = grp.Select(x => x.groupDescription).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d)) ?? "group";
            object[] subs = grp
                .GroupBy(x => (x.name, x.description), new NameDescComparer())
                .Select(sc => new {
                    type = 1, // SUB_COMMAND
                    name = sc.Key.name,
                    description = string.IsNullOrWhiteSpace(sc.Key.description) ? "command" : sc.Key.description
                })
                .Cast<object>()
                .ToArray();

            defs.Add(new
            {
                name = grp.Key,
                type = 1, // CHAT_INPUT
                description = groupDescription,
                options = subs
            });
        }
        object[] arr = defs.ToArray();

        // Debug log a concise summary of definitions
        try
        {
            System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>(arr.Length);
            foreach (System.Linq.IGrouping<string, (string? group, string name, string? description, object instance, MethodInfo method, bool hasContext, string? groupDescription)> g in grouped)
            {
                foreach ((string? group, string name, string? description, object instance, MethodInfo method, bool hasContext, string? groupDescription) sc in g)
                {
                    parts.Add($"/{g.Key} {sc.name}");
                }
            }
            foreach ((string? group, string name, string? description, object instance, MethodInfo method, bool hasContext, string? groupDescription) u in ungrouped)
            {
                parts.Add($"/{u.name}");
            }
            if (parts.Count > 0)
            {
                _logger.Log(LogLevel.Debug, $"Slash command defs: {string.Join(", ", parts)}");
            }
        }
        catch { }

        return arr;
    }

    public async Task HandleAsync(InteractionCreateEvent e, RestClient rest, CancellationToken ct)
    {
        string top = e.Data.Name;
        string? sub = e.Data.Subcommand;

        // Find matching handler by group/sub
        (string? group, string name, string? description, object instance, MethodInfo method, bool hasContext, string? groupDescription) match;
        if (!string.IsNullOrEmpty(sub))
        {
            // grouped
            match = _commands.FirstOrDefault(c => string.Equals(c.group, top, StringComparison.Ordinal) && string.Equals(c.name, sub, StringComparison.Ordinal));
        }
        else
        {
            // ungrouped
            match = _commands.FirstOrDefault(c => c.group is null && string.Equals(c.name, top, StringComparison.Ordinal));
        }

        if (match.method is null)
        {
            _logger.Log(LogLevel.Warning, $"No handler found for command '{top}'{(sub is null ? string.Empty : $"/{sub}")}");
            return;
        }

        try
        {
            InteractionContext ctx = new InteractionContext(rest, e);

            // Auto-defer by default unless the handler explicitly disables it
            bool autoDefer = match.method.GetCustomAttribute<AutoDeferAttribute>()?.Enabled ?? true;
            if (autoDefer)
            {
                await ctx.DeferAsync(ephemeral: false, ct).ConfigureAwait(false);
            }

            ParameterInfo[] parameters = match.method.GetParameters();
            object?[] args = match.hasContext ? new object?[parameters.Length] : Array.Empty<object?>();
            if (match.hasContext)
            {
                args[0] = ctx;
            }

            object? result = match.method.Invoke(match.instance, args);
            if (result is Task task)
            {
                await task.ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Error executing slash command '{top}'{(sub is null ? string.Empty : $"/{sub}")}: {ex.Message}", ex);
        }
    }

    private static string NormalizeName(string name, string kind)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException($"{kind} name is required", nameof(name));

        string s = name.Trim().ToLowerInvariant().Replace(' ', '-');
        // Validate allowed set and length
        foreach (char ch in s)
        {
            bool ok = (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '-' || ch == '_';
            if (!ok)
                throw new ArgumentException($"{kind} name '{name}' contains invalid character '{ch}'. Allowed: a-z, 0-9, -, _.");
        }
        if (s.Length < 1 || s.Length > 32)
            throw new ArgumentException($"{kind} name '{name}' must be between 1 and 32 characters after normalization.");
        return s;
    }

    private static void ValidateDescription(string? description, string scope)
    {
        if (description is null) return;
        if (description.Length == 0)
            throw new ArgumentException($"Description for {scope} must not be empty when provided.");
        if (description.Length > 100)
            throw new ArgumentException($"Description for {scope} must be 1-100 characters.");
    }

    private sealed class NameDescComparer : IEqualityComparer<(string name, string? description)>
    {
        public bool Equals((string name, string? description) x, (string name, string? description) y)
            => string.Equals(x.name, y.name, StringComparison.Ordinal)
               && string.Equals(x.description, y.description, StringComparison.Ordinal);

        public int GetHashCode((string name, string? description) obj)
            => HashCode.Combine(obj.name, obj.description);
    }
}
