namespace Radiant.Text.Unicode;

/// <summary>
/// Splits UTF-16 into code points and the index each starts at, which is what the segmentation
/// rules read and what callers need boundaries in. A lone surrogate stands for itself rather than
/// becoming U+FFFD, so its own properties (a control, for graphemes) decide how it breaks.
/// </summary>
internal static class CodePoints
{
    /// <summary>
    /// Decodes <paramref name="text"/> into <paramref name="codePoints"/> and
    /// <paramref name="starts"/> (both at least <c>text.Length</c> long) and returns the count.
    /// </summary>
    public static int Decode(string text, int[] codePoints, int[] starts)
    {
        var count = 0;
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            starts[count] = i;
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                codePoints[count++] = char.ConvertToUtf32(c, text[i + 1]);
                i += 2;
            }
            else
            {
                codePoints[count++] = c;
                i++;
            }
        }
        return count;
    }
}
