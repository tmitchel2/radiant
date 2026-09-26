using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// An IDE-style docking layout: panels grouped as tabs on the left, the right, along the bottom and
/// in the centre, with dividers between the areas. A panel's tab can be dragged to another area:
/// while it's dragged, the area it would land in is shown, and dropping it near the panel's left,
/// right or bottom edge docks it on that side even where nothing is yet. Each tab's context menu
/// (right-click, or Shift+F10 on a focused tab) moves it to another area or closes it, which the
/// keyboard and assistive technology can use too. Controlled: shows <paramref name="Layout"/> and
/// reports each change, so the app can keep the layout between runs.
/// </summary>
/// <param name="Layout">Which panels are where, and each area's size.</param>
/// <param name="OnLayoutChange">Called with the new layout.</param>
/// <param name="Items">The panels that can be shown, by id.</param>
public sealed record DockPanel(DockLayout Layout, Action<DockLayout> OnLayoutChange, IReadOnlyList<DockItem> Items) : Component
{
    // How near an edge of the whole panel a drop docks on that side.
    private const float EdgeZone = 48f;

    // How far a pressed tab moves before it's being dragged rather than chosen.
    private const float DragThreshold = 6f;

    /// <summary>What the centre says when it has no panels.</summary>
    public string EmptyText { get; init; } = "Drop a panel here";

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var latest = context.UseRef(this);
        latest.Value = this;
        var whole = context.UseRef(new ElementRef()).Value;
        var groups = context.UseMemo(() => Enum.GetValues<DockArea>().ToDictionary(a => a, _ => new ElementRef()), default(ValueTuple));
        var press = context.UseRef<(string Id, Vector2 Start)?>(null);
        var drag = context.UseState<(string Id, Vector2 At)?>(((string, Vector2)?)null);
        var rightToLeft = context.UseRightToLeft();
        var items = Items.ToDictionary(i => i.Id);

        void Change(Func<DockLayout, DockLayout> change) => latest.Value.OnLayoutChange(change(latest.Value.Layout));

        // Where a drop at a point lands: an edge of the whole panel docks on that side; otherwise
        // the area under it, or the centre.
        DockArea TargetAt(Vector2 at)
        {
            var bounds = whole.Bounds;
            var (start, end) = rightToLeft ? (DockArea.Right, DockArea.Left) : (DockArea.Left, DockArea.Right);
            if (at.X < bounds.X + EdgeZone)
            {
                return start;
            }
            if (at.X > bounds.Right - EdgeZone)
            {
                return end;
            }
            if (at.Y > bounds.Bottom - EdgeZone)
            {
                return DockArea.Bottom;
            }
            foreach (var (area, group) in groups)
            {
                if (group.IsMounted && group.Bounds.Contains(at.X, at.Y))
                {
                    return area;
                }
            }
            return DockArea.Center;
        }

        // What a drop there would fill: the area's group, or, where it has none yet, a strip along that edge.
        RectangleF TargetRect(DockArea area)
        {
            if (groups[area].IsMounted && latest.Value.Layout[area].Items.Count > 0)
            {
                return groups[area].Bounds;
            }
            var b = whole.Bounds;
            var width = MathF.Min(260f, b.Width * 0.3f);
            var height = MathF.Min(200f, b.Height * 0.35f);
            var left = area == DockArea.Left != rightToLeft;
            return area switch
            {
                DockArea.Bottom => new RectangleF(b.X, b.Bottom - height, b.Width, height),
                DockArea.Center => b,
                _ => left ? new RectangleF(b.X, b.Y, width, b.Height) : new RectangleF(b.Right - width, b.Y, width, b.Height),
            };
        }

        var tabHandlers = new TabHandlers(
            Down: (id, at) => press.Value = (id, at),
            Move: at =>
            {
                if (press.Value is { } pressed && (drag.Value is not null || Vector2.Distance(at, pressed.Start) > DragThreshold))
                {
                    drag.Set((pressed.Id, at));
                }
            },
            Up: at =>
            {
                if (press.Value is not { } pressed)
                {
                    return;
                }
                press.Value = null;
                if (drag.Value is not null)
                {
                    drag.Set(null);
                    var target = TargetAt(at);
                    if (latest.Value.Layout.AreaOf(pressed.Id) != target)
                    {
                        Change(l => l.Move(pressed.Id, target));
                    }
                }
                else
                {
                    Change(l => l.Activate(pressed.Id));
                }
            },
            Activate: id => Change(l => l.Activate(id)),
            MoveTo: (id, area) => Change(l => l.Move(id, area)),
            Close: id => Change(l => l.Close(id)));

        Element? Group(DockArea area)
        {
            var group = Layout[area];
            if (group.Items.Count == 0 && area != DockArea.Center)
            {
                return null;
            }
            return new GroupView(area, group, [.. group.Items.Where(items.ContainsKey).Select(id => items[id])], groups[area], tabHandlers, EmptyText) { Key = area.ToString() };
        }

