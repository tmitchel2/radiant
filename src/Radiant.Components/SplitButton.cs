using System;
using System.Collections.Generic;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A button for the usual action with a joined arrow for its alternatives ("Save" with "Save as…"
/// and "Save all" under the arrow): the arrow drops a <see cref="Menu"/>.
/// </summary>
/// <param name="Text">The usual action's label.</param>
/// <param name="OnPress">The usual action.</param>
/// <param name="Alternatives">What the arrow offers.</param>
[RequiresTestId]
public sealed partial record SplitButton(string Text, Action? OnPress, IReadOnlyList<MenuItem> Alternatives) : Component
{
    [TestId<SurfaceButton>] public static partial string Main { get; }
    [TestId<IconButton>] public static partial string More { get; }

    /// <summary>An icon before the label.</summary>
    public string? Icon { get; init; }

    /// <summary>Filled or tonal (the arrow matches).</summary>
    public ButtonVariant Variant { get; init; } = ButtonVariant.Filled;

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var anchor = context.UseRef(new ElementRef()).Value;
        var open = context.UseState(false);
        // The arrow is as tall as the button beside it, and square.
        var theme = context.UseTheme();
        var height = theme.Theme.Components.Button.Height + theme.DensityOffset;
        return new Fragment(
            new Box
            {
                Ref = anchor,
                Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, ColumnGap = 2, AlignSelf = Align.FlexStart },
                Children =
                [
                    new SurfaceButton(Text, Variant) { TestId = Main, Icon = Icon, OnPress = OnPress },
                    new IconButton(open.Value ? "expand_less" : "expand_more", $"More {Text} options",
                        Variant == ButtonVariant.Tonal ? IconButtonVariant.Tonal : IconButtonVariant.Filled)
                    {
                        TestId = More,
                        OnPress = () => open.Set(true),
                        Layout = new LayoutStyle { Width = height, Height = height },
                    },
                ],
            },
            new Menu(anchor, open.Value, () => open.Set(false), Alternatives) { Align = Primitives.SideAlign.End });
    }
}
