namespace Radiant.Templates;

/// <summary>A customer's review, for <see cref="Reviews"/>.</summary>
/// <param name="Author">Who wrote it.</param>
/// <param name="Rating">Stars out of five.</param>
/// <param name="Title">Its headline.</param>
/// <param name="Text">What they said.</param>
public sealed record Review(string Author, int Rating, string Title, string Text)
{
    /// <summary>When, as shown ("3 days ago", "12 March").</summary>
    public string? Date { get; init; }
}
