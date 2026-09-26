namespace Radiant.Components;

/// <summary>An open document in <see cref="DocumentTabs"/>.</summary>
/// <param name="Label">The document's name.</param>
public sealed record DocumentTab(string Label)
{
    /// <summary>What tells two tabs apart as they open, close and move; the label if null.</summary>
    public string? Id { get; init; }

    /// <summary>An icon before the name (the document's kind).</summary>
    public string? Icon { get; init; }

    /// <summary>Whether it has unsaved changes: a dot shows where the close button is, until hovered.</summary>
    public bool Modified { get; init; }

    /// <summary>Whether it can be closed.</summary>
    public bool Closable { get; init; } = true;
}
