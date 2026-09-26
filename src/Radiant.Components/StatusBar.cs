using System.Collections.Generic;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// The thin bar along the bottom of a desktop window: small items (usually
/// <see cref="StatusItem"/>s) at the start and at the end, on the container colour.
/// </summary>
public sealed record StatusBar : Component
{
    /// <summary>Items on the left.</summary>
    public IReadOnlyList<Element?> Leading { get; init; } = [];

    /// <summary>Items on the right.</summary>
    public IReadOnlyList<Element?> Trailing { get; init; } = [];

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        System.ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        return new Surface
        {
            SurfaceColor = SurfaceName.SurfaceContainer,
            Semantics = new Semantics { Role = SemanticsRole.Group, Label = "Status" },
            Layout = new LayoutStyle
            {
                FlexDirection = FlexDirection.Row,
                AlignItems = Align.Center,
                Height = 26 + theme.DensityOffset / 2f,
                Padding = Edges.Symmetric(6, 0),
                FlexShrink = 0,
            },
            Children =
            [
                new Box
                {
                    HitTestVisible = false,
                    Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = new Edges(0, 0, 0, Dimension.Undefined), Height = 1 },
                    Background = theme.OutlineVariant,
                },
                new Box { Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, FlexGrow = 1, FlexShrink = 1 }, Children = Leading },
                new Box { Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center }, Children = Trailing },
            ],
        };
    }
}
