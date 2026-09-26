namespace Radiant.Theming;

/// <summary>A step on the theme's shape scale: how rounded a component's corners are.</summary>
public enum CornerShapeRole
{
    /// <summary>Square corners.</summary>
    None,

    /// <summary>Barely rounded (4 px by default): text fields' top corners, snackbars.</summary>
    ExtraSmall,

    /// <summary>Slightly rounded (8): chips, menus.</summary>
    Small,

    /// <summary>Rounded (12): cards.</summary>
    Medium,

    /// <summary>Well rounded (16): navigation drawers, FABs.</summary>
    Large,

    /// <summary>More rounded (20).</summary>
    LargeIncreased,

    /// <summary>Between large and extra large (24).</summary>
    SemiLarge,

    /// <summary>Very rounded (28): dialogs, sheets.</summary>
    ExtraLarge,

    /// <summary>More so (32).</summary>
    ExtraLargeIncreased,

    /// <summary>Extremely rounded (48).</summary>
    ExtraExtraLarge,

    /// <summary>Fully rounded: pills and circles.</summary>
    Full,
}
