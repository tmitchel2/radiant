using System;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A header that shows and hides what's under it ("Advanced options"): pressing it (or Enter or
/// Space) toggles the content, and it's announced as expanded or collapsed. For several sections
/// that work together, use an <see cref="Accordion"/>. Open by itself unless <see cref="Open"/> is
/// set, when its owner keeps it through <see cref="OnOpenChange"/>.
/// </summary>
/// <param name="Title">The header.</param>
/// <param name="Content">What it shows when open.</param>
public sealed record Collapsible(string Title, Element? Content) : Component
{
    /// <summary>Whether it's open, when the owner keeps it; null for it to keep itself.</summary>
    public bool? Open { get; init; }

    /// <summary>Whether it starts open, when it keeps itself.</summary>
    public bool InitiallyOpen { get; init; }

    /// <summary>Called with whether it's now open.</summary>
    public Action<bool>? OnOpenChange { get; init; }

    /// <summary>An icon before the title.</summary>
    public string? Icon { get; init; }

    /// <summary>The collapsible's own layout, added to its default.</summary>
    public LayoutStyle? Layout { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var own = context.UseState(InitiallyOpen);
        var open = Open ?? own.Value;
        var onChange = OnOpenChange;
        var controlled = Open is not null;
        return new Box
        {
            Layout = new LayoutStyle { AlignSelf = Align.Stretch }.Merge(Layout ?? default),
            Children =
            [
                new PressableSurface
                {
                    InsetFocusRing = true,
                    Label = Title,
                    Expanded = open,
                    CornerShape = CornerShapeRole.Small,
                    OnPress = () =>
                    {
                        if (!controlled)
                        {
                            own.Set(!open);
                        }
                        onChange?.Invoke(!open);
                    },
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 8, Height = 40, Padding = Edges.Symmetric(8, 0) },
                    Children =
                    [
                        new SurfaceIcon(open ? "expand_more" : "chevron_right") { IconSize = 20, Legibility = Legibility.Medium },
                        Icon is null ? null : new SurfaceIcon(Icon) { IconSize = 20 },
                        new SurfaceText(Title) { TextType = TextType.TitleSmall, MaxLines = 1 },
                    ],
                },
                open ? new Box { Layout = new LayoutStyle { Padding = new Edges(36, 4, 8, 8) }, Children = [Content] } : null,
            ],
        };
    }
}
