namespace Radiant.Theming;

/// <summary>How tabs (and segmented buttons) are built.</summary>
public enum TabsLook
{
    /// <summary>Tabs share the width, icons above labels, the current one underlined by a sliding indicator; segments are joined and outlined, the chosen one ticked.</summary>
    Underline,

    /// <summary>Tabs sized to their labels in a tray, icons beside labels, the current one a raised pill that slides; segments the same.</summary>
    Segmented,
}
