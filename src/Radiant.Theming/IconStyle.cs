namespace Radiant.Theming;

/// <summary>How icons are drawn: their size where a component doesn't choose one, and their stroke.</summary>
public sealed record IconStyle
{
    /// <summary>The size of an icon a component doesn't size, in pixels.</summary>
    public float Size { get; init; } = 24f;

    /// <summary>The icon font's weight: 400 is the standard stroke, lower is finer, higher bolder.</summary>
    public float Weight { get; init; } = 400f;

    /// <summary>Whether a chosen item's icon (a current tab or destination) is drawn filled.</summary>
    public bool FillChosen { get; init; } = true;
}
