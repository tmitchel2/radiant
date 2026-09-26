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

    public override Element? Build(BuildContext context)
    {
        var page = context.UseState(StartPage);
        NavItem[] items =
        [
            new("widgets", "Components"),
            new("dashboard", "Dashboard") { Section = "Application" },
            new("settings", "Settings"),
            new("login", "Sign in"),
            new("inbox", "Empty state"),
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
            5 => Pages.Marketing(),
            6 => Pages.Store(),
            7 => ShellPages.Workspace(),
            8 => ShellPages.Mail(),
            9 => ShellPages.NewProject(),
            10 => ShellPages.Preferences(),
            _ => new VerticalSlice(Themes) { StartWithDialog = StartWithDialog, StartWithMenu = StartWithMenu },
        };
        return new SnackbarHost(new SidebarLayout("Radiant Gallery", items, page.Value, page.Set, content)
        {
            Actions =
            [
                new Tooltip("Search", new IconButton("search", "Search")),
                new Tooltip("Notifications", new IconButton("notifications", "Notifications")),
                new Avatar("Tom Mitchell") { Size = 32 },
            ],
        });
    }
}
