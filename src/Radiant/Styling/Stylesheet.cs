using System;
using System.Collections.Generic;
using System.Linq;
using Radiant.UI;

namespace Radiant.Styling;

/// <summary>
/// An ordered collection of <see cref="StyleRule"/>s. Rules are kept in insertion order; resolution
/// (<see cref="StyleResolver"/>) applies them by ascending <see cref="StyleRule.Priority"/> with
/// insertion order as a stable tie-break, so for equal priority the later-added rule wins.
/// </summary>
public sealed class Stylesheet
{
    private readonly List<StyleRule> _rules = [];

    /// <summary>Adds a pre-built rule.</summary>
    public Stylesheet Add(StyleRule rule)
    {
        _rules.Add(rule);
        return this;
    }

    /// <summary>Adds a rule from a selector predicate and style (fluent).</summary>
    public Stylesheet Add(Func<UIElement, bool> selector, Style style, int priority = 0) =>
        Add(new StyleRule(selector, style, priority));

    /// <summary>The rules in cascade-application order (ascending priority, stable by insertion).</summary>
    public IEnumerable<StyleRule> OrderedRules => _rules.OrderBy(r => r.Priority);
}
