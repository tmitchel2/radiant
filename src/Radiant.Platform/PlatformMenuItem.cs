namespace Radiant.Platform;

/// <summary>An entry of a platform menu (<see cref="IMenuService"/>).</summary>
/// <param name="Title">What it says; ignored for a separator.</param>
public sealed record PlatformMenuItem(string Title)
{
    /// <summary>A line between groups of items.</summary>
    public static PlatformMenuItem Separator { get; } = new("") { IsSeparator = true };

    /// <summary>Whether it's a line rather than an item.</summary>
    public bool IsSeparator { get; init; }

    /// <summary>Whether it can be chosen.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Whether it's ticked.</summary>
    public bool Checked { get; init; }
}
