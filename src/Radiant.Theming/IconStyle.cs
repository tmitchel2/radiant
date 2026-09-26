namespace Radiant.Theming;

/// <summary>How icons are drawn: which set, their size where a component doesn't choose one, and their stroke.</summary>
public sealed record IconStyle
{
    /// <summary>Which icons are drawn.</summary>
    public IconSet Set { get; init; } = IconSet.Symbols;

    /// <summary>The size of an icon a component doesn't size, in pixels.</summary>
    public float Size { get; init; } = 24f;

    /// <summary>Material Symbols' weight: 400 is the standard stroke, lower is finer, higher bolder (outline icons have one stroke).</summary>
    public float Weight { get; init; } = 400f;

    /// <summary>Whether a chosen item's icon (a current tab or destination) is drawn filled.</summary>
    public bool FillChosen { get; init; } = true;
}
