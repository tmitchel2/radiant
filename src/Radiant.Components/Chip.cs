using System;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A compact choice or action. Plain, it's an assist chip; give it <see cref="Selected"/> for a
/// filter chip (a tick and a tonal fill when chosen); give it <see cref="OnRemove"/> for an input
/// chip (a trailing ×).
/// </summary>
/// <param name="Label">What it says.</param>
public sealed record Chip(string Label) : Component
{
    /// <summary>A leading icon.</summary>
    public string? Icon { get; init; }

    /// <summary>For filter chips: whether chosen. Null for other chips.</summary>
    public bool? Selected { get; init; }

    /// <summary>What pressing it does.</summary>
    public Action? OnPress { get; init; }

    /// <summary>For input chips: what the trailing × does.</summary>
    public Action? OnRemove { get; init; }

    /// <summary>Whether it's raised (on busy backgrounds) instead of outlined.</summary>
    public bool Elevated { get; init; }

    /// <summary>Whether it can't be used.</summary>
    public bool Disabled { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var selected = Selected == true;
        var icon = selected ? "check" : Icon;
        var remove = OnRemove;
        return new PressableSurface
        {
            SurfaceColor = selected ? SurfaceName.Secondary : Elevated ? SurfaceName.SurfaceContainerLow : null,
            SurfaceContainerToggle = selected ? true : null,
            ShowOutline = !selected && !Elevated,
            OutlineVariant = true,
            Elevation = Elevated ? ElevationLevel.Level1 : null,
            CornerShape = CornerShapeRole.Small,
            ShowDisabled = Disabled ? true : null,
            OnPress = OnPress,
            Role = Selected is null ? SemanticsRole.Button : SemanticsRole.CheckBox,
            // A filter chip is a check box, heard as checked or not.
            Checked = Selected is null ? null : selected,
            Layout = new LayoutStyle
            {
                FlexDirection = FlexDirection.Row,
                AlignItems = Align.Center,
                Height = 32,
                Padding = new Edges(icon is null ? 16 : 8, 0, remove is null ? 16 : 8, 0),
                ColumnGap = 8,
            },
            Children =
            [
                icon is null ? null : new SurfaceIcon(icon) { IconSize = 18, Legibility = selected ? null : Legibility.Medium },
                new SurfaceText(Label) { TextType = TextType.LabelLarge },
                remove is null ? null : new Box
                {
                    OnClick = e =>
                    {
                        remove();
                        e.Handled = true;
                    },
                    Children = [new SurfaceIcon("close") { IconSize = 18, Legibility = Legibility.Medium }],
                },
            ],
        };
    }
}
