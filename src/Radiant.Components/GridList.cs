using System;
using System.Collections.Generic;
using System.Linq;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// Tiles in columns to choose from (photos, files as icons, templates): a press selects one; with
/// <see cref="MultiSelect"/>, Ctrl (⌘ on macOS) adds or removes one and Shift selects a range. With
/// a tile focused, the arrows move across and down the columns, Home and End go to the ends, Space
/// selects and Enter or a double click activates. Controlled: shows <paramref name="Selection"/> and
/// reports each change.
/// </summary>
/// <param name="Count">How many tiles.</param>
/// <param name="Tile">Builds a tile's content.</param>
/// <param name="Selection">The selected tiles.</param>
/// <param name="OnSelectionChange">Called with the new selection.</param>
public sealed record GridList(int Count, Func<int, Element?> Tile, IReadOnlySet<int> Selection, Action<IReadOnlySet<int>>? OnSelectionChange) : Component
{
    /// <summary>Whether several tiles can be selected.</summary>
    public bool MultiSelect { get; init; }

    /// <summary>Called with a tile when it's double-clicked or Enter is pressed on it.</summary>
    public Action<int>? OnActivate { get; init; }

    /// <summary>A tile's least width; as many columns as fit.</summary>
    public float MinTileWidth { get; init; } = 140f;

    /// <summary>What assistive technology calls the grid.</summary>
    public string? Label { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var refs = context.UseMemo(() => Enumerable.Range(0, Count).Select(_ => new ElementRef()).ToArray(), Count);
        var anchor = context.UseRef(-1);
        var latest = context.UseRef(this);
        latest.Value = this;

        void Select(IReadOnlySet<int> next)
        {
            if (!next.SetEquals(latest.Value.Selection))
            {
                latest.Value.OnSelectionChange?.Invoke(next);
            }
        }

        HashSet<int> Range(int from, int to) => [.. Enumerable.Range(Math.Min(from, to), Math.Abs(to - from) + 1)];

        // Tiles in the first row share its top: that's how many columns the grid laid out.
        int Columns()
        {
            if (refs.Length == 0 || !refs[0].IsMounted)
            {
                return 1;
            }
            var top = refs[0].Bounds.Y;
            return Math.Max(1, refs.TakeWhile(r => r.IsMounted && MathF.Abs(r.Bounds.Y - top) < 1f).Count());
        }

        void Press(int index, PointerEventArgs e)
        {
            var props = latest.Value;
            var command = (e.Modifiers & KeyChord.CommandModifier) != 0;
            if (props.MultiSelect && (e.Modifiers & KeyModifiers.Shift) != 0 && anchor.Value >= 0)
            {
                Select(Range(anchor.Value, index));
            }
            else if (props.MultiSelect && command)
            {
                var next = new HashSet<int>(props.Selection);
                if (!next.Remove(index))
                {
                    next.Add(index);
                }
                anchor.Value = index;
                Select(next);
            }
            else
            {
                anchor.Value = index;
                Select(new HashSet<int> { index });
            }
            if (e.ClickCount == 2)
            {
                props.OnActivate?.Invoke(index);
            }
        }

        void Key(int index, KeyEventArgs e)
        {
            var props = latest.Value;
            var columns = Columns();
            int? next = e.Key switch
            {
                KeyCode.Right => index + 1,
                KeyCode.Left => index - 1,
                KeyCode.Down => index + columns,
                KeyCode.Up => index - columns,
                KeyCode.Home => 0,
                KeyCode.End => props.Count - 1,
                _ => null,
            };
            if (next is { } to)
            {
                if (to >= 0 && to < props.Count)
                {
                    var extend = props.MultiSelect && (e.Modifiers & KeyModifiers.Shift) != 0 && anchor.Value >= 0;
                    if (!extend)
                    {
                        anchor.Value = to;
                    }
                    Select(extend ? Range(anchor.Value, to) : new HashSet<int> { to });
                    refs[to].Focus();
                }
                e.Handled = true;
            }
            else if (e.Key == KeyCode.Space)
            {
                Select(props.MultiSelect && props.Selection.Contains(index)
                    ? props.Selection.Where(i => i != index).ToHashSet()
                    : props.MultiSelect ? [.. props.Selection, index] : new HashSet<int> { index });
                e.Handled = true;
            }
            else if (e.Key == KeyCode.Enter)
            {
                props.OnActivate?.Invoke(index);
                e.Handled = true;
            }
        }

        var tiles = new List<Element?>();
        for (var i = 0; i < Count; i++)
        {
            var index = i;
            tiles.Add(new TileView(Tile(i), Selection.Contains(i), refs[i], e => Press(index, e), e => Key(index, e)) { Key = i });
        }
        return new Box
        {
            Semantics = new Semantics { Role = SemanticsRole.List, Label = Label },
            Layout = new LayoutStyle { AlignSelf = Align.Stretch },
            Children = [new Grid { MinColumnWidth = MinTileWidth, ColumnGap = 12, RowGap = 12, Children = tiles }],
        };
    }

    /// <summary>A tile: its content in a rounded box, filled and ringed when selected.</summary>
    private sealed record TileView(Element? Content, bool Selected, ElementRef Ref, Action<PointerEventArgs> Press, Action<KeyEventArgs> KeyPress) : Component
    {
        public override Element? Build(BuildContext context)
        {
            var theme = context.UseTheme();
            var hovered = context.UseState(false);
            var ring = context.UseState(false);
            var state = context.UseSurface().With(new SurfaceChange { Surface = Selected ? SurfaceName.Secondary : null, ToggleSurfaceContainer = Selected });
            var (press, key) = (Press, KeyPress);
            return ThemeContexts.Surface.Provide(state, new Box
            {
                Ref = Ref,
                Focusable = true,
                Semantics = new Semantics { Role = SemanticsRole.ListItem, Selected = Selected },
                Background = Selected ? theme.SurfaceColor(state) : hovered.Value ? theme.StateLayerColor(state, theme.Theme.StateLayers.Hover) : null,
                BorderWidth = ring.Value ? 2f : Selected ? 1f : 0f,
                BorderColor = theme.Get(SurfaceName.Primary),
                CornerRadii = theme.Corners(CornerShapeRole.Medium),
                Layout = new LayoutStyle { Padding = Edges.All(8), RowGap = 6 },
                OnPointerEnter = _ => hovered.Set(true),
                OnPointerLeave = _ => hovered.Set(false),
                OnPointerDown = e =>
                {
                    if (e.Button == PointerButton.Left)
                    {
                        press(e);
                    }
                },
                OnKeyDown = key,
                OnFocus = e => ring.Set(e.IsFocusVisible),
                OnBlur = _ => ring.Set(false),
                Children = [Content],
            });
        }
    }
}
