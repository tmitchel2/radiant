namespace Radiant.Theming;

/// <summary>How far a surface is raised above the one beneath: Material 3's six levels.</summary>
public enum ElevationLevel
{
    /// <summary>Flat.</summary>
    Level0,

    /// <summary>Cards, raised buttons.</summary>
    Level1,

    /// <summary>Menus, app bars when scrolled.</summary>
    Level2,

    /// <summary>FABs, dialogs, search.</summary>
    Level3,

    /// <summary>Hovered level-3 components.</summary>
    Level4,

    /// <summary>Hovered level-4 components.</summary>
    Level5,
}
