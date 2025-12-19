namespace SimpleDiscordNet.Attributes;

/// <summary>
/// Marks a class or method as consuming ambient cached Discord data via SimpleDiscordNet.Context.DiscordContext.
/// Use this unified attribute instead of separate Guilds/Channels/Members/Users markers.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class DiscordContextAttribute : Attribute
{
}
