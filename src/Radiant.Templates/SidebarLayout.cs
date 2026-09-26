using System;
using System.Collections.Generic;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>
/// The classic desktop app shell: a navigation drawer down the left, and on the right a top app bar
/// over the scrolling page. The app bar takes its container colour once the page scrolls.
/// </summary>
/// <param name="Title">The app's name, at the top of the sidebar.</param>
/// <param name="Items">The sidebar's destinations.</param>
/// <param name="Selected">The current destination.</param>
/// <param name="OnSelect">Called when a destination is chosen.</param>
/// <param name="Content">The current page.</param>
public sealed partial record SidebarLayout(string Title, IReadOnlyList<NavItem> Items, int Selected, Action<int> OnSelect, Element? Content) : Component
{
    [TestId<NavigationDrawer>] public static partial string Drawer { get; }
    [TestId<TopAppBar>] public static partial string AppBar { get; }
    [TestId] public static partial string Page { get; }

    /// <summary>Actions on the right of the app bar.</summary>
    public IReadOnlyList<Element?> Actions { get; init; } = [];

    /// <summary>The page's title in the app bar; the chosen destination's name by default.</summary>
    public string? PageTitle { get; init; }

    /// <summary>How wide the page's content may grow before it's centred with margins.</summary>
    public float MaxContentWidth { get; init; } = 1080f;

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var scroll = context.UseRef(new Radiant.Scrolling.ScrollController(new Radiant.Scrolling.ScrollBehaviour())).Value;
        var scrolled = context.UseState(false);
        context.UseEffect(() =>
        {
            void OnScroll(Radiant.Scrolling.ScrollMetrics metrics) => scrolled.Set(metrics.ContentOffset.Y > 0.5f);
            scroll.Scroll += OnScroll;
            return () => scroll.Scroll -= OnScroll;
        }, scroll);
        var title = PageTitle ?? (Selected >= 0 && Selected < Items.Count ? Items[Selected].Label : Title);
        return new Surface
        {
            SurfaceColor = SurfaceName.Surface,
            // Shrinks to fit what it's given: the drawer and the page scroll, rather than growing the
            // shell past the window to fit the longer of them.
            Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1, FlexDirection = FlexDirection.Row },
            Children =
            [
                new NavigationDrawer(Items, Selected, OnSelect) { TestId = Drawer, Title = Title, Width = 260 },
                new Box
                {
                    Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 },
                    Children =
                    [
                        new TopAppBar(title) { TestId = AppBar, Actions = Actions, Scrolled = scrolled.Value },
                        new ScrollArea
                        {
                            TestId = Page,
                            Controller = scroll,
                            Layout = new LayoutStyle { FlexGrow = 1 },
                            ContentLayout = new LayoutStyle { AlignItems = Align.Center, Padding = new Edges(24, 8, 24, 32) },
                            Children = [new Box { Layout = new LayoutStyle { MaxWidth = MaxContentWidth, AlignSelf = Align.Stretch, RowGap = 24 }, Children = [Content] }],
                        },
                    ],
                },
            ],
        };
    }
}
