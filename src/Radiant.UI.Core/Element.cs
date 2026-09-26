namespace Radiant.UI.Core;

/// <summary>
/// A description of part of the UI: immutable, cheap, and made afresh on every build. The
/// framework keeps the live objects (state, layout, drawing) and brings them up to date with each
/// new description, touching only what changed.
/// <para>
/// A <see cref="Component"/> describes UI in terms of other elements. Host elements, such as
/// <see cref="Box"/> and <see cref="TextBlock"/>, are what is actually laid out and drawn.
/// </para>
/// </summary>
public abstract record Element
{
    /// <summary>
    /// Identifies the element among its siblings from one build to the next, so a reordered list
    /// keeps each item's state. Without a key, an element takes over from the previous build's
    /// element at the same position in its parent's children, if it is of the same type.
    /// </summary>
    public Key? Key { get; init; }

    /// <summary>
    /// A name for tests and agents to find this element by (<c>@save</c>), which never shows. Set on a
    /// component, it names what the component draws when that's a single box; the outermost wins, so it
    /// can be set where a component is used. Assistive technology on macOS sees it as the accessibility
    /// identifier.
    /// </summary>
    public string? TestId { get; init; }
}
