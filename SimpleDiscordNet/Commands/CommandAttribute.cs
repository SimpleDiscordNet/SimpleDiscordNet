namespace SimpleDiscordNet.Commands;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class CommandAttribute : Attribute
{
    public string Name { get; }
    public string? Description { get; }
    public CommandAttribute(string name, string? description = null)
    {
        Name = name;
        Description = description;
    }
}
