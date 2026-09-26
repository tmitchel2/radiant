using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>One setting: its name and help on the left, its control on the right.</summary>
/// <param name="Label">The setting's name.</param>
/// <param name="Control">Its control (a switch, a select field…).</param>
public sealed record SettingsRow(string Label, Element? Control) : Component
{
    /// <summary>Help under the name.</summary>
    public string? Description { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context) => new Box
    {
        Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, Padding = Edges.Symmetric(20, 12), ColumnGap = 16 },
        Children =
        [
            new Box
            {
                Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1, RowGap = 2 },
                Children =
                [
                    new SurfaceText(Label) { TextType = TextType.BodyLarge },
                    Description is null ? null : new SurfaceText(Description) { Legibility = Legibility.Medium },
                ],
            },
            AccessibleNames.Name(Control, Label),
        ],
    };
}
