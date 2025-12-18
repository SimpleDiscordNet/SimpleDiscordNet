namespace SimpleDiscordNet.Commands;

/// <summary>
/// Forces the library to automatically defer the interaction before invoking the handler.
/// Apply to slash command methods and component/modal handler methods when you want the
/// SDK to send a deferred response for you.
///
/// Defaults without this attribute: no automatic deferral. You can manually defer
/// using <see cref="InteractionContext.DeferResponseAsync(bool, System.Threading.CancellationToken)"/> (slash)
/// or <see cref="InteractionContext.DeferUpdateAsync(System.Threading.CancellationToken)"/> (components).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class DeferAttribute : Attribute
{
}
