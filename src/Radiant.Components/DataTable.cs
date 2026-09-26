using System;
using System.Collections.Generic;
using System.Linq;
using Radiant.Layout;
using Radiant.Platform;
using Radiant.Scrolling;
using Radiant.Text;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A table of rows under a header, virtualised, so a hundred thousand rows cost a screenful.
/// <list type="bullet">
/// <item>Columns are dragged wider or narrower by their header's right edge; a sortable
/// column's header sorts by it (the owner sorts the rows and passes them in order).</item>
/// <item>A press selects a row; with <see cref="MultiSelect"/>, Ctrl (⌘ on macOS) adds or
/// removes one and Shift selects a range, and <see cref="ShowCheckboxes"/> adds a column of
/// check boxes with select-all in the header.</item>
/// <item>From the keyboard, with the table focused: arrows move (Shift extends), Page Up and
/// Down move a page, Home and End go to the ends, Space toggles, Enter or a double click
/// activates, and Ctrl+A (⌘A) selects all.</item>
/// </list>
/// Selection is kept by the table unless <see cref="Selection"/> is set, when the owner keeps it
/// through <see cref="OnSelectionChange"/>.
/// </summary>
/// <param name="Columns">The columns.</param>
/// <param name="RowCount">How many rows.</param>
public sealed partial record DataTable(IReadOnlyList<DataColumn> Columns, int RowCount) : Component
{
    [TestId] public static partial string Row { get; }
    [TestId<Checkbox>] public static partial string SelectAll { get; }
    [TestId<Checkbox>] public static partial string SelectRow { get; }

    private const float CheckboxWidth = 52f;

    /// <summary>What assistive technology calls the table.</summary>
    public string? Label { get; init; }

    /// <summary>The column the rows are sorted by, or null.</summary>
    public int? SortColumn { get; init; }

    /// <summary>Which way <see cref="SortColumn"/> sorts.</summary>
    public SortDirection SortDirection { get; init; }

    /// <summary>Called with a column and direction when a sortable header is pressed.</summary>
    public Action<int, SortDirection>? OnSort { get; init; }

    /// <summary>Whether several rows can be selected.</summary>
    public bool MultiSelect { get; init; }

    /// <summary>Whether there's a column of check boxes (with <see cref="MultiSelect"/>).</summary>
    public bool ShowCheckboxes { get; init; }

    /// <summary>The selected rows, when the owner keeps them; null for the table to keep them.</summary>
    public IReadOnlySet<int>? Selection { get; init; }

    /// <summary>Called with the new selection whenever it changes.</summary>
    public Action<IReadOnlySet<int>>? OnSelectionChange { get; init; }

    /// <summary>Called with a row when it's double-clicked or Enter is pressed on it.</summary>
    public Action<int>? OnActivate { get; init; }

    /// <summary>What shows when there are no rows.</summary>
    public string EmptyText { get; init; } = "No rows";

