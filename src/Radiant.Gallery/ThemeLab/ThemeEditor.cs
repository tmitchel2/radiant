using System.Collections.Generic;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Gallery.ThemeLab;

/// <summary>
/// Picks a preset and edits every part of the theme live: colour, shape, type and the components'
/// styles. Edits last until the app quits; Reset puts the preset back.
/// </summary>
/// <param name="Themes">The app's theme, edited in place.</param>
internal sealed partial record ThemeEditor(ThemeController Themes) : Component
{
    [TestId<SegmentedButton>] public static partial string Preset { get; }
    [TestId<SurfaceButton>] public static partial string Reset { get; }
    [TestId<Switch>] public static partial string Dark { get; }
    [TestId<Tabs>] public static partial string Sections { get; }
    [TestId<ChoiceButton>] public static partial string Family { get; }
    [TestId<PropertyGrid>] public static partial string Properties { get; }

    // Shared by every row of their kind; a row is found by its section's and its own name.
    [TestId<Slider>] public static partial string Number { get; }
    [TestId<Switch>] public static partial string Toggle { get; }
    [TestId<ChoiceButton>] public static partial string Choice { get; }
    [TestId<ColorSwatchField>] public static partial string Colour { get; }

    private static readonly Tab[] s_tabs = [new("Colour") { Icon = "palette" }, new("Shape") { Icon = "crop_square" }, new("Type") { Icon = "text_fields" }, new("Styles") { Icon = "tune" }];

    public override Element? Build(BuildContext context)
    {
        // Watched only to rebuild: the values shown are the target theme's, not those mid-transition.
        context.UseTheme();
        var tab = context.UseState(0);
        var family = context.UseState(2);
        var snackbars = context.UseSnackbars();
        var themes = Themes;
        var theme = themes.Theme;
        var modified = ThemeEditing.IsModified(theme);
        var fields = new ThemeFields(theme, (change, animate) => themes.Set(change(themes.Theme), animate ? ThemeEditing.Transition : default));
        var sections = tab.Value switch
        {
            0 => ColourSections.For(fields),
            1 => ShapeSections.For(fields),
            2 => TypeSections.For(fields),
            _ => StyleSections.For(fields, family.Value),
        };

        var presets = new List<Segment>();
        var chosen = 0;
        for (var i = 0; i < ThemePresets.All.Count; i++)
        {
            presets.Add(new Segment(ThemePresets.All[i].Name!));
            chosen = ThemePresets.All[i].Name == theme.Name ? i : chosen;
        }

        void Choose(IReadOnlySet<int> picked)
        {
            var before = themes.Theme;
            foreach (var index in picked)
            {
                ThemePicker.Choose(themes, ThemePresets.All[index]);
            }
            if (ThemeEditing.IsModified(before))
            {
                snackbars.Show($"Your changes to {before.Name} were discarded", "Undo", () => themes.Set(before, ThemePicker.Transition));
            }
        }

        return new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1, MinHeight = 0, RowGap = 12 },
            Children =
            [
                new SegmentedButton(presets, new HashSet<int> { chosen }, Choose) { TestId = Preset },
                new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 8, MinHeight = 40 },
                    Children =
                    [
                        new Switch(theme.Colors.IsDark, _ => ThemePicker.ToggleDark(themes)) { TestId = Dark, Label = "Dark" },
                        new Box { Layout = new LayoutStyle { FlexGrow = 1 } },
                        modified ? new Tag("Modified") { Icon = "edit" } : null,
                        new SurfaceButton("Reset", ButtonVariant.Text)
                        {
                            TestId = Reset,
                            Icon = "replay",
                            ShowDisabled = !modified,
                            OnPress = modified ? () => themes.Set(ThemeEditing.Reset(themes.Theme), ThemePicker.Transition) : null,
                        },
                    ],
                },
                new Tabs(s_tabs, tab.Value, tab.Set) { TestId = Sections },
                tab.Value != 3 ? null : new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 12 },
                    Children =
                    [
                        new SurfaceText("Components") { TextType = TextType.LabelLarge, Legibility = Legibility.Medium },
                        new ChoiceButton("Components", StyleSections.Families, family.Value, family.Set) { TestId = Family },
                    ],
                },
                new ScrollArea
                {
                    Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1, MinHeight = 0 },
                    ContentLayout = new LayoutStyle { Padding = new Edges(0, 0, 12, 16) },
                    Children =
                    [
                        // Keyed by what's shown, so each tab and family opens fresh, its filter empty.
                        new PropertyGrid(sections)
                        {
                            Key = $"{tab.Value}/{family.Value}",
                            TestId = Properties,
                            NameWidth = 150,
                            Filterable = tab.Value == 3,
                            Label = "Theme properties",
                        },
                    ],
                },
            ],
        };
    }
}
