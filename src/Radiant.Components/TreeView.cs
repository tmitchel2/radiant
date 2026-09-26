using System;
using System.Collections.Generic;
using Radiant.Layout;
using Radiant.Scrolling;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A tree of items that expand to show their children (files and folders, an outline). Only the
/// rows in view are built, so large trees stay cheap.
/// <list type="bullet">
/// <item>A press selects an item; its chevron, or a double click, expands or collapses it; a
/// double click on a leaf activates it.</item>
/// <item>From the keyboard, with the tree focused: Up and Down move, Right expands (or goes to
/// the first child), Left collapses (or goes to the parent), Home and End go to the ends, and
/// Enter activates.</item>
/// </list>
/// Selection and expansion are kept by the tree unless <see cref="Selected"/> or
/// <see cref="Expanded"/> is set, when the owner keeps them.
/// </summary>
/// <param name="Roots">The top-level items.</param>
public sealed record TreeView(IReadOnlyList<TreeNode> Roots) : Component
{
    private const float Indent = 16f;

    /// <summary>The selected item's id, when the owner keeps it.</summary>
    public string? Selected { get; init; }

    /// <summary>Called with an item when it's selected.</summary>
    public Action<TreeNode>? OnSelect { get; init; }

    /// <summary>Called with a leaf when it's double-clicked or Enter is pressed on it.</summary>
    public Action<TreeNode>? OnActivate { get; init; }

    /// <summary>The expanded items' ids, when the owner keeps them.</summary>
    public IReadOnlySet<string>? Expanded { get; init; }

    /// <summary>The items expanded at first, when the tree keeps them.</summary>
    public IReadOnlySet<string>? InitialExpanded { get; init; }

    /// <summary>Called with the expanded ids whenever an item expands or collapses.</summary>
    public Action<IReadOnlySet<string>>? OnExpandedChange { get; init; }

    /// <summary>What assistive technology calls the tree.</summary>
    public string? Label { get; init; }

