using System;
using System.Collections.Generic;
using System.Numerics;
using Radiant.Components.Primitives;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A modal dialog: it dims the app behind a scrim, keeps focus inside itself, closes on Escape or
/// a press on the scrim (unless <see cref="Dismissible"/> is off), gives focus back when it goes,
/// and fades and scales in and out.
/// </summary>
/// <param name="Open">Whether it's showing.</param>
/// <param name="OnClose">Called when it should close.</param>
public sealed partial record Dialog(bool Open, Action OnClose) : Component
{
    [TestId] public static partial string Panel { get; }
    [TestId] public static partial string Heading { get; }
    [TestId] public static partial string ActionRow { get; }

    /// <summary>An icon above the title.</summary>
    public string? Icon { get; init; }

    /// <summary>The title.</summary>
    public string? Title { get; init; }

    /// <summary>The body text.</summary>
    public string? Text { get; init; }

    /// <summary>Content under the text.</summary>
    public IReadOnlyList<Element?> Content { get; init; } = [];

    /// <summary>The buttons, right-aligned at the bottom.</summary>
    public IReadOnlyList<Element?> Actions { get; init; } = [];

    /// <summary>Whether Escape and the scrim close it; off for a decision the user must make.</summary>
    public bool Dismissible { get; init; } = true;

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var style = theme.Theme.Components.Overlay;
        var close = OnClose;
        var props = this;
        return new Presence(Open, progress => new Portal(new Box
        {
            Layout = new LayoutStyle
            {
                Position = PositionType.Absolute,
                Inset = Edges.All(0),
                AlignItems = Align.Center,
                JustifyContent = Justify.Center,
                Padding = Edges.All(24),
            },
            Background = theme.Scrim with { A = style.ScrimOpacity * progress },
            Children =
            [
                new DismissableLayer(new FocusScope(SurfaceLooks.Surface(style.Dialog) with
                {
                    TestId = Panel,
                    CornerShape = style.DialogShape,
                    Semantics = new Semantics { Role = SemanticsRole.Dialog, Label = props.Title },
                    Layout = new LayoutStyle { MinWidth = 280, MaxWidth = 560, Padding = Edges.All(style.DialogPadding), RowGap = 16 },
                    Children =
                    [
                        new Box
                        {
                            Opacity = progress,
                            Transform = Matrix3x2.CreateScale(0.95f + 0.05f * progress),
                            Layout = new LayoutStyle { RowGap = 16 },
                            Children =
                            [
                                props.Icon is null ? null : new SurfaceIcon(props.Icon) { Layout = new LayoutStyle { AlignSelf = Align.Center } },
                                props.Title is null ? null : new SurfaceText(props.Title)
                                {
                                    TestId = Heading,
                                    TextType = style.DialogTitle,
                                    Alignment = props.Icon is null ? default : Radiant.Text.TextAlignment.Center,
                                },
                                props.Text is null ? null : new SurfaceText(props.Text) { Legibility = Legibility.Medium },
                                .. props.Content,
                                props.Actions.Count == 0 ? null : new Box
                                {
                                    TestId = ActionRow,
                                    Layout = new LayoutStyle
                                    {
                                        FlexDirection = FlexDirection.Row,
                                        JustifyContent = Justify.FlexEnd,
                                        ColumnGap = 8,
                                        Margin = new Edges(0, 8, 0, 0),
                                    },
                                    Children = props.Actions,
                                },
                            ],
                        },
                    ],
                }), close) { DismissOnOutsidePress = props.Dismissible, DismissOnEscape = props.Dismissible },
            ],
        }));
    }
}
