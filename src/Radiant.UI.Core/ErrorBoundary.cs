using System;

namespace Radiant.UI.Core;

/// <summary>
/// Keeps a failure in part of the UI from taking the rest down: when building or updating
/// <paramref name="Child"/> throws, what was built of it goes and <paramref name="Fallback"/> shows
/// in its place, given the exception and an action that tries the child again. Only building and
/// updating are caught; an event handler or effect that throws still throws.
/// </summary>
/// <param name="Child">The content.</param>
/// <param name="Fallback">What shows instead once the content has thrown: the exception, and a retry.</param>
public sealed record ErrorBoundary(Element? Child, Func<Exception, Action, Element?> Fallback) : Component
{
    /// <summary>Called with what was caught, to log or report it.</summary>
    public Action<Exception>? OnError { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var node = context.Node;
        return node.Caught is { } error ? Fallback(error, () => context.Root.ResetBoundary(node)) : Child;
    }
}
