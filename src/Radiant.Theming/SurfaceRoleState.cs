namespace Radiant.Theming;

/// <summary>
/// Which of a surface family's four colours to use, and at what opacity: the family, whether it's
/// the "on" (content) colour, whether it's the container colour.
/// </summary>
/// <param name="Name">The colour family.</param>
/// <param name="On">Whether this is the colour for content on the family's colour.</param>
/// <param name="Container">Whether this is the family's container colour (or content on it).</param>
/// <param name="Opacity">An opacity to apply, or null for opaque.</param>
public readonly record struct SurfaceRoleState(SurfaceName Name, bool On, bool Container, float? Opacity = null);
