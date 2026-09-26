using System;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// The screen's main action, floating: a 56 px rounded square with an icon on the primary
/// container, raised, or extended with a label. Place it (bottom right, usually) with
/// <see cref="Layout"/>.
/// </summary>
/// <param name="Icon">The action's icon.</param>
/// <param name="Label">What it does: shown when <see cref="Extended"/>, and always to assistive technology.</param>
[RequiresTestId]
public sealed record Fab(string Icon, string Label) : Component
{
    /// <summary>Whether the label shows beside the icon.</summary>
    public bool Extended { get; init; }

    /// <summary>The action.</summary>
    public Action? OnPress { get; init; }

    /// <summary>The colour family; primary by default.</summary>
    public SurfaceName Color { get; init; } = SurfaceName.Primary;

    /// <summary>Where it floats.</summary>
    public LayoutStyle? Layout { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context) => new PressableSurface
    {
        SurfaceColor = Color,
        SurfaceContainerToggle = true,
        CornerShape = CornerShapeRole.Large,
        Elevation = ElevationLevel.Level3,
        Label = Label,
        OnPress = OnPress,
        Layout = new LayoutStyle
        {
            FlexDirection = FlexDirection.Row,
            AlignItems = Align.Center,
            JustifyContent = Justify.Center,
            Height = 56,
            MinWidth = 56,
            Padding = Extended ? new Edges(16, 0, 20, 0) : Edges.None,
            ColumnGap = 12,
            AlignSelf = Align.FlexStart,
        }.Merge(Layout ?? default),
        Children =
        [
            new SurfaceIcon(Icon),
            Extended ? new SurfaceText(Label) { TextType = TextType.LabelLarge } : null,
        ],
    };
}
