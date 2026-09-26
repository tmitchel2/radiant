namespace Radiant.Components;

/// <summary>How prominent a <see cref="SurfaceButton"/> is.</summary>
public enum ButtonVariant
{
    /// <summary>A filled primary button: the main action.</summary>
    Filled,

    /// <summary>A secondary-container button: important, but not the main action.</summary>
    Tonal,

    /// <summary>An outlined button: secondary actions.</summary>
    Outlined,

    /// <summary>Just text: the least prominent actions.</summary>
    Text,

    /// <summary>A raised surface-container button, for when a filled one is too loud on a busy background.</summary>
    Elevated,
}