    /// <summary>The tree's size and placement (it grows to fill its parent by default).</summary>
    public LayoutStyle? Layout { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rightToLeft = context.UseRightToLeft();
        var theme = context.UseTheme();
        var ownSelected = context.UseState((string?)null);
        var ownExpanded = context.UseState(() => (IReadOnlySet<string>)new HashSet<string>(InitialExpanded ?? new HashSet<string>()));
        var focusRing = context.UseState(false);
        var scroll = context.UseRef(new ScrollController(new ScrollBehaviour())).Value;
        var latest = context.UseRef(this);
        latest.Value = this;

        var rowHeight = 28f + theme.DensityOffset / 2f;
        var expanded = Expanded ?? ownExpanded.Value;
        var selected = Selected ?? ownSelected.Value;

        // The rows in view order: every root, and the children of every expanded item.
        var rows = new List<(TreeNode Node, int Depth, int Parent)>();
        void Add(IReadOnlyList<TreeNode> nodes, int depth, int parent)
        {
            foreach (var node in nodes)
            {
                var index = rows.Count;
                rows.Add((node, depth, parent));
                if (node.Children.Count > 0 && expanded.Contains(node.Id))
                {
                    Add(node.Children, depth + 1, index);
                }
            }
        }
        Add(Roots, 0, -1);
        var selectedRow = rows.FindIndex(r => r.Node.Id == selected);

        void Select(int row)
        {
            if (row < 0 || row >= rows.Count)
            {
                return;
            }
            var node = rows[row].Node;
            if (latest.Value.Selected is null)
            {
                ownSelected.Set(node.Id);
            }
            latest.Value.OnSelect?.Invoke(node);
            VirtualList.ScrollToIndex(scroll, row, rowHeight);
        }

        void SetExpanded(TreeNode node, bool open)
        {
            var current = latest.Value.Expanded ?? ownExpanded.Value;
            if (node.Children.Count == 0 || current.Contains(node.Id) == open)
            {
                return;
            }
            var next = new HashSet<string>(current);
            if (open)
            {
                next.Add(node.Id);
            }
            else
            {
                next.Remove(node.Id);
            }
            if (latest.Value.Expanded is null)
            {
                ownExpanded.Set(next);
            }
            latest.Value.OnExpandedChange?.Invoke(next);
        }

        void Activate(int row)
        {
            var node = rows[row].Node;
            if (node.Children.Count > 0)
            {
                SetExpanded(node, !expanded.Contains(node.Id));
            }
            else
            {
                latest.Value.OnActivate?.Invoke(node);
            }
        }

        void Key(KeyEventArgs e)
        {
            var at = selectedRow;
            var handled = true;
            switch (e.Key.ForDirection(rightToLeft))
            {
                case KeyCode.Down: Select(at < 0 ? 0 : at + 1); break;
                case KeyCode.Up: Select(at < 0 ? 0 : at - 1); break;
                case KeyCode.Home: Select(0); break;
                case KeyCode.End: Select(rows.Count - 1); break;
                case KeyCode.Right when at >= 0:
                    var node = rows[at].Node;
                    if (node.Children.Count > 0 && !expanded.Contains(node.Id))
                    {
                        SetExpanded(node, true);
                    }
                    else if (node.Children.Count > 0)
                    {
                        Select(at + 1);
                    }
                    break;
                case KeyCode.Left when at >= 0:
                    if (rows[at].Node.Children.Count > 0 && expanded.Contains(rows[at].Node.Id))
                    {
                        SetExpanded(rows[at].Node, false);
                    }
                    else
                    {
                        Select(rows[at].Parent);
                    }
                    break;
                case KeyCode.Enter when at >= 0: Activate(at); break;
                default: handled = false; break;
            }
            e.Handled |= handled;
        }

        var ring = focusRing.Value;
        return new Box
        {
            Focusable = rows.Count > 0,
            Semantics = new Semantics { Role = SemanticsRole.Tree, Label = Label },
            Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 }.Merge(Layout ?? default),
            OnKeyDown = Key,
            OnFocus = e => focusRing.Set(e.IsFocusVisible),
            OnBlur = _ => focusRing.Set(false),
            Children =
            [
                new VirtualList(rows.Count, rowHeight, i =>
                {
                    var (node, depth, _) = rows[i];
                    var open = expanded.Contains(node.Id);
                    return new Row(node, depth, open, i == selectedRow, i == selectedRow && ring, e =>
                    {
                        Select(i);
                        if (e.ClickCount == 2)
                        {
                            Activate(i);
                        }
                    }, () => SetExpanded(node, !open));
                })
                {
                    Controller = scroll,
                    Padding = Edges.Symmetric(4, 4),
                },
            ],
        };
    }

    /// <summary>One item: indent, chevron, icon, label and trailing text; filled when selected.</summary>
    private sealed record Row(TreeNode Node, int Depth, bool Open, bool Chosen, bool Ring, Action<PointerEventArgs> Press, Action Toggle) : Component
    {
        public override Element? Build(BuildContext context)
        {
            var theme = context.UseTheme();
            var hovered = context.UseState(false);
            var state = context.UseSurface().With(new SurfaceChange
            {
                Surface = Chosen ? SurfaceName.Secondary : null,
                ToggleSurfaceContainer = Chosen,
            });
            var (press, toggle) = (Press, Toggle);
            var parent = Node.Children.Count > 0;
            return ThemeContexts.Surface.Provide(state, new Box
            {
                Semantics = new Semantics
                {
                    Role = SemanticsRole.TreeItem,
                    Label = Node.Label,
                    Selected = Chosen,
                    Expanded = parent ? Open : null,
                    Value = (Depth + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                Background = hovered.Value && !Chosen ? theme.StateLayerColor(state, theme.Theme.StateLayers.Hover) : Chosen ? theme.SurfaceColor(state) : null,
                BorderWidth = Ring ? 2f : 0f,
                BorderColor = theme.Get(SurfaceName.Primary),
                CornerRadii = theme.Corners(CornerShapeRole.ExtraSmall),
                Layout = new LayoutStyle
                {
                    FlexGrow = 1,
                    FlexDirection = FlexDirection.Row,
                    AlignItems = Align.Center,
                    ColumnGap = 4,
                    Padding = new Edges(4 + Depth * Indent, 0, 8, 0),
                },
                OnPointerEnter = _ => hovered.Set(true),
                OnPointerLeave = _ => hovered.Set(false),
                OnPointerDown = e =>
                {
                    if (e.Button == PointerButton.Left)
                    {
                        press(e);
                    }
                },
                Children =
                [
                    // The chevron's slot keeps its width on leaves, so labels line up by depth.
                    new Box
                    {
                        Layout = new LayoutStyle { Width = 20, Height = 20, AlignItems = Align.Center, JustifyContent = Justify.Center, FlexShrink = 0 },
                        Semantics = parent ? new Semantics { Role = SemanticsRole.Button, Label = Open ? $"Collapse {Node.Label}" : $"Expand {Node.Label}" } : null,
                        OnPointerDown = e =>
                        {
                            if (parent && e.Button == PointerButton.Left)
                            {
                                toggle();
                                e.Handled = true;
                            }
                        },
                        Children = [parent ? new SurfaceIcon(Open ? "expand_more" : "chevron_right") { IconSize = 18, Legibility = Legibility.Medium } : null],
                    },
                    Node.Icon is null ? null : new SurfaceIcon(Open && Node.ExpandedIcon is not null ? Node.ExpandedIcon : Node.Icon) { IconSize = 18, Legibility = Chosen ? null : Legibility.Medium },
                    new SurfaceText(Node.Label) { MaxLines = 1, Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 } },
                    Node.Trailing is null ? null : new SurfaceText(Node.Trailing) { TextType = TextType.LabelSmall, Legibility = Legibility.Medium },
                ],
            });
        }
    }
}
