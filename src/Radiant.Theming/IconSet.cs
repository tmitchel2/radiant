namespace Radiant.Theming;

/// <summary>Which icons are drawn.</summary>
public enum IconSet
{
    /// <summary>Material Symbols: rounded, variable in weight, filled when chosen.</summary>
    Symbols,

    /// <summary>
    /// Lucide's outline icons: a fine, even stroke. Icons the outline set has no match for are
    /// drawn from Material Symbols.
    /// </summary>
    Outline,
}
