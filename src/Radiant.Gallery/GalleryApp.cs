using System;
using Radiant.Components;
using Radiant.Gallery.ThemeLab;
using Radiant.Templates;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Gallery;

/// <summary>The gallery: the component showcase and a page per template family, in the sidebar shell.</summary>
internal sealed partial record GalleryApp(ThemeController Themes) : Component
{
    [TestId<IconButton>] public static partial string Search { get; }
    [TestId<IconButton>] public static partial string Notifications { get; }

    /// <summary>The theme page's index: last, so the other pages keep their numbers and shortcuts.</summary>
    public const int ThemePageIndex = 14;

    public int StartPage { get; init; }

    public bool StartWithDialog { get; init; }

    public bool StartWithMenu { get; init; }

    public bool StartWithSheet { get; init; }

    public bool StartWithPalette { get; init; }

    public override Element? Build(BuildContext context)
    {
        var page = context.UseState(StartPage);
        var palette = context.UseState(StartWithPalette);
        NavItem[] items =
        [
            new("widgets", "Components"),
            new("dashboard", "Dashboard") { Section = "Application" },
            new("settings", "Settings"),
            new("login", "Sign in"),
            new("inbox", "Empty state"),
            new("view_list", "Table"),
            new("rocket_launch", "Landing page") { Section = "Marketing" },
            new("storefront", "Store") { Section = "Ecommerce", Badge = 3 },
            new("code", "Workspace") { Section = "Desktop shells" },
            new("mail", "Mail"),
            new("rocket_launch", "New project"),
            new("tune", "Preferences"),
            new("dashboard_customize", "Docking"),
            new("view_in_ar", "Studio"),
            new("palette", "Theme") { Section = "Theming" },
        ];
        Element content = page.Value switch
        {
            1 => Pages.Dashboard(),
            2 => Pages.Settings(Themes, () => page.Set(ThemePageIndex)),
            3 => Pages.SignIn(),
            4 => Pages.Empty(),
            5 => new TablePage(),
            6 => Pages.Marketing(),
            7 => Pages.Store(),
            8 => ShellPages.Workspace(),
            9 => ShellPages.Mail(),
            10 => ShellPages.NewProject(),
            11 => ShellPages.Preferences(),
            12 => ShellPages.Docking(),
            13 => new StudioPage(),
            ThemePageIndex => new ThemePage(Themes),
            _ => new VerticalSlice(Themes) { StartWithDialog = StartWithDialog, StartWithMenu = StartWithMenu, StartWithSheet = StartWithSheet },
        };
        // The app's commands: on the menu bar (macOS's own), in the palette, and on their shortcuts.
        // The Edit menu acts on whichever text field has focus.
        context.UseStandardEditMenu();
        var section = (string?)null;
        for (var i = 0; i < items.Length; i++)
        {
            var index = i;
            section = items[i].Section ?? section;
            context.UseCommand(new Command($"page-{i}", items[i].Label)
            {
                Menu = "Go",
                Group = section ?? "Gallery",
                Icon = items[i].Icon,
                Keywords = section,
                Checked = page.Value == i,
                Shortcut = i < 9 ? KeyChord.Command(KeyCode.Number1 + i) : null,
                Run = () => page.Set(index),
            });
        }
        context.UseCommand(new Command("palette", "Command palette")
        {
            Menu = "View",
            Group = "Find",
            Icon = "search",
            Shortcut = KeyChord.Command(KeyCode.K),
            Run = () => palette.Set(true),
        });
        var commands = context.UseCommands();
        var editing = page.Value == ThemePageIndex;
        return new SnackbarHost(new Fragment(new SidebarLayout("Radiant Gallery", items, page.Value, page.Set, content)
        {
            // The theme page scrolls its editor and preview apart, and uses a wide window's width.
            FillContent = editing,
            MaxContentWidth = editing ? 1400 : 1080,
            Actions =
            [
                new Tooltip($"Search ({KeyChord.Command(KeyCode.K)})", new IconButton("search", "Search") { TestId = Search, OnPress = () => palette.Set(true) }),
                new ThemePicker(() => page.Set(ThemePageIndex)),
                new Tooltip("Notifications", new IconButton("notifications", "Notifications") { TestId = Notifications }),
                new Avatar("Tom Mitchell") { Size = 32 },
            ],
        }, new CommandPalette(palette.Value, () => palette.Set(false), commands), new ThemeCommands(Themes, () => page.Set(ThemePageIndex)), new CommandMenuBar { DrawWithoutPlatformMenuBar = false }));
    }

    /// <summary>The theme's commands: rebuilt with the theme, so the menu ticks what's in force, without rebuilding the gallery.</summary>
    private sealed record ThemeCommands(ThemeController Themes, Action OpenEditor) : Component
    {
        public override Element? Build(BuildContext context)
        {
            var theme = context.UseTheme().Theme;
            var dark = theme.Colors.IsDark;
            var themes = Themes;
            foreach (var preset in ThemePresets.All)
            {
                context.UseCommand(new Command($"theme-{preset.Name!.ToLowerInvariant()}", $"{preset.Name} theme")
                {
                    Menu = "View",
                    Group = "Theme",
                    Icon = "format_paint",
                    Keywords = "style look appearance",
                    Checked = theme.Name == preset.Name,
                    Run = () => ThemePicker.Choose(themes, preset),
                });
            }
            context.UseCommand(new Command("edit-theme", "Edit theme…")
            {
                Menu = "View",
                Group = "Theme",
                Icon = "tune",
                Keywords = "style look appearance customise colour shape type",
                Run = OpenEditor,
            });
            context.UseCommand(new Command("dark", "Dark theme")
            {
                Menu = "View",
                Group = "Theme",
                Icon = "palette",
                Keywords = "night light appearance",
                Checked = dark,
                Shortcut = KeyChord.Command(KeyCode.D, KeyModifiers.Shift),
                Run = () => ThemePicker.ToggleDark(themes),
            });
            return null;
        }
    }
}
