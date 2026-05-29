using System.Collections.Generic;

namespace Radiant.UI;

/// <summary>
/// A <see cref="UIElement"/> that holds child elements. Provides a uniform child enumeration
/// so tree walks (layout, hit-testing, search) don't need to special-case each container type.
/// </summary>
public interface IUiContainer
{
    /// <summary>The element's direct children, in draw/z order.</summary>
    IReadOnlyList<UIElement> Children { get; }
}
