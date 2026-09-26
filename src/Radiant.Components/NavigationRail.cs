using System;
using System.Collections.Generic;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A narrow column of destinations down the side of a desktop window: each an icon in a pill that
/// fills when current, with its label below.
/// </summary>
/// <param name="Items">The destinations.</param>
/// <param name="Selected">The current destination's index.</param>
/// <param name="OnSelect">Called with a destination's index when it's chosen.</param>
public sealed record NavigationRail(IReadOnlyList<NavItem> Items, int Selected, Action<int>? OnSelect) : Component
{
    /// <summary>Something above the destinations, such as a menu button or a floating action button.</summary>
    public Element? Header { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var items = new List<Element?> { Header };
        for (var i = 0; i < Items.Count; i++)
        {
            var index = i;
            var item = Items[i];
            var chosen = i == Selected;
            var select = OnSelect;
            Element icon = new SurfaceIcon(item.Icon) { IconFilled = chosen };
            if (item.Badge is { } badge)
            {
                icon = new Badge(icon) { Count = badge == 0 ? null : badge };
            }
            items.Add(new PressableSurface
            {
                Role = SemanticsRole.Tab,
                Selected = chosen,
                Label = item.Label,
                OnPress = () => select?.Invoke(index),
                CornerShape = CornerShapeRole.Medium,
                Layout = new LayoutStyle { Width = 72, AlignItems = Align.Center, RowGap = 4, Padding = Edges.Symmetric(0, 6) },
                Children =
                [
                    new Surface
                    {
                        SurfaceColor = chosen ? SurfaceName.Secondary : null,
                        SurfaceContainerToggle = chosen ? true : null,
                        ContentColor = chosen ? null : SurfaceName.SurfaceVariant,
                        ContentOnToggle = chosen ? null : true,
                        CornerShape = CornerShapeRole.Full,
                        Layout = new LayoutStyle { Width = 56, Height = 32, AlignItems = Align.Center, JustifyContent = Justify.Center },
                        Children = [icon],
                    },
                    new SurfaceText(item.Label) { TextType = TextType.LabelMedium, MaxLines = 1 },
                ],
            });
        }
        return new Surface
        {
            SurfaceColor = SurfaceName.Surface,
            Semantics = new Semantics { Role = SemanticsRole.TabList },
            Layout = new LayoutStyle { Width = 80 + theme.DensityOffset, AlignItems = Align.Center, RowGap = 12, Padding = Edges.Symmetric(0, 12) },
            Children = items,
        };
    }
}
