namespace Radiant.Components;

/// <summary>A destination in a <see cref="NavigationRail"/> or <see cref="NavigationDrawer"/>.</summary>
/// <param name="Icon">Its icon (drawn filled when it's the current destination).</param>
/// <param name="Label">Its name.</param>
public sealed record NavItem(string Icon, string Label)
{
    /// <summary>A count to badge it with; 0 for a dot; null for none.</summary>
    public int? Badge { get; init; }

    /// <summary>A section heading shown before it (drawers only).</summary>
    public string? Section { get; init; }
}
