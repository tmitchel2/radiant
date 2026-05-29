namespace Radiant.Scrolling;

/// <summary>When scroll indicators (scrollbars) are shown.</summary>
public enum IndicatorVisibility
{
    /// <summary>Never draw indicators.</summary>
    None,

    /// <summary>Draw indicators only when the content is scrollable on that axis.</summary>
    Auto,

    /// <summary>Always draw indicators when scrollable, persisting (no fade).</summary>
    Always,
}
