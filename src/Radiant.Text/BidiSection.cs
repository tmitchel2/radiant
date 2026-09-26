using Radiant.Text.Unicode;

namespace Radiant.Text;

/// <summary>
/// A bidi paragraph: the text between hard line breaks, with the break at its end. Each resolves
/// its own direction, as Unicode says, so a Hebrew line after an English one reads right to left.
/// </summary>
/// <param name="Start">The first UTF-16 index.</param>
/// <param name="End">One past the last, after the line break.</param>
/// <param name="Bidi">The resolved levels, indexed from <paramref name="Start"/>.</param>
internal readonly record struct BidiSection(int Start, int End, BidiParagraph Bidi);
