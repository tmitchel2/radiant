using System;
using System.Collections.Generic;
using System.Globalization;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A time of day, as a desktop app enters one: an outlined field whose hour and minute (and AM/PM
/// in 12-hour time) are separate parts. With a part focused, Up and Down step it (wrapping round),
/// typed digits set it, Left and Right move between parts, and Backspace clears the time. Each part
/// is announced with its value. Controlled: shows <paramref name="Value"/> and reports each change.
/// </summary>
/// <param name="Label">What the time is for.</param>
/// <param name="Value">The time, or null for none yet.</param>
/// <param name="OnChange">Called with the new time.</param>
public sealed record TimePicker(string Label, TimeOnly? Value, Action<TimeOnly?>? OnChange) : Component
{
    /// <summary>The culture whose AM and PM designators it shows and whose clock it follows; the current culture by default.</summary>
    public CultureInfo? Culture { get; init; }

    /// <summary>Whether it shows 24-hour time; by default, as <see cref="Culture"/> does.</summary>
    public bool? Use24Hour { get; init; }

    /// <summary>How far Up and Down move the minutes.</summary>
    public int MinuteStep { get; init; } = 1;

    /// <summary>Whether it can't be changed.</summary>
    public bool Disabled { get; init; }

    /// <summary>The picker's own layout, added to its default.</summary>
    public LayoutStyle? Layout { get; init; }

    private enum PartKind
    {
        Hour,
        Minute,
        Period,
    }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var rightToLeft = context.UseRightToLeft();
        var latest = context.UseRef(this);
        latest.Value = this;
        var refs = context.UseMemo(() => new[] { new ElementRef(), new ElementRef(), new ElementRef() }, default(ValueTuple));
        // A first digit typed into a part waits for a second (1 then 0 is ten, not one then zero).
        var pending = context.UseRef<(PartKind Part, int Digit)?>(null);
        var focused = context.UseState(false);
        var culture = Culture ?? CultureInfo.CurrentCulture;
        var h24 = Use24Hour ?? !culture.DateTimeFormat.ShortTimePattern.Contains('h', StringComparison.Ordinal);
        PartKind[] parts = h24 ? [PartKind.Hour, PartKind.Minute] : [PartKind.Hour, PartKind.Minute, PartKind.Period];

        void Set(TimeOnly? time)
        {
            if (time != latest.Value.Value)
            {
                latest.Value.OnChange?.Invoke(time);
            }
        }

        // With no time yet, a part's first change starts from 12:00 (noon), a neutral middle.
        TimeOnly Base() => latest.Value.Value ?? new TimeOnly(12, 0);

        void Step(PartKind part, int direction)
        {
            var time = Base();
            Set(part switch
            {
                PartKind.Hour => time.AddHours(direction),
                PartKind.Minute => time.AddMinutes(direction * Math.Max(1, latest.Value.MinuteStep)),
                _ => time.AddHours(12),
            });
        }

        void Type(PartKind part, int digit)
        {
            var time = Base();
            var first = pending.Value is { } p && p.Part == part ? p.Digit : (int?)null;
            var value = first is { } tens ? tens * 10 + digit : digit;
            var max = part == PartKind.Minute ? 59 : h24 ? 23 : 12;
            if (value > max)
            {
                value = digit;
                first = null;
            }
            if (part == PartKind.Hour)
            {
                var hour = h24 ? value : (value % 12) + (time.Hour >= 12 ? 12 : 0);
                Set(new TimeOnly(hour, time.Minute));
            }
            else
            {
                Set(new TimeOnly(time.Hour, value));
            }
            // A second digit, or one that can't start a two-digit value, finishes the part.
            var done = first is not null || value * 10 > max;
            pending.Value = done ? null : (part, digit);
            if (done)
            {
                Move(part, 1);
            }
        }

        void Move(PartKind part, int direction)
        {
            var at = Array.IndexOf(parts, part) + direction;
            if (at >= 0 && at < parts.Length)
            {
                refs[at].Focus();
            }
        }

        string Text(PartKind part) => Value is not { } time ? part == PartKind.Period ? "--" : "––" : part switch
        {
            PartKind.Hour => (h24 ? time.Hour : time.Hour % 12 == 0 ? 12 : time.Hour % 12).ToString(h24 ? "00" : "0", CultureInfo.InvariantCulture),
            PartKind.Minute => time.Minute.ToString("00", CultureInfo.InvariantCulture),
            _ => time.Hour < 12 ? culture.DateTimeFormat.AMDesignator is { Length: > 0 } am ? am : "AM"
                : culture.DateTimeFormat.PMDesignator is { Length: > 0 } pm ? pm : "PM",
        };

        var children = new List<Element?>();
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (i == 1)
            {
                children.Add(new SurfaceText(":") { TextType = TextType.BodyLarge });
            }
            children.Add(new PartView(Text(part), part switch { PartKind.Hour => "Hour", PartKind.Minute => "Minute", _ => "AM or PM" }, refs[i], Disabled)
            {
                Key = i,
                OnKey = e =>
                {
                    var key = e.Key.ForDirection(rightToLeft);
                    if (key is KeyCode.Up or KeyCode.Down)
                    {
                        Step(part, key == KeyCode.Up ? 1 : -1);
                    }
                    else if (key is KeyCode.Left or KeyCode.Right)
                    {
                        Move(part, key == KeyCode.Right ? 1 : -1);
                    }
                    else if (key is >= KeyCode.Number0 and <= KeyCode.Number9 && part != PartKind.Period)
                    {
                        Type(part, key - KeyCode.Number0);
                    }
                    else if (part == PartKind.Period && key is KeyCode.A or KeyCode.P)
                    {
                        var time = Base();
                        if (time.Hour >= 12 != (key == KeyCode.P))
                        {
                            Set(time.AddHours(12));
                        }
                    }
                    else if (key is KeyCode.Backspace or KeyCode.Delete)
                    {
                        Set(null);
                    }
                    else
                    {
                        return;
                    }
                    e.Handled = true;
                },
                OnFocusChange = has =>
                {
                    pending.Value = null;
                    focused.Set(has);
                },
            });
        }

        var outline = focused.Value ? theme.Get(SurfaceName.Primary) : theme.Outline;
        return new Box
        {
            Semantics = new Semantics { Role = SemanticsRole.Group, Label = Label, Value = Value?.ToString(h24 ? "HH:mm" : "h:mm tt", culture) },
            Layout = new LayoutStyle { AlignSelf = Align.FlexStart, Padding = new Edges(0, 8, 0, 0) }.Merge(Layout ?? default),
            Children =
            [
                new Box
                {
                    BorderWidth = focused.Value ? 2 : 1,
                    BorderColor = outline,
                    CornerRadii = theme.Corners(CornerShapeRole.ExtraSmall),
                    Layout = new LayoutStyle
                    {
                        FlexDirection = FlexDirection.Row,
                        AlignItems = Align.Center,
                        Height = 56 + theme.DensityOffset,
                        Padding = Edges.Symmetric(12, 0),
                        ColumnGap = 2,
                    },
                    Children = [.. children, new Box { Layout = new LayoutStyle { Width = 12 } }, new SurfaceIcon("schedule") { IconSize = 20, Legibility = Legibility.Medium }],
                },
                new Box
                {
                    HitTestVisible = false,
                    Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = new Edges(10, 0, Dimension.Undefined, Dimension.Undefined), Padding = Edges.Symmetric(4, 0) },
                    Background = theme.SurfaceColor(context.UseSurface()),
                    Children = [new SurfaceText(Label) { TextType = TextType.BodySmall, Legibility = focused.Value ? null : Legibility.Medium }],
                },
            ],
        };
    }

    /// <summary>One part of the time: its digits, highlighted while it has focus.</summary>
    private sealed record PartView(string Text, string Name, ElementRef Ref, bool Disabled) : Component
    {
        public Action<KeyEventArgs>? OnKey { get; init; }

        public Action<bool>? OnFocusChange { get; init; }

        public override Element? Build(BuildContext context)
        {
            var theme = context.UseTheme();
            var focus = context.UseState(false);
            var (onKey, onFocus) = (OnKey, OnFocusChange);
            var state = context.UseSurface().With(new SurfaceChange { Surface = focus.Value ? SurfaceName.Primary : null, ToggleSurfaceContainer = focus.Value });
            return ThemeContexts.Surface.Provide(state, new Box
            {
                Ref = Ref,
                Focusable = !Disabled,
                Semantics = new Semantics { Role = SemanticsRole.Slider, Label = Name, Value = Text, Disabled = Disabled },
                Background = focus.Value ? theme.SurfaceColor(state) : null,
                CornerRadii = theme.Corners(CornerShapeRole.ExtraSmall),
                Layout = new LayoutStyle { Padding = Edges.Symmetric(4, 2), MinWidth = 28, AlignItems = Align.Center },
                OnPointerDown = _ => Ref.Focus(visible: false),
                OnKeyDown = e => onKey?.Invoke(e),
                OnFocus = _ =>
                {
                    focus.Set(true);
                    onFocus?.Invoke(true);
                },
                OnBlur = _ =>
                {
                    focus.Set(false);
                    onFocus?.Invoke(false);
                },
                Children = [new SurfaceText(Text) { TextType = TextType.BodyLarge, Legibility = Disabled ? Legibility.Low : null }],
            });
        }
    }
}
