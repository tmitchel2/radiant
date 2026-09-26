using Radiant.Graphics2D;

namespace Radiant.Theming;

/// <summary>
/// The four colours of a hand-picked <see cref="SurfaceName"/> family: the colour, content on it,
/// its quieter container, and content on the container.
/// </summary>
/// <param name="Color">The colour.</param>
/// <param name="On">Content on the colour.</param>
/// <param name="Container">The quieter container colour.</param>
/// <param name="OnContainer">Content on the container.</param>
public readonly record struct ColorFamily(Color Color, Color On, Color Container, Color OnContainer);
