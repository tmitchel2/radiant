using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>A step of a <see cref="Wizard"/>.</summary>
/// <param name="Title">The step's name, in the list of steps and over its content.</param>
/// <param name="Content">What the step asks.</param>
public sealed record WizardStep(string Title, Element? Content)
{
    /// <summary>A line under the title.</summary>
    public string? Description { get; init; }

    /// <summary>Whether the step is complete enough to go on (Next is disabled until it is).</summary>
    public bool CanContinue { get; init; } = true;
}
