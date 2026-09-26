namespace Radiant.Theming;

/// <summary>How a switch is built.</summary>
public enum SwitchLook
{
    /// <summary>A 52 × 32 track, outlined when off; the handle grows when on and more while pressed.</summary>
    Expressive,

    /// <summary>A 36 × 20 track filled either way; a white thumb that only slides.</summary>
    Compact,
}
