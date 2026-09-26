using Radiant.Graphics2D;
using Radiant.Layout;

namespace Radiant.UI.Core;

/// <summary>
/// A picture. Without a size in its layout it takes the picture's own; otherwise the picture fills
/// the box as <see cref="Fit"/> says, cut to <see cref="CornerRadii"/>.
/// </summary>
/// <param name="Source">The picture.</param>
public sealed record Image(ImageSource Source) : HostElement
{
    /// <summary>How the picture fills the box.</summary>
    public ImageFit Fit { get; init; }

    /// <summary>The corners it's cut to.</summary>
    public CornerRadii CornerRadii { get; init; }

    /// <summary>What the picture shows, for assistive technology; null for a decorative picture.</summary>
    public string? AltText { get; init; }

    /// <summary>Size and placement.</summary>
    public LayoutStyle Layout { get; init; }

    internal override RenderNode CreateRenderNode() => new ImageRenderNode();
}
