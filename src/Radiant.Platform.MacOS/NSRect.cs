using System.Runtime.InteropServices;

namespace Radiant.Platform.MacOS;

/// <summary>
/// AppKit's <c>NSRect</c> (<c>CGRect</c>): origin and size in points, as doubles on 64-bit. Its
/// origin is the bottom left unless the view is flipped.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct NSRect(double X, double Y, double Width, double Height);
