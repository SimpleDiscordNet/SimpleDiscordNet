namespace SimpleDiscordNet.Commands;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class CommandAttribute(string name, string? description = null) : Attribute
{
    public string Name { get; } = name;
    public string? Description { get; } = description;
}
