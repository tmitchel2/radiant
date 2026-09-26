using System;
using System.Collections.Generic;
using System.Numerics;
using Radiant.Layout;
using Radiant.Scrolling;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// Slides in a row that scrolls sideways a slide at a time (featured products, screenshots):
/// previous and next buttons at its sides, dots under it for each slide, and Left and Right with
/// focus inside. Scrolled by the wheel or trackpad, it settles on the nearest slide once scrolling
/// pauses.
/// </summary>
/// <param name="Slides">The slides.</param>
public sealed record Carousel(IReadOnlyList<Element?> Slides) : Component
{
    // How long scrolling must pause before the carousel settles on a slide.
    private const double SettleAfter = 0.15;

    /// <summary>Each slide's width; the carousel's own (one slide at a time) if null.</summary>
    public float? SlideWidth { get; init; }

    /// <summary>The space between slides.</summary>
    public float Gap { get; init; } = 16f;

    /// <summary>The slides' height.</summary>
    public float Height { get; init; } = 220f;

    /// <summary>What assistive technology calls the carousel.</summary>
    public string? Label { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rightToLeft = context.UseRightToLeft();
        var theme = context.UseTheme();
        var scroll = context.UseRef(new ScrollController(new ScrollBehaviour { Axes = ScrollAxes.Horizontal })).Value;
        var viewport = context.UseState(0f);
        var index = context.UseState(0);
        var root = context.Root;
        var (count, gap, fixedWidth) = (Slides.Count, Gap, SlideWidth);
        var width = fixedWidth ?? viewport.Value;
        var step = MathF.Max(1f, width + gap);

        // Follows the scroll to know which slide is current; settles on one once scrolling pauses.
        context.UseEffect(() =>
        {
            IDisposable? ticker = null;
            var idle = 0.0;
            void Settle(double seconds)
            {
                idle += seconds;
                if (idle < SettleAfter || scroll.IsAnimating)
                {
                    return;
                }
                ticker?.Dispose();
                ticker = null;
                var nearest = Math.Clamp((int)MathF.Round(scroll.Offset.X / step), 0, Math.Max(0, count - 1));
                var target = MathF.Min(nearest * step, scroll.MaxOffset.X);
                if (MathF.Abs(scroll.Offset.X - target) > 0.5f)
                {
                    scroll.ScrollTo(new Vector2(target, 0), animated: true);
                }
            }
            void OnScroll(ScrollMetrics metrics)
            {
                var current = Math.Clamp((int)MathF.Round(metrics.ContentOffset.X / step), 0, Math.Max(0, count - 1));
                if (current != index.Value)
                {
                    index.Set(current);
                }
                idle = 0;
                ticker ??= root.AddTicker(Settle, TickerKind.Animation, "carousel settle");
            }
            void OnExtents(ScrollMetrics metrics) => viewport.Set(metrics.LayoutMeasurement.X);
            scroll.Scroll += OnScroll;
            scroll.ExtentsChanged += OnExtents;
            if (scroll.ViewportSize.X > 0)
            {
                viewport.Set(scroll.ViewportSize.X);
            }
            return () =>
            {
                scroll.Scroll -= OnScroll;
                scroll.ExtentsChanged -= OnExtents;
                ticker?.Dispose();
            };
        }, (step, count));

        void GoTo(int slide)
        {
            var target = Math.Clamp(slide, 0, Math.Max(0, count - 1));
            index.Set(target);
            scroll.ScrollTo(new Vector2(MathF.Min(target * step, scroll.MaxOffset.X), 0), animated: true);
        }

        var slides = new List<Element?>();
        for (var i = 0; i < count; i++)
        {
            slides.Add(new Box
            {
                Semantics = new Semantics { Role = SemanticsRole.Group, Label = $"Slide {i + 1} of {count}" },
                Layout = new LayoutStyle { Width = width, Height = Height, FlexShrink = 0 },
                Children = [Slides[i]],
            });
        }

        var dots = new List<Element?>();
        for (var i = 0; i < count; i++)
        {
            var slide = i;
            var current = i == index.Value;
            dots.Add(new PressableSurface
            {
                Label = $"Go to slide {i + 1}",
                Selected = current,
                CornerShape = CornerShapeRole.Full,
                OnPress = () => GoTo(slide),
                Layout = new LayoutStyle { Width = 20, Height = 20, AlignItems = Align.Center, JustifyContent = Justify.Center },
                Children =
                [
                    new Box
                    {
                        HitTestVisible = false,
                        Layout = new LayoutStyle { Width = current ? 16 : 8, Height = 8 },
                        Background = current ? theme.Get(SurfaceName.Primary) : theme.OutlineVariant,
                        CornerRadii = Radiant.Graphics2D.CornerRadii.All(4),
                    },
                ],
            });
        }

        var at = index.Value;
        return new Box
        {
            Semantics = new Semantics { Role = SemanticsRole.Group, Label = Label ?? "Carousel", Value = $"{at + 1} of {count}" },
            Layout = new LayoutStyle { AlignSelf = Align.Stretch, RowGap = 8 },
            OnKeyDown = e =>
            {
                if (e.Key is KeyCode.Left or KeyCode.Right)
                {
                    GoTo(at + (e.Key.ForDirection(rightToLeft) == KeyCode.Right ? 1 : -1));
                    e.Handled = true;
                }
            },
            Children =
            [
                new Box
                {
                    Layout = new LayoutStyle { Height = Height, JustifyContent = Justify.Center },
                    Children =
                    [
                        new ScrollArea
                        {
                            Controller = scroll,
                            Behaviour = scroll.Behaviour,
                            IndicatorColor = default,
                            Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = Edges.All(0) },
                            ContentLayout = new LayoutStyle { FlexDirection = FlexDirection.Row, ColumnGap = gap },
                            Children = slides,
                        },
                        at <= 0 ? null : new IconButton("chevron_left", "Previous slide", IconButtonVariant.Tonal)
                        {
                            OnPress = () => GoTo(at - 1),
                            Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = new Edges(8, Dimension.Undefined, Dimension.Undefined, Dimension.Undefined) },
                        },
                        at >= count - 1 ? null : new IconButton("chevron_right", "Next slide", IconButtonVariant.Tonal)
                        {
                            OnPress = () => GoTo(at + 1),
                            Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = new Edges(Dimension.Undefined, Dimension.Undefined, 8, Dimension.Undefined) },
                        },
                    ],
                },
                count < 2 ? null : new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, JustifyContent = Justify.Center, ColumnGap = 2 },
                    Children = dots,
                },
            ],
        };
    }
}
