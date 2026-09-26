using System;
using Radiant.UI.Core;

namespace Radiant.Components.Primitives;

/// <summary>
/// Keeps keyboard focus inside its content while shown (Tab cycles within it), moves focus into it
/// when it appears, and gives focus back to what had it when it goes: what a modal dialog or an
/// open menu needs.
/// </summary>
/// <param name="Child">The content.</param>
public sealed record FocusScope(Element? Child) : Component
{
    /// <summary>Whether Tab stays inside.</summary>
    public bool Trap { get; init; } = true;

    /// <summary>Whether the first focusable element inside takes focus when the scope appears.</summary>
    public bool AutoFocus { get; init; } = true;

    /// <summary>Whether focus returns to where it was when the scope goes.</summary>
    public bool RestoreFocus { get; init; } = true;

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var scope = context.UseRef(new ElementRef()).Value;
        var root = context.Root;
        var (autoFocus, restore) = (AutoFocus, RestoreFocus);
        context.UseEffect(() =>
        {
            var saved = root.SaveFocus();
            if (autoFocus)
            {
                root.FocusFirst(scope);
            }
            return restore ? saved.Restore : null;
        }, default(ValueTuple));
        return new Box { Ref = scope, TrapFocus = Trap, HitTestVisible = false, Children = [Child] };
    }
}
