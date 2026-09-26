using System;
using System.Collections.Generic;
using System.Linq;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A window's row of menus (File, Edit, View), drawn in the window for apps with their own title bar
/// or on platforms without a global menu bar. A press opens a menu; while one is open, the pointer
/// moving onto another title opens that one instead, and Left and Right move between them.
/// </summary>
/// <param name="Menus">The menus, left to right.</param>
public sealed record MenuBar(IReadOnlyList<MenuBarMenu> Menus) : Component
{
    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var open = context.UseState((int?)null);
        var anchors = context.UseMemo(() => Menus.Select(_ => new ElementRef()).ToArray(), Menus.Count);
        var count = Menus.Count;

        var titles = new List<Element?>();
        for (var i = 0; i < Menus.Count; i++)
        {
            var index = i;
            var isOpen = open.Value == i;
            titles.Add(new Box
            {
                Ref = anchors[i],
                OnPointerEnter = _ =>
                {
                    if (open.Value is { } current && current != index)
                    {
                        open.Set(index);
                    }
                },
                Children =
                [
                    new PressableSurface
                    {
                        SurfaceColor = isOpen ? SurfaceName.Secondary : null,
                        SurfaceContainerToggle = isOpen ? true : null,
                        CornerShape = CornerShapeRole.ExtraSmall,
                        Label = Menus[i].Title,
                        Expanded = isOpen,
                        OnPress = () => open.Set(isOpen ? null : index),
                        Layout = new LayoutStyle { Height = 28, Padding = Edges.Symmetric(10, 0), JustifyContent = Justify.Center },
                        Children = [new SurfaceText(Menus[i].Title) { TextType = TextType.LabelLarge }],
                    },
                ],
            });
        }

        Element? menu = open.Value is { } shown && shown < Menus.Count
            ? new Menu(anchors[shown], true, () => open.Set(null), Menus[shown].Items) { Key = shown }
            : null;

        return new Box
        {
            Semantics = new Semantics { Role = SemanticsRole.Group, Label = "Menu bar" },
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 2, Height = 32, Padding = Edges.Symmetric(4, 0) },
            // Keys from an open menu bubble here: Left and Right go to the neighbouring menu.
            OnKeyDown = e =>
            {
                if (open.Value is { } current && e.Key is KeyCode.Left or KeyCode.Right)
                {
                    open.Set((current + (e.Key == KeyCode.Right ? 1 : count - 1)) % count);
                    e.Handled = true;
                }
            },
            Children = [.. titles, menu],
        };
    }
}
