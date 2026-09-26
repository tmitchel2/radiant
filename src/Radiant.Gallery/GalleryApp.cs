using System.Linq;
using Radiant.Components;
using Radiant.Templates;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Gallery;

/// <summary>The gallery: the component showcase and a page per template family, in the sidebar shell.</summary>
internal sealed record GalleryApp(ThemeController Themes) : Component
{
    public int StartPage { get; init; }

    public bool StartWithDialog { get; init; }

    public bool StartWithMenu { get; init; }

    public bool StartWithSheet { get; init; }

    public bool StartWithPalette { get; init; }

    public override Element? Build(BuildContext context)
    {
        var page = context.UseState(StartPage);
        var palette = context.UseState(StartWithPalette);
        context.UseShortcut(KeyChord.Command(KeyCode.K), () => palette.Set(true));
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
        ];
        Element content = page.Value switch
        {
            1 => Pages.Dashboard(),
            2 => Pages.Settings(Themes),
            3 => Pages.SignIn(),
            4 => Pages.Empty(),
            5 => new TablePage(),
            6 => Pages.Marketing(),
            7 => Pages.Store(),
            8 => ShellPages.Workspace(),
            9 => ShellPages.Mail(),
            10 => ShellPages.NewProject(),
            11 => ShellPages.Preferences(),
            _ => new VerticalSlice(Themes) { StartWithDialog = StartWithDialog, StartWithMenu = StartWithMenu, StartWithSheet = StartWithSheet },
        };
        var themes = Themes;
        Command[] commands =
        [
            .. items.Select((item, index) => new Command($"page-{index}", $"Go to {item.Label}")
            {
                Group = "Pages",
                Icon = item.Icon,
                Keywords = item.Section,
                Run = () => page.Set(index),
            }),
            new Command("dark", "Toggle dark theme")
            {
                Group = "Theme",
                Icon = "palette",
                Keywords = "night light appearance",
                Run = () => themes.Set(themes.Theme with { Colors = themes.Theme.Colors with { IsDark = !themes.Theme.Colors.IsDark } }, System.TimeSpan.FromMilliseconds(300)),
            },
        ];
        return new SnackbarHost(new Fragment(new SidebarLayout("Radiant Gallery", items, page.Value, page.Set, content)
        {
            Actions =
            [
                new Tooltip($"Search ({KeyChord.Command(KeyCode.K)})", new IconButton("search", "Search") { OnPress = () => palette.Set(true) }),
                new Tooltip("Notifications", new IconButton("notifications", "Notifications")),
                new Avatar("Tom Mitchell") { Size = 32 },
            ],
        }, new CommandPalette(palette.Value, () => palette.Set(false), commands)));
    }
}
