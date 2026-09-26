using System;
using System.Collections.Generic;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>A list of people or items in an outlined card: avatar, two lines, a note and a status, divided.</summary>
/// <param name="Entries">The rows.</param>
public sealed record StackedList(IReadOnlyList<ListEntry> Entries) : Component
{
    /// <summary>A heading above the list.</summary>
    public string? Title { get; init; }

    /// <summary>Called with a row's index when it's pressed.</summary>
    public Action<int>? OnPress { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        var rows = new List<Element?>();
        if (Title is not null)
        {
            rows.Add(new SurfaceText(Title) { TextType = TextType.TitleMedium, HeadingLevel = 2, Layout = new LayoutStyle { Padding = new Edges(20, 16, 20, 8) } });
        }
        for (var i = 0; i < Entries.Count; i++)
        {
            var index = i;
            var entry = Entries[i];
            var press = OnPress;
            if (i > 0)
            {
                rows.Add(new Divider { Inset = 72 });
            }
            rows.Add(new PressableSurface
            {
                InsetFocusRing = true,
                OnPress = press is null ? null : () => press(index),
                Role = SemanticsRole.ListItem,
                Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, Padding = Edges.Symmetric(16, 12), ColumnGap = 16 },
                Children =
                [
                    new Avatar(entry.AvatarName ?? entry.Title),
                    new Box
                    {
                        Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 },
                        Children =
                        [
                            new SurfaceText(entry.Title) { TextType = TextType.BodyLarge, MaxLines = 1 },
                            new SurfaceText(entry.Subtitle) { Legibility = Legibility.Medium, MaxLines = 1 },
                        ],
                    },
                    entry.Status is null ? null : new Tag(entry.Status),
                    entry.Meta is null ? null : new SurfaceText(entry.Meta) { TextType = TextType.LabelMedium, Legibility = Legibility.Medium },
                ],
            });
        }
        return new Card(rows.ToArray()) { Variant = CardVariant.Outlined, Layout = new LayoutStyle { Padding = Edges.Symmetric(0, 4) } };
    }
}
