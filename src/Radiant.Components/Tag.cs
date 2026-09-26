using System;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A short label on something ("New", "Shipped", "Most popular"): a small tinted pill that
/// can't be pressed. For a choice or an action, use a <see cref="Chip"/>.
/// </summary>
/// <param name="Text">What it says.</param>
public sealed record Tag(string Text) : Component
{
    /// <summary>An icon before the text.</summary>
    public string? Icon { get; init; }

    /// <summary>The colour family it's tinted with (its container colour); secondary by default.</summary>
    public SurfaceName Color { get; init; } = SurfaceName.Secondary;

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var style = context.UseTheme().Theme.Components.Chip;
        // Outlined tags draw their family's colour on the surface they're on; a neutral one keeps the surface's own.
        var preset = style.OutlinedTags
            ? new Surface { ContentColor = Color == SurfaceName.Secondary ? null : Color, ShowOutline = true, OutlineVariant = true }
            : new Surface { SurfaceColor = Color, SurfaceContainerToggle = true };
        return preset with
        {
            CornerShape = style.TagShape,
            Semantics = new Semantics { Role = SemanticsRole.None, Label = Text },
            Layout = new LayoutStyle
            {
                FlexDirection = FlexDirection.Row,
                AlignItems = Align.Center,
                AlignSelf = Align.FlexStart,
                ColumnGap = 4,
                Height = style.TagHeight,
                Padding = new Edges(Icon is null ? 8 : 6, 0, 8, 0),
            },
            Children =
            [
                Icon is null ? null : new SurfaceIcon(Icon) { IconSize = 16 },
                new SurfaceText(Text) { TextType = TextType.LabelMedium, MaxLines = 1 },
            ],
        };
    }
}
