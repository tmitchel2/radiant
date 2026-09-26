using System;
using System.Globalization;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A field for a number: type it (read in the culture's format when the field is left or Enter is
/// pressed), or step it with the arrows beside it, Up and Down, or Page Up and Down for ten steps.
/// Values are kept within <see cref="Min"/> and <see cref="Max"/> and rounded to
/// <see cref="Decimals"/>. Text that isn't a number puts the value back. Controlled: shows
/// <paramref name="Value"/> and reports each change.
/// </summary>
/// <param name="Label">The field's label.</param>
/// <param name="Value">The number.</param>
/// <param name="OnChange">Called with the new number.</param>
[RequiresTestId]
public sealed partial record NumberField(string Label, double Value, Action<double>? OnChange) : Component
{
    [TestId<TextField>] public static partial string Field { get; }
    [TestId<IconButton>] public static partial string Increase { get; }
    [TestId<IconButton>] public static partial string Decrease { get; }

    /// <summary>The least value.</summary>
    public double Min { get; init; } = double.MinValue;

    /// <summary>The greatest value.</summary>
    public double Max { get; init; } = double.MaxValue;

    /// <summary>How much the arrows and keys change it.</summary>
    public double Step { get; init; } = 1;

    /// <summary>The decimal places shown and kept.</summary>
    public int Decimals { get; init; }

    /// <summary>Text after the number ("px", "%").</summary>
    public string? Suffix { get; init; }

    /// <summary>How numbers are written and read; the current culture if null.</summary>
    public CultureInfo? Culture { get; init; }

    /// <summary>Filled or outlined, as <see cref="TextField"/>.</summary>
    public TextFieldVariant Variant { get; init; }

    /// <summary>The field's size and placement.</summary>
    public LayoutStyle? Layout { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var culture = Culture ?? CultureInfo.CurrentCulture;
        var format = "F" + Math.Clamp(Decimals, 0, 10).ToString(CultureInfo.InvariantCulture);
        string Show(double value) => value.ToString(format, culture);
        var text = context.UseState(() => TextEditState.From(Show(Value)));
        var latest = context.UseRef(this);
        latest.Value = this;

        var value = Value;
        context.UseEffect(() =>
        {
            text.Set(TextEditState.From(Show(value)));
            return null;
        }, value);

        double Tidy(double number)
        {
            var props = latest.Value;
            return Math.Round(Math.Clamp(number, props.Min, props.Max), Math.Clamp(props.Decimals, 0, 10), MidpointRounding.AwayFromZero);
        }

        void Set(double number)
        {
            var props = latest.Value;
            var next = Tidy(number);
            text.Set(TextEditState.From(Show(next)));
            if (next != props.Value)
            {
                props.OnChange?.Invoke(next);
            }
        }

        // Reads the typed text; anything that isn't a number puts the value back.
        void Commit()
        {
            var typed = text.Value.Text.Trim();
            if (latest.Value.Suffix is { } suffix && typed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                typed = typed[..^suffix.Length].Trim();
            }
            if (double.TryParse(typed, NumberStyles.Float | NumberStyles.AllowThousands, culture, out var number))
            {
                Set(number);
            }
            else
            {
                text.Set(TextEditState.From(Show(latest.Value.Value)));
            }
        }

        void StepBy(double steps) => Set(latest.Value.Value + steps * latest.Value.Step);

        var stepper = new Box
        {
            Layout = new LayoutStyle { Width = 28 },
            Children =
            [
                new IconButton("expand_less", $"Increase {Label}") { TestId = Increase, OnPress = Value < Max ? () => StepBy(1) : null, ShowDisabled = Value >= Max ? true : null, Layout = new LayoutStyle { Width = 28, Height = 22 } },
                new IconButton("expand_more", $"Decrease {Label}") { TestId = Decrease, OnPress = Value > Min ? () => StepBy(-1) : null, ShowDisabled = Value <= Min ? true : null, Layout = new LayoutStyle { Width = 28, Height = 22 } },
            ],
        };

        return new Box
        {
            Semantics = new Semantics { Role = SemanticsRole.Slider, Label = Label, Value = Show(Value) + (Suffix is null ? "" : " " + Suffix) },
            Layout = new LayoutStyle { AlignSelf = Align.Stretch }.Merge(Layout ?? default),
            OnKeyDownCapture = e =>
            {
                var handled = true;
                switch (e.Key)
                {
                    case KeyCode.Up: StepBy(1); break;
                    case KeyCode.Down: StepBy(-1); break;
                    case KeyCode.PageUp: StepBy(10); break;
                    case KeyCode.PageDown: StepBy(-10); break;
                    case KeyCode.Enter: Commit(); break;
                    default: handled = false; break;
                }
                e.Handled |= handled;
            },
            Children =
            [
                new TextField(Label)
                {
                    TestId = Field,
                    Value = text.Value,
                    OnChange = text.Set,
                    Variant = Variant,
                    // Numbers are short: the field takes the number field's width, not a text field's minimum.
                    Layout = new LayoutStyle { MinWidth = 0 },
                    Trailing = Suffix is null ? stepper : new Box
                    {
                        Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 4 },
                        Children = [new SurfaceText(Suffix) { Legibility = Legibility.Medium }, stepper],
                    },
                    OnFocusChange = focused =>
                    {
                        if (!focused)
                        {
                            Commit();
                        }
                    },
                },
            ],
        };
    }
}
