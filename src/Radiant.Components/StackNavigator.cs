using System;
using System.Collections.Generic;
using System.Numerics;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// Drill-down navigation (settings to a section to an item): an app bar with the page's title and,
/// past the root, a back button; pages inside push the next with
/// <c>context.UseNavigator()!.Push(title, page)</c>. Each new page slides in from the side it came
/// from. ⌘[ (Alt+Left elsewhere) goes back.
/// </summary>
/// <param name="RootTitle">The first page's title.</param>
/// <param name="Root">The first page.</param>
public sealed record StackNavigator(string RootTitle, Element Root) : Component
{
    /// <summary>Buttons on the app bar's right, on every page.</summary>
    public IReadOnlyList<Element?> Actions { get; init; } = [];

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var pages = context.UseState<IReadOnlyList<NavigatorPage>>(() => [new NavigatorPage(RootTitle, Root)]);
        var navigator = context.UseRef<Navigator?>(null);
        navigator.Value ??= new Navigator(() => pages.Value, pages.Set);
        var nav = navigator.Value;
        var depth = pages.Value.Count;

        // Which way the last change went: a new page comes from the right, a revealed one from the
        // left. The first page just appears.
        var previousDepth = context.UseRef(depth);
        var navigated = context.UseRef(false);
        if (depth != previousDepth.Value)
        {
            navigated.Value = true;
        }
        var direction = depth >= previousDepth.Value ? 1f : -1f;
        previousDepth.Value = depth;

        context.UseShortcut(OperatingSystem.IsMacOS() ? KeyChord.Command(KeyCode.LeftBracket) : new KeyChord(KeyCode.Left, KeyModifiers.Alt), nav.Pop);

        var top = pages.Value[^1];
        return NavigatorHooks.Context.Provide(nav, new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1 },
            Children =
            [
                new TopAppBar(top.Title)
                {
                    NavigationIcon = depth > 1 ? "arrow_back" : null,
                    NavigationLabel = "Back",
                    OnNavigation = depth > 1 ? nav.Pop : null,
                    Actions = Actions,
                },
                // Keyed by depth, so each page change is a new transition.
                new PageTransition(top.Content, navigated.Value ? direction : 0f) { Key = depth },
            ],
        });
    }

    /// <summary>A page sliding in: from a little to the right (a push) or left (a pop), fading in; still when <see cref="Direction"/> is 0.</summary>
    private sealed record PageTransition(Element Content, float Direction) : Component
    {
        public override Element? Build(BuildContext context)
        {
            var motion = context.UseTheme().Theme.Motion;
            var animate = Direction != 0f && !motion.Reduced;
            var progress = context.UseTransition(1f, animate ? motion.MediumDuration : TimeSpan.Zero, motion.Enter, initial: animate ? 0f : 1f);
            return new Box
            {
                Opacity = progress,
                Transform = progress < 1f ? Matrix3x2.CreateTranslation(Direction * 32f * (1 - progress), 0) : null,
                Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 },
                Children = [Content],
            };
        }
    }
}
