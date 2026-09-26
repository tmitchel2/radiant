using System;
using System.Collections.Generic;
using System.Globalization;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A month of days to pick one from: the month's name with buttons to the previous and next
/// month, the weekdays, and the days in a grid, today ringed and the chosen day filled.
/// <list type="bullet">
/// <item>With a day focused, arrows move a day or a week, Page Up and Down a month, Home and
/// End to the week's ends, and Enter or Space choose it; moving past the month turns to the next.
/// Only one day is a Tab stop.</item>
/// <item>Days before <see cref="Min"/>, after <see cref="Max"/> or refused by
/// <see cref="IsDisabled"/> can't be chosen.</item>
/// </list>
/// Controlled: shows <paramref name="Selected"/> and reports each choice.
/// </summary>
/// <param name="Selected">The chosen day, or null.</param>
/// <param name="OnSelect">Called with the chosen day.</param>
public sealed partial record Calendar(DateOnly? Selected, Action<DateOnly>? OnSelect) : Component
{
    [TestId] public static partial string DayButton { get; }

    [TestId<IconButton>] public static partial string PreviousMonth { get; }
    [TestId<IconButton>] public static partial string NextMonth { get; }

    private const float Cell = 40f;

    /// <summary>The earliest day that can be chosen.</summary>
    public DateOnly? Min { get; init; }

    /// <summary>The latest day that can be chosen.</summary>
    public DateOnly? Max { get; init; }

    /// <summary>Days that can't be chosen besides those outside <see cref="Min"/> and <see cref="Max"/>.</summary>
    public Func<DateOnly, bool>? IsDisabled { get; init; }

    /// <summary>Today, ringed; the local date if null.</summary>
    public DateOnly? Today { get; init; }

    /// <summary>The month shown at first; the chosen day's, or today's, if null.</summary>
    public DateOnly? InitialMonth { get; init; }

    /// <summary>The names of months and days, and the first day of the week; the current culture if null.</summary>
    public CultureInfo? Culture { get; init; }

    /// <summary>Whether the focused day (the chosen one, or today) takes keyboard focus when the calendar appears.</summary>
    public bool AutoFocus { get; init; }

    /// <summary>Whether a day can be chosen.</summary>
    public bool CanChoose(DateOnly day) =>
        (Min is not { } min || day >= min) && (Max is not { } max || day <= max) && IsDisabled?.Invoke(day) != true;

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rightToLeft = context.UseRightToLeft();
        var theme = context.UseTheme();
        var culture = Culture ?? CultureInfo.CurrentCulture;
        var today = Today ?? DateOnly.FromDateTime(DateTime.Now);
        var start = InitialMonth ?? Selected ?? today;
        var focus = context.UseState(start);
        var refs = context.UseRef(new Dictionary<int, ElementRef>()).Value;
        var moved = context.UseRef(AutoFocus);
        var latest = context.UseRef(this);
        latest.Value = this;

        var focused = focus.Value;
        var month = new DateOnly(focused.Year, focused.Month, 1);
        var days = DateTime.DaysInMonth(month.Year, month.Month);
        var firstDay = culture.DateTimeFormat.FirstDayOfWeek;
        var lead = ((int)month.DayOfWeek - (int)firstDay + 7) % 7;

        // After a keyboard move, focus follows to the new day once it's built.
        context.UseEffect(() =>
        {
            if (moved.Value && refs.TryGetValue(focused.Day, out var reference))
            {
                moved.Value = false;
                reference.Focus();
            }
            return null;
        });

        void MoveTo(DateOnly day)
        {
            focus.Set(day);
            moved.Value = true;
        }

        void Choose(DateOnly day)
        {
            if (latest.Value.CanChoose(day))
            {
                focus.Set(day);
                latest.Value.OnSelect?.Invoke(day);
            }
        }

