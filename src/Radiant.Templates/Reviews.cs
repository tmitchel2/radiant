using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>
/// A product's reviews: a summary (the average, its stars, how many, and a bar per star count)
/// beside the reviews themselves, which move under it on narrow widths.
/// </summary>
/// <param name="Items">The reviews.</param>
public sealed partial record Reviews(IReadOnlyList<Review> Items) : Component
{
    [TestId<SurfaceButton>] public static partial string WriteReview { get; }

    /// <summary>The heading.</summary>
    public string Title { get; init; } = "Customer reviews";

    /// <summary>What writing a review does; with none, there's no button for it.</summary>
    public Action? OnWrite { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var count = Items.Count;
        var average = count == 0 ? 0 : Items.Average(r => r.Rating);
        var bars = Enumerable.Range(1, 5).Reverse().Select(stars =>
        {
            var share = count == 0 ? 0f : Items.Count(r => r.Rating == stars) / (float)count;
            return (Element?)new Box
            {
                Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 8 },
                Children =
                [
                    new SurfaceText(stars.ToString(CultureInfo.InvariantCulture)) { TextType = TextType.LabelMedium, Layout = new LayoutStyle { Width = 10 } },
                    new Box { Layout = new LayoutStyle { FlexGrow = 1 }, Children = [new LinearProgress { Value = share, Label = $"{stars} stars" }] },
                    new SurfaceText($"{share:0%}") { TextType = TextType.LabelMedium, Legibility = Legibility.Medium, Layout = new LayoutStyle { Width = 36 }, Alignment = Radiant.Text.TextAlignment.End },
                ],
            };
        }).ToList();

        var summary = new Box
        {
            Layout = new LayoutStyle { RowGap = 12, FlexBasis = 260, FlexGrow = 1, MaxWidth = 360 },
            Children =
            [
                new SurfaceText(Title) { TextType = TextType.HeadlineSmall, HeadingLevel = 2 },
                new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 12 },
                    Children =
                    [
                        new SurfaceText(average.ToString("0.0", CultureInfo.InvariantCulture)) { TextType = TextType.DisplaySmall },
                        new Box
                        {
                            Layout = new LayoutStyle { RowGap = 2 },
                            Children = [new Stars(average), new SurfaceText($"Based on {count} reviews") { TextType = TextType.BodySmall, Legibility = Legibility.Medium }],
                        },
                    ],
                },
                .. bars,
                OnWrite is null ? null : new SurfaceButton("Write a review", ButtonVariant.Outlined) { TestId = WriteReview, OnPress = OnWrite, Layout = new LayoutStyle { Margin = new Edges(0, 8, 0, 0) } },
            ],
        };

        var list = new Box
        {
            Semantics = new Semantics { Role = SemanticsRole.List, Label = "Reviews" },
            Layout = new LayoutStyle { RowGap = 20, FlexBasis = 320, FlexGrow = 2 },
            Children = [.. Items.Select((review, i) => (Element?)new Box
            {
                Semantics = new Semantics { Role = SemanticsRole.ListItem, Label = $"{review.Title}, by {review.Author}" },
                Layout = new LayoutStyle { RowGap = 6 },
                Children =
                [
                    i == 0 ? null : new Divider(),
                    new Box
                    {
                        Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 12, Margin = new Edges(0, i == 0 ? 0 : 14, 0, 0) },
                        Children =
                        [
                            new Avatar(review.Author) { Size = 32 },
                            new Box
                            {
                                Layout = new LayoutStyle { FlexGrow = 1 },
                                Children =
                                [
                                    new SurfaceText(review.Author) { TextType = TextType.TitleSmall },
                                    review.Date is null ? null : new SurfaceText(review.Date) { TextType = TextType.BodySmall, Legibility = Legibility.Medium },
                                ],
                            },
                            new Stars(review.Rating) { Size = 16 },
                        ],
                    },
                    new SurfaceText(review.Title) { TextType = TextType.TitleSmall },
                    new SurfaceText(review.Text) { Legibility = Legibility.Medium },
                ],
            })],
        };

        return new Box
        {
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, FlexWrap = FlexWrap.Wrap, ColumnGap = 48, RowGap = 32, Padding = Edges.Symmetric(0, 16) },
            Children = [summary, list],
        };
    }
}
