using Radiant.UI;

namespace Radiant.Styling;

/// <summary>
/// Computes the effective <see cref="Style"/> for an element: folds every matching stylesheet rule
/// in cascade order, then layers the element's inline <see cref="UIElement.Style"/> on top (inline
/// always wins). Selectors read live state (e.g. <see cref="UIElement.CurrentPseudoState"/>), so the
/// result reflects the element's current pseudo-state at resolve time.
/// </summary>
public static class StyleResolver
{
    /// <summary>Resolves the effective style for <paramref name="element"/> against <paramref name="sheet"/>.</summary>
    public static Style Resolve(UIElement element, Stylesheet? sheet)
    {
        var result = default(Style);

        if (sheet is not null)
        {
            foreach (var rule in sheet.OrderedRules)
            {
                if (rule.Matches(element))
                {
                    result = Style.Merge(result, rule.Style);
                }
            }
        }

        // Inline style wins over any stylesheet rule.
        return Style.Merge(result, element.Style);
    }
}
