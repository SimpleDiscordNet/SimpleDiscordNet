namespace SimpleDiscordNet.Commands;

/// <summary>
/// Controls whether the library should automatically defer an interaction before invoking the handler.
/// Apply to slash command methods and component/modal handler methods.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AutoDeferAttribute(bool enabled = true) : Attribute
{
    /// <summary>
    /// When true (default), the library will auto-defer before invoking the handler to prevent timeouts.
    /// When false, the library will NOT auto-defer; use this for handlers that immediately open a modal (type 9)
    /// or otherwise need to send the first response synchronously.
    /// </summary>
    public bool Enabled { get; } = enabled;
}
