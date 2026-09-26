using System;
using Radiant.Platform;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// Text that goes somewhere: in the primary colour, underlined under the pointer or keyboard focus,
/// with a pointing-hand cursor. A link to assistive technology.
/// </summary>
/// <param name="Text">What it says.</param>
/// <param name="OnPress">Where pressing it goes.</param>
public sealed record Link(string Text, Action? OnPress) : Component
{
    /// <summary>The text's type-scale step.</summary>
    public TextType TextType { get; init; } = TextType.BodyMedium;

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var hovered = context.UseState(false);
        var ring = context.UseState(false);
        var press = OnPress;
        var color = theme.ContentColor(context.UseSurface().With(new SurfaceChange { Content = SurfaceName.Primary }));
        var style = theme.Text(TextType) with { Color = color };
        return new Box
        {
            Focusable = true,
            Cursor = CursorShape.PointingHand,
            Semantics = new Semantics { Role = SemanticsRole.Link, Label = Text },
            OnPointerEnter = _ => hovered.Set(true),
            OnPointerLeave = _ => hovered.Set(false),
            OnFocus = e => ring.Set(e.IsFocusVisible),
            OnBlur = _ => ring.Set(false),
            OnClick = e =>
            {
                press?.Invoke();
                e.Handled = true;
            },
            OnKeyDown = e =>
            {
                if (e.Key == KeyCode.Enter)
                {
                    press?.Invoke();
                    e.Handled = true;
                }
            },
            Children =
            [
                new TextBlock(Text) { Style = style, IsDecorative = true },
                // The underline: a 1 px line under the text while hovered or focused.
                hovered.Value || ring.Value ? new Box
                {
                    HitTestVisible = false,
                    Layout = new Radiant.Layout.LayoutStyle { Height = 1, Margin = new Radiant.Layout.Edges(0, -2, 0, 1) },
                    Background = color,
                } : null,
            ],
        };
    }
}
