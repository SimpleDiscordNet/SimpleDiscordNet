namespace SimpleDiscordNet.Commands;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CommandGroupAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
