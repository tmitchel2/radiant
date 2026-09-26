using System;
using Radiant.UI.Core;

namespace Radiant.Theming;

/// <summary>
/// Gives everything below it the <see cref="ThemeController"/>'s current theme, and runs the
/// controller's transitions on the UI's frames.
/// </summary>
/// <param name="Controller">The controller whose theme to provide.</param>
/// <param name="Child">The app.</param>
public sealed record ThemeProvider(ThemeController Controller, Element? Child) : Component
{
    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.Watch(Controller.Current);
        var controller = Controller;
        var root = context.Root;
        context.UseEffect(() => root.AddTicker(controller.Advance).Dispose, controller);
        return ThemeContexts.Theme.Provide(theme, Child);
    }
}
