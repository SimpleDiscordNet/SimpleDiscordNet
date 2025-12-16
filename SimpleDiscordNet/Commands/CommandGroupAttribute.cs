namespace SimpleDiscordNet.Commands;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CommandGroupAttribute : Attribute
{
    public string Name { get; }
    public CommandGroupAttribute(string name) => Name = name;
}
