namespace Radiant.UI.Core;

/// <summary>
/// What assistive technology should know about a box: its role, name and state. Boxes without
/// it (and without focus) are skipped, their children reported in their place.
/// </summary>
public sealed record Semantics
{
    /// <summary>What the box is.</summary>
    public SemanticsRole Role { get; init; } = SemanticsRole.Group;

    /// <summary>Its name, read out: a button's text, an icon button's purpose. Null takes it from the text inside.</summary>
    public string? Label { get; init; }

    /// <summary>Its value, for sliders, fields and progress: "50%".</summary>
    public string? Value { get; init; }

    /// <summary>More detail, read after the name.</summary>
    public string? Description { get; init; }

    /// <summary>For check boxes, switches and radio buttons: whether checked; null for mixed or not applicable.</summary>
    public bool? Checked { get; init; }

    /// <summary>For tabs, list items and options: whether selected.</summary>
    public bool Selected { get; init; }

    /// <summary>For menus and disclosures: whether expanded; null if it doesn't expand.</summary>
    public bool? Expanded { get; init; }

    /// <summary>Whether it can't be used right now.</summary>
    public bool Disabled { get; init; }

    /// <summary>For headings: the level, 1 to 6.</summary>
    public int HeadingLevel { get; init; }

    /// <summary>A name for tests to find it by; takes precedence over the element's <see cref="Element.TestId"/>.</summary>
    public string? TestId { get; init; }
}
