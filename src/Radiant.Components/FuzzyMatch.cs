using System;

namespace Radiant.Components;

/// <summary>
/// Scores how well a typed query matches a text, as command palettes and quick-open lists rank:
/// the query's characters must appear in order (ignoring case), and runs of them (more the
/// longer they go), word starts and a match at the very start score higher.
/// </summary>
public static class FuzzyMatch
{
    /// <summary>The match's score (higher is better), or null when the text doesn't contain the query in order.</summary>
    public static int? Score(string query, string text)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(text);
        // Preferring word starts can skip past the only place a later character matches ("ba"
        // in "xba Bx"), so fall back to taking each first match.
        return Try(query, text, preferWordStarts: true) ?? Try(query, text, preferWordStarts: false);
    }

    private static int? Try(string query, string text, bool preferWordStarts)
    {
        var score = 0;
        var at = 0;
        var previous = -2;
        var run = 0;
        foreach (var wanted in query)
        {
            if (char.IsWhiteSpace(wanted))
            {
                continue;
            }
            var first = -1;
            var found = -1;
            for (var i = at; i < text.Length && found < 0; i++)
            {
                if (char.ToUpperInvariant(text[i]) != char.ToUpperInvariant(wanted))
                {
                    continue;
                }
                if (first < 0)
                {
                    first = i;
                }
                if (!preferWordStarts || i == previous + 1 || IsWordStart(text, i))
                {
                    found = i;
                }
            }
            found = found < 0 ? first : found;
            if (found < 0)
            {
                return null;
            }
            score += 1;
            // Each character continuing a run is worth more than the last, so a run outscores
            // letters scattered across word starts ("them": "theme" over "the manual").
            run = found == previous + 1 ? run + 1 : 0;
            if (run > 0)
            {
                score += 3 + 2 * run;
            }
            if (IsWordStart(text, found))
            {
                score += found == 0 ? 10 : 8;
            }
            score -= Math.Min(3, found - previous - 1);
            previous = found;
            at = found + 1;
        }
        return score;
    }

    private static bool IsWordStart(string text, int i) =>
        i == 0
        || !char.IsLetterOrDigit(text[i - 1])
        || char.IsUpper(text[i]) && char.IsLower(text[i - 1]);
}
