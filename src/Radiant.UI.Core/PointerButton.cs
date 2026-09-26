namespace Radiant.UI.Core;

/// <summary>A pointer button. The values match GLFW's, which the window layer reports.</summary>
public enum PointerButton
{
    /// <summary>A button the platform couldn't identify.</summary>
    Unknown = -1,

    /// <summary>The primary button.</summary>
    Left = 0,

    /// <summary>The secondary button (context menus).</summary>
    Right = 1,

    /// <summary>The middle button or wheel press.</summary>
    Middle = 2,

    /// <summary>The fourth button, usually Back.</summary>
    Button4 = 3,

    /// <summary>The fifth button, usually Forward.</summary>
    Button5 = 4,
}
