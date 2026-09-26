using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;
using Color = Radiant.Graphics2D.Color;

namespace Radiant.Gallery.ThemeLab;

/// <summary>
/// Makes the editor's rows: each reads its value from the theme through a lens and writes changes
/// back through it. Rows share their controls' test IDs, and are told apart by their section and name.
/// </summary>
/// <param name="theme">The theme being edited.</param>
/// <param name="edit">Applies a change to it.</param>
internal sealed class ThemeFields(Theme theme, ThemeEdit edit)
{
    public Theme Theme => theme;

    /// <summary>A number on a slider, with its value beside it.</summary>
    public PropertyItem Float(string name, ThemeLens<float> value, float min, float max, float step = 1f, string format = "0") =>
        new(name, Slider(name, value.Read(theme), min, max, step, format, v => edit(t => value.Write(t, v), false)));

    /// <summary>A number that can be left unset, when the component works it out: a switch, and a slider when it's on.</summary>
    public PropertyItem OptionalFloat(string name, ThemeLens<float?> value, float min, float max, float fallback, float step = 1f, string format = "0")
    {
        var current = value.Read(theme);
        return new PropertyItem(name, new Box
        {
            Layout = new LayoutStyle { AlignSelf = Align.Stretch, FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 8 },
            Children =
            [
                new Switch(current is not null, on => edit(t => value.Write(t, on ? fallback : null), true)) { TestId = ThemeEditor.Toggle, AccessibleLabel = $"Set {name}" },
                current is { } set ? Slider(name, set, min, max, step, format, v => edit(t => value.Write(t, v), false)) : new SurfaceText("Automatic") { Legibility = Legibility.Medium },
            ],
        });
    }

    public PropertyItem Bool(string name, ThemeLens<bool> value) =>
        new(name, new Switch(value.Read(theme), on => edit(t => value.Write(t, on), true)) { TestId = ThemeEditor.Toggle });

    /// <summary>One of some options, by index.</summary>
    public PropertyItem Choice(string name, IReadOnlyList<string> options, ThemeLens<int> index) =>
        new(name, new ChoiceButton(name, options, index.Read(theme), i => edit(t => index.Write(t, i), true)) { TestId = ThemeEditor.Choice });

    /// <summary>One of an enum's values, named in words.</summary>
    public PropertyItem Enum<TEnum>(string name, ThemeLens<TEnum> value)
        where TEnum : struct, System.Enum
    {
        var values = System.Enum.GetValues<TEnum>();
        return Choice(name, Names(values), value.Map(v => Array.IndexOf(values, v), i => values[i]));
    }

    /// <summary>One of an enum's values, or none (<paramref name="none"/> names what none means).</summary>
    public PropertyItem OptionalEnum<TEnum>(string name, ThemeLens<TEnum?> value, string none = "None")
        where TEnum : struct, System.Enum
    {
        var values = System.Enum.GetValues<TEnum>();
        string[] options = [none, .. Names(values)];
        return Choice(name, options, value.Map(v => v is { } set ? Array.IndexOf(values, set) + 1 : 0, i => i == 0 ? null : values[i - 1]));
    }

    public PropertyItem Color(string name, ThemeLens<Color> value) =>
        new(name, new ColorSwatchField(name, value.Read(theme), c => edit(t => value.Write(t, c), false)) { TestId = ThemeEditor.Colour });

    /// <summary>A section for a variant's look: its surface and content colours, outline and elevation. Closed at first.</summary>
    public PropertySection Look(string title, ThemeLens<SurfaceLook> look) => new(title,
    [
        OptionalEnum("Surface", look.Then(l => l.Surface, (l, v) => l with { Surface = v }), "None (see-through)"),
        Bool("Container colour", look.Then(l => l.SurfaceContainer, (l, v) => l with { SurfaceContainer = v })),
        OptionalEnum("Content", look.Then(l => l.Content, (l, v) => l with { Content = v }), "Surface's own"),
        Bool("On colour", look.Then(l => l.ContentOn, (l, v) => l with { ContentOn = v })),
        OptionalFloat("Legibility", look.Then(l => l.ContentLegibility, (l, v) => l with { ContentLegibility = v }), 0f, 1f, 1f, 0.05f, "0.00"),
        Bool("Outline", look.Then(l => l.Outline, (l, v) => l with { Outline = v })),
        Bool("Quiet outline", look.Then(l => l.OutlineVariant, (l, v) => l with { OutlineVariant = v })),
        OptionalFloat("Outline width", look.Then(l => l.OutlineWidth, (l, v) => l with { OutlineWidth = v }), 0.5f, 4f, 1f, 0.5f, "0.0"),
        OptionalEnum("Outline colour", look.Then(l => l.OutlineColor, (l, v) => l with { OutlineColor = v }), "Outline"),
        OptionalEnum("Elevation", look.Then(l => l.Elevation, (l, v) => l with { Elevation = v }), "Flat"),
    ]) { InitiallyOpen = false };

    /// <summary>A PascalCase name in words: "SurfaceContainerHigh" is "Surface container high".</summary>
    public static string Words(string name)
    {
        var words = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
            {
                words.Append(' ').Append(char.ToLowerInvariant(c));
            }
            else if (i > 0 && char.IsDigit(c) && !char.IsDigit(name[i - 1]))
            {
                words.Append(' ').Append(c);
            }
            else
            {
                words.Append(c);
            }
        }
        return words.ToString();
    }

    private static string[] Names<TEnum>(TEnum[] values)
        where TEnum : struct, System.Enum
    {
        var names = new string[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            names[i] = Words(values[i].ToString());
        }
        return names;
    }

    private static Box Slider(string name, float value, float min, float max, float step, string format, Action<float> change) => new()
    {
        Layout = new LayoutStyle { AlignSelf = Align.Stretch, FlexGrow = 1, FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 8 },
        Children =
        [
            new Box
            {
                Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 },
                Children = [new Slider(Math.Clamp(value, min, max), change) { TestId = ThemeEditor.Number, Min = min, Max = max, Step = step, Label = name }],
            },
            new SurfaceText(value.ToString(format, CultureInfo.InvariantCulture))
            {
                TextType = TextType.LabelMedium,
                Legibility = Legibility.Medium,
                MaxLines = 1,
                Layout = new LayoutStyle { Width = 44 },
            },
        ],
    };
}
