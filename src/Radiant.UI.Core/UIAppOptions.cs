using System.Numerics;
using Radiant.Text;

namespace Radiant.UI.Core;

/// <summary>How <see cref="RadiantUI.Run"/> opens its window.</summary>
public sealed record UIAppOptions
{
    /// <summary>The window title.</summary>
    public string Title { get; init; } = "Radiant";

    /// <summary>The window's width, in logical pixels.</summary>
    public int Width { get; init; } = 1024;

    /// <summary>The window's height, in logical pixels.</summary>
    public int Height { get; init; } = 768;

    /// <summary>What shows behind the UI, linear and straight alpha.</summary>
    public Vector4 Background { get; init; } = Vector4.One;

    /// <summary>The fonts text is set in; Radiant's embedded fonts if null.</summary>
    public FontLibrary? Fonts { get; init; }

    /// <summary>How many pixels one notch of a mouse wheel scrolls.</summary>
    public float WheelStep { get; init; } = 40f;
}
