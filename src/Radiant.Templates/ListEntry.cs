namespace Radiant.Templates;

/// <summary>A row of a <see cref="StackedList"/>.</summary>
/// <param name="Title">The main line.</param>
/// <param name="Subtitle">The second line.</param>
public sealed record ListEntry(string Title, string Subtitle)
{
    /// <summary>Who or what it's about, for the avatar.</summary>
    public string? AvatarName { get; init; }

    /// <summary>A note on the right, such as a time.</summary>
    public string? Meta { get; init; }

    /// <summary>A status chip on the right.</summary>
    public string? Status { get; init; }
}
