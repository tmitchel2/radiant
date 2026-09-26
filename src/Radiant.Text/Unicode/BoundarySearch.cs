using System.Collections.Generic;

namespace Radiant.Text.Unicode;

/// <summary>
/// Finds the boundary either side of an index in a sorted boundary list. Segmentation rules look
/// arbitrarily far back (regional indicator pairs, conjuncts), so the neighbours of an index are
/// read off the whole text's boundaries rather than worked out locally.
/// </summary>
internal static class BoundarySearch
{
    /// <summary>The nearest boundary strictly before <paramref name="index"/>, or 0.</summary>
    public static int Previous(List<int> boundaries, int index)
    {
        var i = boundaries.BinarySearch(index);
        if (i < 0)
        {
            i = ~i;
        }
        return i > 0 ? boundaries[i - 1] : 0;
    }

    /// <summary>The nearest boundary strictly after <paramref name="index"/>, or the last one (the text's length).</summary>
    public static int Next(List<int> boundaries, int index)
    {
        var i = boundaries.BinarySearch(index);
        i = i < 0 ? ~i : i + 1;
        return i < boundaries.Count ? boundaries[i] : boundaries[^1];
    }
}
