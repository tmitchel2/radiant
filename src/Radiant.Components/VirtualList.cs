using System;
using System.Collections.Generic;
using Radiant.Layout;
using Radiant.Scrolling;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A scrolling list that builds only the rows in view (and a few either side), so a million
/// rows cost what a screenful does. Every row is <see cref="ItemHeight"/> tall; rows are keyed
/// by index, so a row's state lives as long as it stays in view. Pass a
/// <see cref="Controller"/> to scroll it from outside (<see cref="ScrollToIndex"/>).
/// </summary>
/// <param name="Count">How many rows.</param>
/// <param name="ItemHeight">Each row's height.</param>
/// <param name="Item">Builds the row at an index.</param>
public sealed record VirtualList(int Count, float ItemHeight, Func<int, Element?> Item) : Component
{
    // Rows built before the first layout says how tall the viewport is.
    private const int FirstGuess = 40;

    /// <summary>Rows built beyond each end of the view, so a fast scroll doesn't show gaps.</summary>
    public int Overscan { get; init; } = 4;

    /// <summary>A controller to scroll the list from outside; the list makes its own if null.</summary>
    public ScrollController? Controller { get; init; }

    /// <summary>The list's size and placement (it grows to fill its parent by default).</summary>
    public LayoutStyle? Layout { get; init; }

    /// <summary>Padding around the rows.</summary>
    public Edges Padding { get; init; }

    /// <summary>What assistive technology calls the list.</summary>
    public string? Label { get; init; }

    /// <summary>
    /// Scrolls <paramref name="controller"/> so the row at <paramref name="index"/> is in view,
    /// moving as little as possible (none if it already is).
    /// </summary>
    public static void ScrollToIndex(ScrollController controller, int index, float itemHeight, bool animated = false)
    {
        ArgumentNullException.ThrowIfNull(controller);
        controller.ScrollIntoView(index * itemHeight, itemHeight, animated: animated);
    }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var own = context.UseRef<ScrollController?>(null);
        var controller = Controller ?? (own.Value ??= new ScrollController(new ScrollBehaviour()));
        var range = context.UseState((First: 0, Last: Math.Min(Count, FirstGuess) - 1));
        var (height, overscan, count, top) = (MathF.Max(ItemHeight, 1f), Overscan, Count, Padding.Top.IsSet ? Padding.Top.Value : 0f);

        // Keeps the built range covering the view as it scrolls or resizes.
        context.UseEffect(() =>
        {
            void Update(ScrollMetrics metrics)
            {
                var first = (int)MathF.Floor((metrics.ContentOffset.Y - top) / height) - overscan;
                var last = (int)MathF.Ceiling((metrics.ContentOffset.Y - top + metrics.LayoutMeasurement.Y) / height) - 1 + overscan;
                var next = (Math.Clamp(first, 0, Math.Max(0, count - 1)), Math.Clamp(last, -1, count - 1));
                if (next != range.Value)
                {
                    range.Set(next);
                }
            }
            controller.Scroll += Update;
            controller.ExtentsChanged += Update;
            if (controller.ViewportSize.Y > 0f)
            {
                Update(new ScrollMetrics(controller.Offset, controller.ContentSize, controller.ViewportSize, controller.Velocity));
            }
            return () =>
            {
                controller.Scroll -= Update;
                controller.ExtentsChanged -= Update;
            };
        }, (controller, height, overscan, count, top));

        var (from, to) = (range.Value.First, Math.Min(range.Value.Last, Count - 1));
        var rows = new List<Element?>(Math.Max(0, to - from + 1));
        for (var i = from; i <= to; i++)
        {
            rows.Add(new Box
            {
                Key = i,
                // Whole-pixel tops are exact in floating point far further than float precision
                // alone suggests (every even number to 2^25), so rows never drift apart.
                Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = new Edges(0, i * height, 0, Dimension.Undefined), Height = height },
                Children = [Item(i)],
            });
        }
        return new ScrollArea
        {
            Controller = controller,
            Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 }.Merge(Layout ?? default),
            ContentLayout = new LayoutStyle { Padding = Padding },
            Children =
            [
                new Box
                {
                    Semantics = new Semantics { Role = SemanticsRole.List, Label = Label, Value = Count.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                    Layout = new LayoutStyle { Height = Count * height },
                    Children = rows,
                },
            ],
        };
    }
}
