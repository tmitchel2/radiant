using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>A property in an inspector: its name in a fixed-width column, then its control.</summary>
/// <param name="Label">The property's name.</param>
/// <param name="Control">What shows or edits it.</param>
public sealed record PropertyRow(string Label, Element? Control) : Component
{
    /// <summary>The name column's width.</summary>
    public float LabelWidth { get; init; } = 80f;

    /// <inheritdoc/>
    public override Element? Build(BuildContext context) => new Box
    {
        Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 8, MinHeight = 32 },
        Children =
        [
            new SurfaceText(Label) { Legibility = Legibility.Medium, MaxLines = 1, Layout = new LayoutStyle { Width = LabelWidth, FlexShrink = 0 } },
            new Box { Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1, AlignItems = Align.FlexStart }, Children = [Control] },
        ],
    };
}
