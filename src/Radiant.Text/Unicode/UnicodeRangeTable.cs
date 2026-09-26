using System;

namespace Radiant.Text.Unicode;

/// <summary>
/// A Unicode character property as sorted, non-overlapping code-point ranges with a value each,
/// generated from the Unicode Character Database by <c>tools/unicode-data/generate.py</c>. Code
/// points outside every range take the property's default. Latin-1 is looked up in a flat array;
/// the rest by binary search.
/// </summary>
internal sealed class UnicodeRangeTable
{
    private readonly int[] _starts;
    private readonly int[] _ends;
    private readonly byte[] _values;
    private readonly byte _default;
    private readonly byte[] _latin1 = new byte[256];

    public UnicodeRangeTable(int[] starts, int[] ends, byte[] values, byte defaultValue)
    {
        if (starts.Length != ends.Length || starts.Length != values.Length)
        {
            throw new ArgumentException("Range arrays differ in length.");
        }
        _starts = starts;
        _ends = ends;
        _values = values;
        _default = defaultValue;
        for (var c = 0; c < _latin1.Length; c++)
        {
            _latin1[c] = Search(c);
        }
    }

    /// <summary>The property value of a code point.</summary>
    public byte Lookup(int codePoint) =>
        (uint)codePoint < (uint)_latin1.Length ? _latin1[codePoint] : Search(codePoint);

    private byte Search(int codePoint)
    {
        int lo = 0, hi = _starts.Length - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) >>> 1;
            if (codePoint < _starts[mid])
            {
                hi = mid - 1;
            }
            else if (codePoint > _ends[mid])
            {
                lo = mid + 1;
            }
            else
            {
                return _values[mid];
            }
        }
        return _default;
    }
}
