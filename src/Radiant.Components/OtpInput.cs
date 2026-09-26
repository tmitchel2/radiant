using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A one-time code's boxes (six digits from an authenticator app or a text message): type or paste
/// the code, and each character lands in its box, the next box ringed; non-digits are ignored
/// (unless <see cref="Digits"/> is off) and Backspace goes back a box. Called with the code once
/// it's whole.
/// </summary>
/// <param name="OnComplete">Called with the whole code.</param>
[RequiresTestId]
public sealed record OtpInput(Action<string> OnComplete) : Component
{
    /// <summary>How many characters the code has.</summary>
    public int Length { get; init; } = 6;

    /// <summary>Whether only digits are accepted.</summary>
    public bool Digits { get; init; } = true;

    /// <summary>What assistive technology calls it.</summary>
    public string Label { get; init; } = "Verification code";

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var surface = context.UseSurface();
        var code = context.UseState(TextEditState.Empty);
        var focused = context.UseState(false);
        var input = context.UseRef(new ElementRef()).Value;
        var (length, digits, complete) = (Length, Digits, OnComplete);
        var text = code.Value.Text;

        void Change(TextEditState next)
        {
            var kept = new string([.. next.Text.Where(c => digits ? char.IsAsciiDigit(c) : !char.IsWhiteSpace(c)).Take(length)]);
            var before = code.Value.Text;
            code.Set(TextEditState.From(kept));
            if (kept.Length == length && kept != before)
            {
                complete(kept);
            }
        }

        var boxes = new List<Element?>();
        for (var i = 0; i < length; i++)
        {
            var current = focused.Value && i == Math.Min(text.Length, length - 1);
            boxes.Add(new Box
            {
                HitTestVisible = false,
                Layout = new LayoutStyle { Width = 44, Height = 52, AlignItems = Align.Center, JustifyContent = Justify.Center },
                Background = theme.Get(SurfaceName.SurfaceContainerHighest),
                BorderWidth = current ? 2f : 1f,
                BorderColor = current ? theme.Get(SurfaceName.Primary) : theme.Outline,
                CornerRadii = theme.Corners(CornerShapeRole.Small),
                Children = [i < text.Length ? new SurfaceText(text[i].ToString()) { TextType = TextType.HeadlineSmall } : null],
            });
        }

        var clear = new Vector4(0, 0, 0, 0);
        return new Box
        {
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, ColumnGap = 8, AlignSelf = Align.FlexStart },
            OnClick = _ => input.Focus(visible: false),
            Children =
            [
                .. boxes,
                // The typing goes into one input over all the boxes, drawn invisibly: the boxes show it.
                new TextInput(code.Value, Change)
                {
                    Ref = input,
                    Label = Label,
                    Style = theme.Text(TextType.BodyLarge) with { Color = clear },
                    CaretColor = clear,
                    SelectionColor = clear,
                    OnFocusChange = focused.Set,
                    Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = Edges.All(0) },
                },
            ],
        };
    }
}
