namespace Radiant.Layout;

/// <summary>
/// An <see cref="ILayoutBoundary"/> scroll container that opts into having its children laid out by
/// <see cref="YogaLayoutEngine"/> as a flex subtree in <em>content space</em> — full content width
/// (its <c>Size.X</c> minus the scrollbar gutter), unbounded height — instead of keeping manually
/// assigned child positions.
///
/// <para>The container still stops the parent layout walk (it is an <see cref="ILayoutBoundary"/>);
/// the engine merely runs a second, self-contained pass rooted at it. Because the container applies
/// the scroll offset at draw time (a render-translate), the engine writes children at their
/// <em>unscrolled</em> content positions, and the container's content extent is auto-measured from
/// those laid-out child bounds.</para>
/// </summary>
public interface IScrollContentHost
{
    /// <summary>
    /// When true, the engine lays out this container's children using <see cref="ContentLayout"/>.
    /// Default (false) preserves the legacy behaviour where children keep their manual positions.
    /// </summary>
    bool LayoutChildren { get; }

    /// <summary>
    /// The flex style applied to the synthetic content root that wraps the children — typically a
    /// padded vertical column with a row gap and <c>AlignItems = Stretch</c> so children fill the
    /// content width.
    /// </summary>
    LayoutStyle ContentLayout { get; }

    /// <summary>
    /// Width reserved on the trailing edge for the scrollbar; excluded from the content width so the
    /// scrollbar never overlaps content. Reserved unconditionally to avoid a width-vs-scrollability
    /// feedback loop.
    /// </summary>
    float ScrollbarWidth { get; }
}
