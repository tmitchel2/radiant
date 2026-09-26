using System;
using System.Numerics;
using Radiant.Components.Primitives;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A modal panel that slides in from an edge of the window over a dimmed app (filters, details,
/// a form): a title with a close button over its content. Focus stays inside while it's open;
/// Escape, the close button or a press on the scrim close it, and focus goes back.
/// </summary>
/// <param name="Open">Whether it's showing.</param>
/// <param name="OnClose">Called when it should close.</param>
/// <param name="Content">What it shows.</param>
public sealed record Sheet(bool Open, Action OnClose, Element? Content) : Component
{
    /// <summary>The edge it slides in from.</summary>
    public SheetSide Side { get; init; }

    /// <summary>Its width (from the side) or height (from the bottom).</summary>
    public float Size { get; init; } = 400f;

    /// <summary>The title at its top.</summary>
    public string? Title { get; init; }

    /// <summary>Buttons along its bottom.</summary>
    public System.Collections.Generic.IReadOnlyList<Element?> Actions { get; init; } = [];

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var state = context.UseSurface().With(new SurfaceChange { Surface = SurfaceName.SurfaceContainerLow });
        var props = this;
        var close = OnClose;
        var bottom = Side == SheetSide.Bottom;
        var start = Side == SheetSide.Start;
        // The start is the left unless the UI reads right to left.
        var left = start != context.UseRightToLeft();
        var corner = theme.Radius(CornerShapeRole.Large);
        // Rounded only on the side facing the app.
        var radii = bottom ? Radiant.Graphics2D.CornerRadii.Top(corner)
            : left ? new Radiant.Graphics2D.CornerRadii(0, corner, corner, 0)
            : new Radiant.Graphics2D.CornerRadii(corner, 0, 0, corner);
        // The overlay primitives each wrap the panel in a box. The outer one stretches across the
        // edge (the window's height for a side sheet); the inner, in the outer's column, grows down it.
        var outer = new LayoutStyle { AlignSelf = Align.Stretch };
        var inner = bottom ? default : new LayoutStyle { FlexGrow = 1 };
        return new Presence(Open, progress =>
        {
            // Slides the rest of the way in as it appears: its whole size when hidden.
            var hidden = (1f - progress) * props.Size;
            var shift = bottom ? new Vector2(0, hidden) : new Vector2(left ? -hidden : hidden, 0);
            return new Portal(new Box
            {
                Layout = new LayoutStyle
                {
                    Position = PositionType.Absolute,
                    Inset = Edges.All(0),
                    FlexDirection = bottom ? FlexDirection.Column : FlexDirection.Row,
                    JustifyContent = start ? Justify.FlexStart : Justify.FlexEnd,
                },
                Background = theme.Scrim with { A = 0.32f * progress },
                Children =
                [
                    new DismissableLayer(new FocusScope(new Box
                    {
                        Semantics = new Semantics { Role = SemanticsRole.Dialog, Label = props.Title },
                        Transform = Matrix3x2.CreateTranslation(shift),
                        Background = theme.SurfaceColor(state),
                        CornerRadii = radii,
                        Shadows = theme.Elevation(ElevationLevel.Level1),
                        Layout = new LayoutStyle
                        {
                            Width = bottom ? Dimension.Undefined : props.Size,
                            Height = bottom ? props.Size : Dimension.Undefined,
                            FlexGrow = bottom ? 0 : 1,
                            AlignSelf = Align.Stretch,
                        },
                        Children =
                        [
                            ThemeContexts.Surface.Provide(state, new Box
                            {
                                Layout = new LayoutStyle { FlexGrow = 1 },
                                Children =
                                [
                                    new Box
                                    {
                                        Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, Padding = new Edges(24, 16, 12, 8), MinHeight = 64 },
                                        Children =
                                        [
                                            new SurfaceText(props.Title) { TextType = TextType.TitleLarge, HeadingLevel = 1, Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 } },
                                            new IconButton("close", "Close") { OnPress = close },
                                        ],
                                    },
                                    new ScrollArea
                                    {
                                        Layout = new LayoutStyle { FlexGrow = 1 },
                                        ContentLayout = new LayoutStyle { Padding = new Edges(24, 0, 24, 24) },
                                        Children = [props.Content],
                                    },
                                    props.Actions.Count == 0 ? null : new Box
                                    {
                                        Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, JustifyContent = Justify.FlexEnd, ColumnGap = 8, Padding = Edges.All(16) },
                                        Children = props.Actions,
                                    },
                                ],
                            }),
                        ],
                    }) { Layout = inner }, close) { Layout = outer },
                ],
            });
        }) { Duration = TimeSpan.FromMilliseconds(250) };
    }
}
