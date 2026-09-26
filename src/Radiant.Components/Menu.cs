using System;
using System.Collections.Generic;
using Radiant.Components.Primitives;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A list of choices that drops from an anchor (a button): it takes focus, moves between items
/// with the arrow keys and Tab, and closes on a choice, Escape, or a press outside it.
/// <code>
/// var anchor = context.UseRef(new ElementRef()).Value;
/// var open = context.UseState(false);
/// … new Box { Ref = anchor, Children = [new IconButton("more_vert", "More") { OnPress = () => open.Set(true) }] },
///   new Menu(anchor, open.Value, () => open.Set(false), [new MenuItem("Copy", copy) { Icon = "content_copy" }])
/// </code>
/// </summary>
/// <param name="Anchor">What the menu drops from.</param>
/// <param name="Open">Whether it's showing.</param>
/// <param name="OnClose">Called when it should close (a choice, Escape, an outside press).</param>
/// <param name="Items">The entries.</param>
public sealed record Menu(ElementRef Anchor, bool Open, Action OnClose, IReadOnlyList<MenuItem> Items) : Component
{
    /// <summary>Which side of the anchor it opens on.</summary>
    public Side Side { get; init; } = Side.Bottom;

    /// <summary>How it lines up with the anchor.</summary>
    public SideAlign Align { get; init; } = SideAlign.Start;

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var root = context.Root;
        var close = OnClose;
        var items = Items;
        var (anchor, side, align) = (Anchor, Side, Align);
        return new Presence(Open, progress =>
        {
            var rows = new List<Element?>();
            foreach (var item in items)
            {
                if (item.DividerBefore)
                {
                    rows.Add(new Box { Layout = new LayoutStyle { Padding = new Edges(0, 8, 0, 8) }, Children = [new Divider()] });
                }
                rows.Add(new PressableSurface
                {
                    Role = SemanticsRole.MenuItem,
                    ShowDisabled = item.Disabled ? true : null,
                    OnPress = () =>
                    {
                        close();
                        item.OnSelect?.Invoke();
                    },
                    Layout = new LayoutStyle
                    {
                        FlexDirection = FlexDirection.Row,
                        AlignItems = Radiant.Layout.Align.Center,
                        MinHeight = 48 + theme.DensityOffset,
                        Padding = Edges.Symmetric(12, 0),
                        ColumnGap = 12,
                    },
                    Children =
                    [
                        item.Icon is null ? null : new SurfaceIcon(item.Icon) { Legibility = Legibility.Medium },
                        new SurfaceText(item.Text) { TextType = TextType.LabelLarge, Layout = new LayoutStyle { FlexGrow = 1 } },
                        item.Shortcut is null ? null : new SurfaceText(item.Shortcut) { TextType = TextType.LabelLarge, Legibility = Legibility.Medium },
                    ],
                });
            }

            return new Anchored(anchor, new DismissableLayer(new FocusScope(new Surface
            {
                SurfaceColor = SurfaceName.SurfaceContainer,
                CornerShape = CornerShapeRole.ExtraSmall,
                Elevation = ElevationLevel.Level2,
                ClipContent = true,
                Semantics = new Semantics { Role = SemanticsRole.Menu },
                Layout = new LayoutStyle { MinWidth = 112, MaxWidth = 280, Padding = Edges.Symmetric(0, 8) },
                Children =
                [
                    new Box
                    {
                        Opacity = progress,
                        Transform = System.Numerics.Matrix3x2.CreateScale(1f, 0.9f + 0.1f * progress),
                        TransformOrigin = new System.Numerics.Vector2(0.5f, 0f),
                        OnKeyDown = e =>
                        {
                            if (e.Key is KeyCode.Down or KeyCode.Up)
                            {
                                root.MoveFocus(forward: e.Key == KeyCode.Down);
                                e.Handled = true;
                            }
                        },
                        Children = rows,
                    },
                ],
            }), close)) { Side = side, Align = align };
        }) { Duration = TimeSpan.FromMilliseconds(150) };
    }
}
