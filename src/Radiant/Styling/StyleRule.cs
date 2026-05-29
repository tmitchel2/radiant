using System;
using Radiant.UI;

namespace Radiant.Styling;

/// <summary>
/// One stylesheet rule: a predicate "selector" plus the <see cref="Style"/> to apply to any element
/// it matches. Selectors are plain <see cref="Func{UIElement, Boolean}"/> (LINQ-style) — compose them
/// with <c>&amp;&amp;</c>/<c>||</c> or the <see cref="Selectors"/> helpers. There is no CSS specificity:
/// predicates are opaque, so precedence is <see cref="Priority"/> first, then stylesheet insertion
/// order (later wins). See <see cref="StyleResolver"/>.
/// </summary>
/// <param name="Matches">Returns true when this rule applies to the element.</param>
/// <param name="Style">The paint properties this rule contributes.</param>
/// <param name="Priority">Higher priority wins over lower; ties break by insertion order.</param>
public sealed record StyleRule(Func<UIElement, bool> Matches, Style Style, int Priority = 0);
