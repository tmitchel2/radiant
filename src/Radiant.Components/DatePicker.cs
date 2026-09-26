using System;
using System.Globalization;
using Radiant.Components.Primitives;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A date field: type a date in the culture's short format, or open the calendar from the field's
/// button (or with Down) and pick one. Typed text is read when the field is left or Enter is
/// pressed; text that isn't a date, or a date that can't be chosen, shows an error until fixed.
/// Controlled: shows <paramref name="Value"/> and reports each change.
/// </summary>
/// <param name="Label">The field's label.</param>
/// <param name="Value">The date, or null for none.</param>
/// <param name="OnChange">Called with the new date (null when the field is cleared).</param>
[RequiresTestId]
public sealed partial record DatePicker(string Label, DateOnly? Value, Action<DateOnly?>? OnChange) : Component
{
    [TestId<TextField>] public static partial string Field { get; }

    /// <summary>The earliest date that can be chosen.</summary>
    public DateOnly? Min { get; init; }

    /// <summary>The latest date that can be chosen.</summary>
    public DateOnly? Max { get; init; }

    /// <summary>Dates that can't be chosen besides those outside <see cref="Min"/> and <see cref="Max"/>.</summary>
    public Func<DateOnly, bool>? IsDisabled { get; init; }

    /// <summary>Today, ringed in the calendar; the local date if null.</summary>
    public DateOnly? Today { get; init; }

    /// <summary>How dates are written and read, and the calendar's names; the current culture if null.</summary>
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
        var format = culture.DateTimeFormat.ShortDatePattern;
        string Show(DateOnly? date) => date?.ToString(format, culture) ?? "";
        var text = context.UseState(() => TextEditState.From(Show(Value)));
        var error = context.UseState((string?)null);
        var open = context.UseState(false);
        var anchor = context.UseRef(new ElementRef()).Value;
        var input = context.UseRef(new ElementRef()).Value;
        var latest = context.UseRef(this);
        latest.Value = this;

        var value = Value;
        context.UseEffect(() =>
        {
            text.Set(TextEditState.From(Show(value)));
            error.Set(null);
            return null;
        }, value);

        var calendar = new Calendar(Value, day =>
        {
            open.Set(false);
            text.Set(TextEditState.From(Show(day)));
            error.Set(null);
            latest.Value.OnChange?.Invoke(day);
            input.Focus(visible: false);
        })
        {
            Min = Min,
            Max = Max,
            IsDisabled = IsDisabled,
            Today = Today,
            Culture = culture,
            AutoFocus = true,
        };

        // Reads the typed text: empty clears, a date that can be chosen is taken, anything else is an error.
        void Commit()
        {
            var props = latest.Value;
            var typed = text.Value.Text.Trim();
            if (typed.Length == 0)
            {
                error.Set(null);
                if (props.Value is not null)
                {
                    props.OnChange?.Invoke(null);
                }
                return;
            }
            if (!DateOnly.TryParse(typed, culture, DateTimeStyles.AllowWhiteSpaces, out var date))
            {
                error.Set($"Enter a date like {new DateOnly(2026, 12, 31).ToString(format, culture)}");
                return;
            }
            if (!calendar.CanChoose(date))
            {
                error.Set("That date can't be chosen");
                return;
            }
            error.Set(null);
            text.Set(TextEditState.From(Show(date)));
            if (date != props.Value)
            {
                props.OnChange?.Invoke(date);
            }
        }

        return new Fragment(
            new Box
            {
                Ref = anchor,
                Layout = new LayoutStyle { AlignSelf = Align.Stretch }.Merge(Layout ?? default),
                OnKeyDownCapture = e =>
                {
                    if (e.Key == KeyCode.Down && !open.Value)
                    {
                        open.Set(true);
                        e.Handled = true;
                    }
                    else if (e.Key == KeyCode.Enter && !open.Value)
                    {
                        Commit();
                        e.Handled = true;
                    }
                },
                Children =
                [
                    new TextField(Label)
                    {
                        TestId = Field,
                        Value = text.Value,
                        OnChange = text.Set,
                        InputRef = input,
                        Variant = Variant,
                        Placeholder = format.ToUpperInvariant(),
                        Error = error.Value,
                        TrailingIcon = "calendar_today",
                        TrailingIconLabel = $"Choose {Label}",
                        OnTrailingIconPress = () => open.Set(!open.Value),
                        OnFocusChange = focused =>
                        {
                            if (!focused && !open.Value)
                            {
                                Commit();
                            }
                        },
                    },
                ],
            },
            new Presence(open.Value, progress => new Anchored(anchor, new DismissableLayer(new FocusScope(new Surface
            {
                SurfaceColor = SurfaceName.SurfaceContainerHigh,
                CornerShape = CornerShapeRole.Large,
                Elevation = ElevationLevel.Level3,
                Semantics = new Semantics { Role = SemanticsRole.Dialog, Label = Label },
                Children = [new Box { Opacity = progress, Children = [calendar] }],
            }) { AutoFocus = false, RestoreFocus = false }, () => open.Set(false)))
            { Offset = 4f }));
    }
}
