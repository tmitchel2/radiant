using System;
using System.Collections.Generic;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>
/// A list beside the chosen item's detail, divided by a splitter, as mail and notes apps are
/// laid out. The list can be searched (by title and subtitle), and with an item focused Up and
/// Down choose its neighbours. With nothing chosen, the detail side shows an empty state.
/// </summary>
/// <param name="Items">The list.</param>
/// <param name="Selected">The chosen item's index, or -1 for none.</param>
/// <param name="OnSelect">Called with an item's index (in <paramref name="Items"/>) when it's chosen.</param>
/// <param name="Detail">The chosen item's detail.</param>
public sealed record MasterDetail(IReadOnlyList<ListEntry> Items, int Selected, Action<int>? OnSelect, Element? Detail) : Component
{
    /// <summary>The list's heading.</summary>
    public string? Title { get; init; }

    /// <summary>Buttons beside the heading (compose, filter).</summary>
    public IReadOnlyList<Element?> Actions { get; init; } = [];

    /// <summary>Whether there's a search field over the list.</summary>
    public bool Searchable { get; init; } = true;

    /// <summary>The list's width at first.</summary>
    public float ListWidth { get; init; } = 320f;

    /// <summary>What the detail side says with nothing chosen.</summary>
    public string EmptyTitle { get; init; } = "Nothing selected";

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var query = context.UseState(TextEditState.Empty);
        var text = query.Value.Text.Trim();
        var shown = new List<int>();
        for (var i = 0; i < Items.Count; i++)
        {
            if (text.Length == 0
                || Items[i].Title.Contains(text, StringComparison.OrdinalIgnoreCase)
                || Items[i].Subtitle.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                shown.Add(i);
            }
        }
        var refs = context.UseMemo(() => new Dictionary<int, ElementRef>(), Items);
        var select = OnSelect;
        var rows = new List<Element?>();
        for (var n = 0; n < shown.Count; n++)
        {
            var index = shown[n];
            var position = n;
            if (!refs.TryGetValue(index, out var reference))
            {
                refs[index] = reference = new ElementRef();
            }
            rows.Add(new Row(Items[index], index == Selected, reference, () => select?.Invoke(index))
            {
                Key = index,
                OnArrow = step =>
                {
                    var next = position + step;
                    if (next >= 0 && next < shown.Count)
                    {
                        select?.Invoke(shown[next]);
                        refs[shown[next]].Focus();
                    }
                },
            });
        }

        var master = new Surface
        {
            SurfaceColor = SurfaceName.SurfaceContainerLow,
            Layout = new LayoutStyle { FlexGrow = 1 },
            Children =
            [
                Title is null && Actions.Count == 0 ? null : new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, Padding = new Edges(16, 8, 8, 0), MinHeight = 48 },
                    Children =
                    [
                        new SurfaceText(Title) { TextType = TextType.TitleLarge, HeadingLevel = 2, MaxLines = 1, Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 } },
                        .. Actions,
                    ],
                },
                Searchable ? new Box
                {
                    Layout = new LayoutStyle { Padding = new Edges(12, 8, 12, 8) },
                    Children = [new SearchField("Search") { Value = query.Value, OnChange = query.Set }],
                } : null,
                shown.Count == 0
                    ? new SurfaceText("No matches") { Legibility = Legibility.Medium, Layout = new LayoutStyle { Padding = Edges.All(16) } }
                    : new ScrollArea
                    {
                        Layout = new LayoutStyle { FlexGrow = 1 },
                        ContentLayout = new LayoutStyle { Padding = Edges.Symmetric(8, 4), RowGap = 2 },
                        Children = [new Box { Semantics = new Semantics { Role = SemanticsRole.List, Label = Title }, Children = rows }],
                    },
            ],
        };
        var detail = new Surface
        {
            SurfaceColor = SurfaceName.Surface,
            Layout = new LayoutStyle { FlexGrow = 1 },
            Children = [Selected >= 0 && Detail is not null ? Detail : new Box
            {
                Layout = new LayoutStyle { FlexGrow = 1, JustifyContent = Justify.Center },
                Children = [new EmptyState("inbox", EmptyTitle)],
            }],
        };
        return new Splitter(master, detail) { InitialSize = ListWidth, MinSize = 220, MinOtherSize = 320, Label = Title ?? "List" };
    }

    /// <summary>One entry: avatar, title and meta, subtitle; filled when chosen.</summary>
    private sealed record Row(ListEntry Entry, bool Chosen, ElementRef Ref, Action Select) : Component
    {
        public Action<int>? OnArrow { get; init; }

        public override Element? Build(BuildContext context)
        {
            var arrow = OnArrow;
            return new Box
            {
                Ref = Ref,
                OnKeyDown = e =>
                {
                    var step = e.Key switch { KeyCode.Down => 1, KeyCode.Up => -1, _ => 0 };
                    if (step != 0)
                    {
                        arrow?.Invoke(step);
                        e.Handled = true;
                    }
                },
                Children =
                [
                    new PressableSurface
                    {
                        SurfaceColor = Chosen ? SurfaceName.Secondary : null,
                        SurfaceContainerToggle = Chosen ? true : null,
                        CornerShape = CornerShapeRole.Medium,
                        Role = SemanticsRole.ListItem,
                        Label = Entry.Title,
                        Selected = Chosen,
                        OnPress = Select,
                        Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 12, Padding = Edges.Symmetric(12, 10) },
                        Children =
                        [
                            new Avatar(Entry.AvatarName ?? Entry.Title) { Size = 36 },
                            new Box
                            {
                                Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1, RowGap = 2 },
                                Children =
                                [
                                    new Box
                                    {
                                        Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 8 },
                                        Children =
                                        [
                                            new SurfaceText(Entry.Title) { TextType = TextType.TitleSmall, MaxLines = 1, Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 } },
                                            Entry.Meta is null ? null : new SurfaceText(Entry.Meta) { TextType = TextType.LabelSmall, Legibility = Legibility.Medium },
                                        ],
                                    },
                                    new SurfaceText(Entry.Subtitle) { Legibility = Legibility.Medium, MaxLines = 1 },
                                ],
                            },
                        ],
                    },
                ],
            };
        }
    }
}
