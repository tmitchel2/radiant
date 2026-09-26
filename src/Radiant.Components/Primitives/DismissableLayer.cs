using System;
using Radiant.UI.Core;

namespace Radiant.Components.Primitives;

/// <summary>
/// Content that goes away when the user presses outside it or presses Escape inside it: the
/// behaviour behind menus, popovers and non-modal dialogs. Presses on content it shows through a
/// portal count as inside.
/// </summary>
/// <param name="Child">The content.</param>
/// <param name="OnDismiss">Called on an outside press or Escape.</param>
public sealed record DismissableLayer(Element? Child, Action OnDismiss) : Component
{
    /// <summary>Whether a press outside dismisses (true) or only Escape does.</summary>
    public bool DismissOnOutsidePress { get; init; } = true;

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var layer = context.UseRef(new ElementRef()).Value;
        var latest = context.UseRef(OnDismiss);
        latest.Value = OnDismiss;
        var root = context.Root;
        var outside = DismissOnOutsidePress;
        context.UseEffect(() => outside
            ? root.ObservePointerDown(press =>
            {
                if (!press.IsWithin(layer))
                {
                    latest.Value();
                }
            }).Dispose
            : null, outside);
        return new Box
        {
            Ref = layer,
            HitTestVisible = false,
            OnKeyDown = e =>
            {
                if (e.Key == KeyCode.Escape)
                {
                    e.Handled = true;
                    latest.Value();
                }
            },
            Children = [Child],
        };
    }
}
