using System.Collections.Generic;
using System.Drawing;

namespace Radiant.Platform;

/// <summary>A node of the accessibility tree the UI hands the platform (<see cref="IAccessibility"/>).</summary>
/// <param name="Id">What identifies it, the same from one tree to the next while it lives.</param>
/// <param name="Role">What it is.</param>
/// <param name="Label">Its name.</param>
/// <param name="Frame">Where it is, in the window's content, points from its top left.</param>
/// <param name="Children">What it contains.</param>
public sealed record AccessibilityNode(int Id, AccessibilityRole Role, string? Label, RectangleF Frame, IReadOnlyList<AccessibilityNode> Children)
{
    /// <summary>Its value: a field's text, a slider's number, a row's position.</summary>
    public string? Value { get; init; }

    /// <summary>More about it than its name.</summary>
    public string? Description { get; init; }

    /// <summary>Whether it's checked (null: not a check box, or mixed).</summary>
    public bool? Checked { get; init; }

    /// <summary>Whether it's the chosen one (a tab, a row).</summary>
    public bool Selected { get; init; }

    /// <summary>Whether it can't be used.</summary>
    public bool Disabled { get; init; }

    /// <summary>Whether it can take keyboard focus.</summary>
    public bool Focusable { get; init; }

    /// <summary>Whether it has keyboard focus.</summary>
    public bool Focused { get; init; }

    /// <summary>Whether what it shows or hides is showing (null if it doesn't).</summary>
    public bool? Expanded { get; init; }

    /// <summary>A heading's level, or 0.</summary>
    public int HeadingLevel { get; init; }
}
