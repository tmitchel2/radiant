using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>A section of an <see cref="Accordion"/>.</summary>
/// <param name="Title">The header's title.</param>
/// <param name="Content">What opening it shows.</param>
public sealed record AccordionItem(string Title, Element? Content)
{
    /// <summary>A line under the title.</summary>
    public string? Subtitle { get; init; }

    /// <summary>An icon before the title.</summary>
    public string? Icon { get; init; }

    /// <summary>Whether it can't be opened or closed.</summary>
    public bool Disabled { get; init; }
}