        void Key(KeyEventArgs e)
        {
            var at = focus.Value;
            DateOnly? next = e.Key.ForDirection(rightToLeft) switch
            {
                KeyCode.Left => at.AddDays(-1),
                KeyCode.Right => at.AddDays(1),
                KeyCode.Up => at.AddDays(-7),
                KeyCode.Down => at.AddDays(7),
                KeyCode.PageUp => at.AddMonths(-1),
                KeyCode.PageDown => at.AddMonths(1),
                KeyCode.Home => at.AddDays(-(((int)at.DayOfWeek - (int)firstDay + 7) % 7)),
                KeyCode.End => at.AddDays(6 - ((int)at.DayOfWeek - (int)firstDay + 7) % 7),
                _ => null,
            };
            if (next is { } day)
            {
                MoveTo(day);
                e.Handled = true;
            }
            else if (e.Key is KeyCode.Enter or KeyCode.Space)
            {
                Choose(at);
                e.Handled = true;
            }
        }

        var weekdays = new List<Element?>();
        for (var i = 0; i < 7; i++)
        {
            var weekday = (DayOfWeek)(((int)firstDay + i) % 7);
            weekdays.Add(new SurfaceText(culture.DateTimeFormat.GetShortestDayName(weekday))
            {
                TextType = TextType.BodySmall,
                Legibility = Legibility.Medium,
                Alignment = Radiant.Text.TextAlignment.Center,
                Layout = new LayoutStyle { Width = Cell },
            });
        }

        var cells = new List<Element?>();
        for (var i = 0; i < lead; i++)
        {
            cells.Add(new Box { Layout = new LayoutStyle { Width = Cell, Height = Cell } });
        }
        for (var d = 1; d <= days; d++)
        {
            var day = new DateOnly(month.Year, month.Month, d);
            if (!refs.TryGetValue(d, out var reference))
            {
                refs[d] = reference = new ElementRef();
            }
            cells.Add(new Day(day, day == Selected, day == today, day == focused, CanChoose(day), reference, culture, () => Choose(day)) { Key = d });
        }

        var title = month.ToString("Y", culture);
        return new Box
        {
            Semantics = new Semantics { Role = SemanticsRole.Group, Label = title },
            Layout = new LayoutStyle { Width = Cell * 7 + 24, Padding = Edges.All(12), RowGap = 4 },
            OnKeyDown = Key,
            Children =
            [
                new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, Padding = new Edges(12, 0, 0, 4) },
                    Children =
                    [
                        new SurfaceText(title) { TextType = TextType.TitleSmall, HeadingLevel = 2, Layout = new LayoutStyle { FlexGrow = 1 } },
                        new IconButton("chevron_left", "Previous month") { TestId = PreviousMonth, OnPress = () => focus.Set(focus.Value.AddMonths(-1)) },
                        new IconButton("chevron_right", "Next month") { TestId = NextMonth, OnPress = () => focus.Set(focus.Value.AddMonths(1)) },
                    ],
                },
                new Box { Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, Height = 32, AlignItems = Align.Center }, Children = weekdays },
                new Box { Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, FlexWrap = FlexWrap.Wrap, Width = Cell * 7 }, Children = cells },
            ],
        };
    }

    /// <summary>A day: a circle, filled when chosen, ringed when today, faded when it can't be chosen.</summary>
    private sealed record Day(DateOnly Date, bool Chosen, bool IsToday, bool IsFocusTarget, bool Enabled, ElementRef Ref, CultureInfo Culture, Action Choose) : Component
    {
        public override Element? Build(BuildContext context)
        {
            return new Box
            {
                Ref = Ref,
                Layout = new LayoutStyle { Width = Cell, Height = Cell, AlignItems = Align.Center, JustifyContent = Justify.Center },
                Children =
                [
                    new PressableSurface
                    {
                        TestId = DayButton,
                        SurfaceColor = Chosen ? SurfaceName.Primary : null,
                        ContentColor = !Chosen && IsToday ? SurfaceName.Primary : null,
                        ShowOutline = !Chosen && IsToday ? true : null,
                        OutlineInContentColor = !Chosen && IsToday ? true : null,
                        ShowDisabled = Enabled ? null : true,
                        CornerShape = CornerShapeRole.Full,
                        Label = Date.ToString("D", Culture),
                        Selected = Chosen,
                        // Only the focused day is a Tab stop; arrows move between days.
                        TabIndex = IsFocusTarget ? 0 : -1,
                        OnPress = Choose,
                        Layout = new LayoutStyle { Width = 36, Height = 36, AlignItems = Align.Center, JustifyContent = Justify.Center },
                        Children = [new SurfaceText(Date.Day.ToString(Culture)) { TextType = TextType.BodyMedium }],
                    },
                ],
            };
        }
    }
}