    /// <summary>The table's size and placement (it grows to fill its parent by default).</summary>
    public LayoutStyle? Layout { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var own = context.UseState<IReadOnlySet<int>>(new HashSet<int>());
        var widths = context.UseState(() => Columns.Select(c => c.Width).ToArray());
        if (widths.Value.Length != Columns.Count)
        {
            widths.Set(Columns.Select(c => c.Width).ToArray());
        }
        var active = context.UseState(-1);
        var anchor = context.UseRef(-1);
        var focusRing = context.UseState(false);
        var scroll = context.UseRef(new ScrollController(new ScrollBehaviour())).Value;
        var latest = context.UseRef(this);
        latest.Value = this;

        var rowHeight = 40f + theme.DensityOffset;
        var selection = Selection ?? own.Value;
        var checkboxes = ShowCheckboxes && MultiSelect;
        var columnWidths = widths.Value.Length == Columns.Count ? widths.Value : Columns.Select(c => c.Width).ToArray();

        void Select(IReadOnlySet<int> next)
        {
            var props = latest.Value;
            if (props.Selection is null)
            {
                own.Set(next);
            }
            props.OnSelectionChange?.Invoke(next);
        }

        IReadOnlySet<int> Current() => latest.Value.Selection ?? own.Value;

        HashSet<int> Range(int from, int to)
        {
            var set = new HashSet<int>();
            for (var i = Math.Min(from, to); i <= Math.Max(from, to); i++)
            {
                set.Add(i);
            }
            return set;
        }

        void Toggle(int row)
        {
            var next = new HashSet<int>(Current());
            if (!next.Remove(row))
            {
                next.Add(row);
            }
            anchor.Value = row;
            Select(next);
        }

        // Moves the active row, selecting it (or extending the selection with Shift), and
        // scrolls it into view.
        void MoveTo(int row, bool extend)
        {
            var props = latest.Value;
            if (props.RowCount == 0)
            {
                return;
            }
            row = Math.Clamp(row, 0, props.RowCount - 1);
            active.Set(row);
            if (extend && props.MultiSelect && anchor.Value >= 0)
            {
                Select(Range(anchor.Value, row));
            }
            else
            {
                anchor.Value = row;
                Select(new HashSet<int> { row });
            }
            VirtualList.ScrollToIndex(scroll, row, rowHeight);
        }

        void Press(int row, PointerEventArgs e)
        {
            var props = latest.Value;
            active.Set(row);
            var command = (e.Modifiers & (OperatingSystem.IsMacOS() ? KeyModifiers.Super : KeyModifiers.Control)) != 0;
            if (props.MultiSelect && (e.Modifiers & KeyModifiers.Shift) != 0 && anchor.Value >= 0)
            {
                Select(Range(anchor.Value, row));
            }
            else if (props.MultiSelect && command)
            {
                Toggle(row);
            }
            else if (!(e.ClickCount == 2 && Current().Contains(row)))
            {
                anchor.Value = row;
                Select(new HashSet<int> { row });
            }
            if (e.ClickCount == 2)
            {
                props.OnActivate?.Invoke(row);
            }
        }

        void Key(KeyEventArgs e)
        {
            var props = latest.Value;
            var shift = (e.Modifiers & KeyModifiers.Shift) != 0;
            var command = (e.Modifiers & (OperatingSystem.IsMacOS() ? KeyModifiers.Super : KeyModifiers.Control)) != 0;
            var page = Math.Max(1, (int)(scroll.ViewportSize.Y / rowHeight) - 1);
            var at = active.Value;
            var handled = true;
            switch (e.Key)
            {
                case KeyCode.Down: MoveTo(at + 1, shift); break;
                case KeyCode.Up: MoveTo(at < 0 ? 0 : at - 1, shift); break;
                case KeyCode.PageDown: MoveTo(at + page, shift); break;
                case KeyCode.PageUp: MoveTo(at - page, shift); break;
                case KeyCode.Home: MoveTo(0, shift); break;
                case KeyCode.End: MoveTo(props.RowCount - 1, shift); break;
                case KeyCode.Space when at >= 0:
                    if (props.MultiSelect)
                    {
                        Toggle(at);
                    }
                    else
                    {
                        Select(new HashSet<int> { at });
                    }
                    break;
                case KeyCode.Enter when at >= 0: props.OnActivate?.Invoke(at); break;
                case KeyCode.A when command && props.MultiSelect: Select(Range(0, props.RowCount - 1)); break;
                default: handled = false; break;
            }
            e.Handled |= handled;
        }

        var header = new Header(Columns, columnWidths, SortColumn, SortDirection, OnSort, rowHeight + 4f)
        {
            Checkbox = checkboxes
                ? new Checkbox(RowCount > 0 && selection.Count == RowCount, all => Select(all ? Range(0, RowCount - 1) : new HashSet<int>()))
                {
                    TestId = SelectAll,
                    AccessibleLabel = "Select all",
                    Indeterminate = selection.Count > 0 && selection.Count < RowCount,
                }
                : null,
            OnResize = (column, width) =>
            {
                var copy = (float[])widths.Value.Clone();
                copy[column] = MathF.Max(latest.Value.Columns[column].MinWidth, width);
                widths.Set(copy);
            },
        };

        var (columns, activeRow, showRing) = (Columns, active.Value, focusRing.Value);
        Element body = RowCount == 0
            ? new Box
            {
                Layout = new LayoutStyle { FlexGrow = 1, AlignItems = Align.Center, JustifyContent = Justify.Center, Padding = Edges.All(24) },
                Children = [new SurfaceText(EmptyText) { Legibility = Legibility.Medium }],
            }
            : new VirtualList(RowCount, rowHeight, i => new TableRow(i, columns, columnWidths, selection.Contains(i), i == activeRow && showRing, Press)
            {
                Checkbox = checkboxes ? new Checkbox(selection.Contains(i), _ => Toggle(i)) { TestId = SelectRow, AccessibleLabel = "Select row" } : null,
            })
            {
                Controller = scroll,
                Label = Label,
            };

        return new Surface
        {
            SurfaceColor = SurfaceName.Surface,
            Semantics = new Semantics { Role = SemanticsRole.Table, Label = Label },
            ClipContent = true,
            Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 }.Merge(Layout ?? default),
            Children =
            [
                header,
                new Box
                {
                    Focusable = RowCount > 0,
                    // What takes focus to move through the rows: named for it, not heard as an empty group.
                    Semantics = new Semantics { Role = SemanticsRole.Group, Label = Label is null ? "Rows" : $"{Label} rows" },
                    Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 },
                    OnKeyDown = Key,
                    OnFocus = e =>
                    {
                        focusRing.Set(e.IsFocusVisible);
                        if (active.Value < 0 && latest.Value.RowCount > 0)
                        {
                            active.Set(0);
                        }
                    },
                    OnBlur = _ => focusRing.Set(false),
                    Children = [body],
                },
            ],
        };
    }

    // A cell's box: its column's width (or the spare width), its content lined up as the column says.
    internal static Box CellBox(DataColumn column, float width, Element? content, SemanticsRole role) => new()
    {
        Semantics = role == SemanticsRole.Cell ? null : new Semantics { Role = role },
        ClipContent = true,
        Layout = new LayoutStyle
        {
            Width = column.Grow ? Dimension.Undefined : width,
            FlexGrow = column.Grow ? 1 : 0,
            FlexShrink = column.Grow ? 1 : 0,
            MinWidth = column.MinWidth,
            FlexDirection = FlexDirection.Row,
            AlignItems = Align.Center,
            JustifyContent = column.Alignment is TextAlignment.End or TextAlignment.Right ? Justify.FlexEnd
                : column.Alignment == TextAlignment.Center ? Justify.Center
                : Justify.FlexStart,
            Padding = Edges.Symmetric(16, 0),
        },
        Children = [content],
    };

    /// <summary>The header: column names, sort arrows and resize grips, over a divider.</summary>
    private sealed record Header(IReadOnlyList<DataColumn> Columns, float[] Widths, int? SortColumn, SortDirection Direction, Action<int, SortDirection>? OnSort, float Height) : Component
    {
        public Element? Checkbox { get; init; }

        public Action<int, float>? OnResize { get; init; }

        public override Element? Build(BuildContext context)
        {
            var theme = context.UseTheme();
            var cells = new List<Element?>();
            if (Checkbox is not null)
            {
                cells.Add(new Box { Layout = new LayoutStyle { Width = CheckboxWidth, AlignItems = Align.Center, JustifyContent = Justify.Center }, Children = [Checkbox] });
            }
            for (var i = 0; i < Columns.Count; i++)
            {
                var index = i;
                var column = Columns[i];
                var sorted = SortColumn == i;
                var sort = OnSort;
                var direction = sorted && Direction == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;
                Element label = new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 4 },
                    Children =
                    [
                        new SurfaceText(column.Header) { TextType = TextType.TitleSmall, MaxLines = 1, Legibility = sorted ? null : Legibility.Medium },
                        sorted ? new SurfaceIcon(Direction == SortDirection.Ascending ? "arrow_upward" : "arrow_downward") { IconSize = 16 } : null,
                    ],
                };
                var content = column.Sortable
                    ? new PressableSurface
                    {
                        InsetFocusRing = true,
                        Role = SemanticsRole.ColumnHeader,
                        Label = column.Header,
                        CornerShape = CornerShapeRole.ExtraSmall,
                        OnPress = () => sort?.Invoke(index, direction),
                        Layout = new LayoutStyle { AlignSelf = Align.Stretch, JustifyContent = Justify.Center, Padding = Edges.Symmetric(4, 0), Margin = Edges.Symmetric(-4, 0) },
                        Children = [label],
                    }
                    : label;
                var cell = CellBox(column, Widths[i], content, column.Sortable ? SemanticsRole.Cell : SemanticsRole.ColumnHeader);
                if (!column.Grow && OnResize is { } resize)
                {
                    cell = cell with { Children = [content, new ResizeGrip(column.Header, Widths[i], width => resize(index, width))] };
                }
                cells.Add(cell);
            }
            return new Surface
            {
                SurfaceColor = SurfaceName.SurfaceContainerLow,
                Semantics = new Semantics { Role = SemanticsRole.Row },
                Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Stretch, Height = Height, FlexShrink = 0 },
                Children =
                [
                    .. cells,
                    new Box
                    {
                        HitTestVisible = false,
                        Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = new Edges(0, Dimension.Undefined, 0, 0), Height = 1 },
                        Background = theme.OutlineVariant,
                    },
                ],
            };
        }
    }

    /// <summary>The strip along a header cell's right edge that drags the column's width.</summary>
    private sealed record ResizeGrip(string Column, float Width, Action<float> OnResize) : Component
    {
        public override Element? Build(BuildContext context)
        {
            var theme = context.UseTheme();
            var hovered = context.UseState(false);
            var drag = context.UseRef<(float Pointer, float Width)?>(null);
            var rightToLeft = context.UseRightToLeft();
            var latest = context.UseRef(this);
            latest.Value = this;
            return new Box
            {
                Cursor = CursorShape.ResizeLeftRight,
                Semantics = new Semantics { Role = SemanticsRole.Separator, Label = $"Resize {Column}" },
                Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = new Edges(Dimension.Undefined, 8, 0, 8), Width = 6, AlignItems = Align.FlexEnd },
                OnPointerEnter = _ => hovered.Set(true),
                OnPointerLeave = _ => hovered.Set(false),
                OnPointerDown = e =>
                {
                    drag.Value = (e.Position.X, latest.Value.Width);
                    e.Handled = true;
                },
                OnPointerMove = e =>
                {
                    if (drag.Value is { } start)
                    {
                        // The grip is at the column's end: dragging it towards the end widens the column.
                        latest.Value.OnResize(start.Width + (rightToLeft ? start.Pointer - e.Position.X : e.Position.X - start.Pointer));
                    }
                },
                OnPointerUp = _ => drag.Value = null,
                Children =
                [
                    new Box
                    {
                        HitTestVisible = false,
                        Layout = new LayoutStyle { Width = hovered.Value || drag.Value is not null ? 2 : 1, FlexGrow = 1 },
                        Background = hovered.Value || drag.Value is not null ? theme.Get(SurfaceName.Primary) : theme.OutlineVariant,
                    },
                ],
            };
        }
    }

    /// <summary>A row: its cells, filled when selected, a state layer when hovered, a ring when it's the keyboard's.</summary>
    private sealed record TableRow(int Index, IReadOnlyList<DataColumn> Columns, float[] Widths, bool Selected, bool Ring, Action<int, PointerEventArgs> Press) : Component
    {
        public Element? Checkbox { get; init; }

        public override Element? Build(BuildContext context)
        {
            var theme = context.UseTheme();
            var hovered = context.UseState(false);
            var state = context.UseSurface().With(new SurfaceChange
            {
                Surface = Selected ? SurfaceName.Secondary : null,
                ToggleSurfaceContainer = Selected,
            });
            var (index, press) = (Index, Press);
            var cells = new List<Element?>();
            if (Checkbox is not null)
            {
                // A press on the check box toggles just this row, rather than selecting it alone.
                cells.Add(new Box
                {
                    Layout = new LayoutStyle { Width = CheckboxWidth, AlignItems = Align.Center, JustifyContent = Justify.Center },
                    OnPointerDown = e => e.Handled = true,
                    Children = [Checkbox],
                });
            }
            for (var i = 0; i < Columns.Count; i++)
            {
                cells.Add(CellBox(Columns[i], Widths[i], Columns[i].Cell(Index), SemanticsRole.Cell));
            }
            return ThemeContexts.Surface.Provide(state, new Box
            {
                TestId = Row,
                Semantics = new Semantics { Role = SemanticsRole.Row, Selected = Selected, Value = (Index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) },
                Background = hovered.Value ? theme.StateLayerColor(state, theme.Theme.StateLayers.Hover) : Selected ? theme.SurfaceColor(state) : null,
                BorderWidth = Ring ? 2f : 0f,
                BorderColor = theme.Get(SurfaceName.Primary),
                Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Stretch, FlexGrow = 1 },
                OnPointerEnter = _ => hovered.Set(true),
                OnPointerLeave = _ => hovered.Set(false),
                OnPointerDown = e =>
                {
                    if (e.Button == PointerButton.Left)
                    {
                        press(index, e);
                    }
                },
                Children =
                [
                    .. cells,
                    new Box
                    {
                        HitTestVisible = false,
                        Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = new Edges(0, Dimension.Undefined, 0, 0), Height = 1 },
                        Background = theme.OutlineVariant,
                    },
                ],
            });
        }
    }
}
