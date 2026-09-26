namespace Radiant.Theming;

/// <summary>
/// How strongly content shows against its surface, as an opacity: the Destash legibility scale.
/// Any value from 0 to 1 works; these are the named steps.
/// </summary>
public static class Legibility
{
    /// <summary>Fully opaque.</summary>
    public const float Full = 1f;

    /// <summary>Primary text.</summary>
    public const float High = 0.87f;

    /// <summary>Secondary text.</summary>
    public const float Medium = 0.6f;

    /// <summary>Hints and disabled content.</summary>
    public const float Low = 0.38f;

    /// <summary>Disabled containers, dividers.</summary>
    public const float VeryLow = 0.12f;

    /// <summary>Invisible.</summary>
    public const float None = 0f;
}
