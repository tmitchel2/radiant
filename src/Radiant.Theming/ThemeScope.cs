using System;
using Radiant.UI.Core;

namespace Radiant.Theming;

/// <summary>
/// Restyles part of an app: everything below it sees the theme in force changed by
/// <paramref name="Change"/> (its component styles, shapes, type, density), with the app's
/// colours kept. For one screen whose tabs are underlined in a theme whose tabs are segmented,
/// or a compact toolbar in a roomy app.
/// <code>new ThemeScope(t => t with { Density = -2 }, toolbar)</code>
/// </summary>
/// <param name="Change">How the theme differs below here. Changes to its colours are ignored: provide another <see cref="ThemeProvider"/> for those.</param>
/// <param name="Child">What it applies to.</param>
public sealed record ThemeScope(Func<Theme, Theme> Change, Element? Child) : Component
{
    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        return ThemeContexts.Theme.Provide(theme.Restyled(Change(theme.Theme)), Child);
    }
}
