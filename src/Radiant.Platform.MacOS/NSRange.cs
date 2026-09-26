using System.Runtime.InteropServices;

namespace Radiant.Platform.MacOS;

/// <summary>Foundation's <c>NSRange</c>: a location and length in UTF-16 code units.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct NSRange(nuint Location, nuint Length)
{
    /// <summary>Foundation's <c>NSNotFound</c> (<c>NSIntegerMax</c>), the location of "no range".</summary>
    public static readonly nuint NotFound = (nuint)nint.MaxValue;

    /// <summary>The range AppKit reads as "none": <c>{NSNotFound, 0}</c>.</summary>
    public static NSRange Empty => new(NotFound, 0);
}