        void Resize(DockArea area, float size) => Change(l => l.Resize(area, size));

        // The centre, with the bottom under it, the right beside them, and the left before all three.
        var content = Group(DockArea.Center);
        if (Group(DockArea.Bottom) is { } bottom)
        {
            content = new Splitter(content, bottom)
            {
                Orientation = Orientation.Vertical,
                SizedPane = SplitterPane.Second,
                Size = Layout.Bottom.Size,
                InitialSize = 200f,
                OnSizeChange = s => Resize(DockArea.Bottom, s),
                MinSize = 80f,
                Label = "Bottom panels",
            };
        }
        if (Group(DockArea.Right) is { } right)
        {
            content = new Splitter(content, right)
            {
                SizedPane = SplitterPane.Second,
                Size = Layout.Right.Size,
                InitialSize = 260f,
                OnSizeChange = s => Resize(DockArea.Right, s),
                Label = "Right panels",
            };
        }
        if (Group(DockArea.Left) is { } left)
        {
            content = new Splitter(left, content)
            {
                SizedPane = SplitterPane.First,
                Size = Layout.Left.Size,
                InitialSize = 260f,
                OnSizeChange = s => Resize(DockArea.Left, s),
                Label = "Left panels",
            };
        }

        // While a tab is dragged: where it would land, and its title under the pointer.
        Element? overlay = null;
        if (drag.Value is { } dragged && items.TryGetValue(dragged.Id, out var item))
        {
            var target = TargetRect(TargetAt(dragged.At));
            var primary = theme.Get(SurfaceName.Primary);
            overlay = new Portal(new Box
            {
                HitTestVisible = false,
                Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = Edges.All(0) },
                Children =
                [
                    new Box
                    {
                        Layout = new LayoutStyle
                        {
                            Position = PositionType.Absolute,
                            Inset = Edges.Physical(target.X, target.Y, Dimension.Undefined, Dimension.Undefined, rightToLeft),
                            Width = target.Width,
                            Height = target.Height,
                        },
                        Background = primary.WithAlpha(0.12f),
                        BorderWidth = 2f,
                        BorderColor = primary,
                        CornerRadii = theme.Corners(CornerShapeRole.Small),
                    },
                    new Surface
                    {
                        SurfaceColor = SurfaceName.Inverse,
                        CornerShape = CornerShapeRole.ExtraSmall,
                        Layout = new LayoutStyle
                        {
                            Position = PositionType.Absolute,
                            Inset = Edges.Physical(dragged.At.X + 12, dragged.At.Y + 12, Dimension.Undefined, Dimension.Undefined, rightToLeft),
                            Padding = Edges.Symmetric(10, 6),
                        },
                        Children = [new SurfaceText(item.Title) { TextType = TextType.LabelLarge }],
                    },
                ],
            });
        }

        return new Box
        {
            Ref = whole,
            Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1, AlignSelf = Align.Stretch },
            Children = [content, overlay],
        };
    }

    /// <summary>What a tab does with the pointer, the keyboard and its menu.</summary>
    private sealed record TabHandlers(
        Action<string, Vector2> Down,
        Action<Vector2> Move,
        Action<Vector2> Up,
        Action<string> Activate,
        Action<string, DockArea> MoveTo,
        Action<string> Close);

    /// <summary>One area: its tabs, and the shown panel under them.</summary>
    private sealed record GroupView(DockArea Area, DockGroup Group, IReadOnlyList<DockItem> Items, ElementRef Ref, TabHandlers Handlers, string EmptyText) : Component
    {
        public override Element? Build(BuildContext context)
        {
            var theme = context.UseTheme();
            var shown = Group.Shown;
            var active = Items.FirstOrDefault(i => i.Id == shown);
            var tabs = Items.Select(i => (Element?)new TabView(i, Area, i.Id == shown, Handlers) { Key = i.Id }).ToList();
            return new Surface
            {
                SurfaceColor = Area == DockArea.Center ? SurfaceName.Surface : SurfaceName.SurfaceContainerLow,
                Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1, AlignSelf = Align.Stretch },
                Children =
                [
                    new Box
                    {
                        Ref = Ref,
                        Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 },
                        Children =
                        [
                            new Box
                            {
                                Semantics = new Semantics { Role = SemanticsRole.TabList, Label = $"{Area} panels" },
                                Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, Height = 34, FlexShrink = 0, Padding = Edges.Symmetric(4, 0) },
                                Children = tabs,
                            },
                            new Box { Layout = new LayoutStyle { Height = 1, FlexShrink = 0 }, Background = theme.OutlineVariant },
                            new Box
                            {
                                ClipContent = true,
                                Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 },
                                Children =
                                [
                                    active is null
                                        ? new Box
                                        {
                                            Layout = new LayoutStyle { FlexGrow = 1, AlignItems = Align.Center, JustifyContent = Justify.Center },
                                            Children = [new SurfaceText(EmptyText) { Legibility = Radiant.Theming.Legibility.Medium }],
                                        }
                                        : new Box { Key = active.Id, Layout = new LayoutStyle { FlexGrow = 1 }, Children = [active.Content] },
                                ],
                            },
                        ],
                    },
                ],
            };
        }
    }

    /// <summary>A panel's tab: pressed, it's shown; dragged, it moves; its menu moves or closes it.</summary>
    private sealed record TabView(DockItem Item, DockArea Area, bool Shown, TabHandlers Handlers) : Component
    {
        public override Element? Build(BuildContext context)
        {
            var theme = context.UseTheme();
            var surface = context.UseSurface();
            var hovered = context.UseState(false);
            var ring = context.UseState(false);
            var (id, handlers) = (Item.Id, Handlers);
            var menu = Enum.GetValues<DockArea>().Where(a => a != Area)
                .Select(a => new MenuItem($"Move to {Name(a)}", () => handlers.MoveTo(id, a)))
                .Append(new MenuItem("Close", () => handlers.Close(id)) { DividerBefore = true, Disabled = !Item.Closable })
                .ToList();
            var tab = new Box
            {
                Focusable = true,
                Semantics = new Semantics { Role = SemanticsRole.Tab, Label = Item.Title, Selected = Shown },
                Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 6, Height = 34, Padding = new Edges(10, 0, Item.Closable ? 4 : 10, 0) },
                Background = hovered.Value ? theme.StateLayerColor(surface, theme.Theme.StateLayers.Hover) : null,
                BorderWidth = ring.Value ? 2f : 0f,
                BorderColor = theme.Get(SurfaceName.Primary),
                CornerRadii = Radiant.Graphics2D.CornerRadii.All(4),
                OnPointerEnter = _ => hovered.Set(true),
                OnPointerLeave = _ => hovered.Set(false),
                OnPointerDown = e =>
                {
                    if (e.Button == PointerButton.Left)
                    {
                        handlers.Down(id, e.Position);
                        e.Handled = true;
                    }
                },
                OnPointerMove = e => handlers.Move(e.Position),
                OnPointerUp = e => handlers.Up(e.Position),
                OnKeyDown = e =>
                {
                    if (e.Key is KeyCode.Enter or KeyCode.Space)
                    {
                        handlers.Activate(id);
                        e.Handled = true;
                    }
                },
                OnFocus = e => ring.Set(e.IsFocusVisible),
                OnBlur = _ => ring.Set(false),
                Children =
                [
                    Item.Icon is null ? null : new SurfaceIcon(Item.Icon) { IconSize = 16, Legibility = Shown ? null : Radiant.Theming.Legibility.Medium },
                    new SurfaceText(Item.Title) { TextType = TextType.LabelLarge, MaxLines = 1, Legibility = Shown ? null : Radiant.Theming.Legibility.Medium },
                    Item.Closable ? new CloseButton(Item.Title, () => handlers.Close(id)) : null,
                    // The shown tab is underlined.
                    !Shown ? null : new Box
                    {
                        HitTestVisible = false,
                        Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = new Edges(6, Dimension.Undefined, 6, 0), Height = 2 },
                        Background = theme.Get(SurfaceName.Primary),
                    },
                ],
            };
            return new ContextMenu(tab, menu);
        }

        private static string Name(DockArea area) => area switch
        {
            DockArea.Left => "left",
            DockArea.Right => "right",
            DockArea.Bottom => "bottom",
            _ => "centre",
        };
    }

    /// <summary>A tab's close button: it takes its own press, so closing never starts a drag.</summary>
    private sealed record CloseButton(string Label, Action Close) : Component
    {
        public override Element? Build(BuildContext context)
        {
            var theme = context.UseTheme();
            var surface = context.UseSurface();
            var hovered = context.UseState(false);
            var close = Close;
            return new Box
            {
                Semantics = new Semantics { Role = SemanticsRole.Button, Label = $"Close {Label}" },
                Layout = new LayoutStyle { Width = 20, Height = 20, AlignItems = Align.Center, JustifyContent = Justify.Center },
                CornerRadii = Radiant.Graphics2D.CornerRadii.All(4),
                Background = hovered.Value ? theme.StateLayerColor(surface, theme.Theme.StateLayers.Hover * 1.5f) : null,
                OnPointerEnter = _ => hovered.Set(true),
                OnPointerLeave = _ => hovered.Set(false),
                OnPointerDown = e => e.Handled = true,
                OnClick = e =>
                {
                    if (e.Button == PointerButton.Left)
                    {
                        close();
                        e.Handled = true;
                    }
                },
                Children = [new SurfaceIcon("close") { IconSize = 14 }],
            };
        }
    }
}
