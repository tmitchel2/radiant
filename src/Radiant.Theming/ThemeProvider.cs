using System;
using Radiant.UI.Core;

namespace Radiant.Theming;

/// <summary>
/// Gives everything below it the <see cref="ThemeController"/>'s current theme, and runs the
/// controller's transitions on the UI's frames. With <see cref="FollowAppearance"/>, the theme
/// also follows the user's system appearance, read from the platform above.
/// </summary>
/// <param name="Controller">The controller whose theme to provide.</param>
/// <param name="Child">The app.</param>
public sealed record ThemeProvider(ThemeController Controller, Element? Child) : Component
{
    /// <summary>
    /// Whether the theme follows the platform's appearance (dark mode, accent colour, increased
    /// contrast, reduced motion) through <see cref="ThemeAppearance.FollowAppearance"/>.
    /// </summary>
    public bool FollowAppearance { get; init; }

    /// <summary>Which settings <see cref="FollowAppearance"/> follows; all if null.</summary>
    public AppearanceFollowing? Following { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.Watch(Controller.Current);
        var controller = Controller;
        var root = context.Root;
        // Frames are asked for only while a transition runs: an idle app draws nothing.
        context.UseEffect(() =>
        {
            IDisposable? ticker = null;
            void Start() => ticker ??= root.AddTicker(seconds =>
            {
                controller.Advance(seconds);
                if (!controller.IsAnimating)
                {
                    ticker?.Dispose();
                    ticker = null;
                }
            }, TickerKind.Animation, "theme transition");
            controller.TransitionStarted += Start;
            if (controller.IsAnimating)
            {
                Start();
            }
            return () =>
            {
                controller.TransitionStarted -= Start;
                ticker?.Dispose();
            };
        }, controller);
        var appearance = context.UsePlatform().Appearance;
        var follow = FollowAppearance;
        var following = Following;
        // Effects run before the frame is drawn, so the first frame already has the user's appearance.
        context.UseEffect(() => follow ? controller.FollowAppearance(appearance, following).Dispose : null,
            (controller, appearance, follow, following));
        return ThemeContexts.Theme.Provide(theme, Child);
    }
}
