namespace Radiant.Components;

/// <summary>Something that happened, on a <see cref="Timeline"/>.</summary>
/// <param name="Title">What happened.</param>
/// <param name="Time">When ("2h ago", "12 March").</param>
public sealed record TimelineEvent(string Title, string Time)
{
    /// <summary>More about it.</summary>
    public string? Text { get; init; }

    /// <summary>An icon in its marker; a dot if null.</summary>
    public string? Icon { get; init; }

    /// <summary>The marker's colour family.</summary>
    public Radiant.Theming.SurfaceName? Color { get; init; }
}
