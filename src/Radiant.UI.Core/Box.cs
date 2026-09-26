using System;
using System.Collections.Generic;
using System.Numerics;
using Radiant.Graphics2D;
using Radiant.Layout;
using Radiant.Platform;

namespace Radiant.UI.Core;

/// <summary>
/// The general-purpose host element: a rectangle that lays out its children with flexbox and can
/// draw a background, border, rounded corners and shadows, clip, fade, transform, take focus and
/// handle input. Most of the UI is boxes.
/// </summary>
public sealed record Box : HostElement
{
    /// <summary>Size, spacing and how children are arranged (flexbox).</summary>
    public LayoutStyle Layout { get; init; }

    /// <summary>The fill, linear and straight alpha; null for none.</summary>
    public Vector4? Background { get; init; }

    /// <summary>A gradient fill, drawn instead of <see cref="Background"/>; its points are in the box's own coordinates (0, 0 at its top left).</summary>
    public Gradient? BackgroundGradient { get; init; }

    /// <summary>The corner radii, for the background, border, shadows and clip.</summary>
    public CornerRadii CornerRadii { get; init; }

    /// <summary>The border's width, inside the box; 0 for none.</summary>
    public float BorderWidth { get; init; }

    /// <summary>The border's colour.</summary>
    public Vector4 BorderColor { get; init; }

    /// <summary>Shadows, drawn under the box, first to last.</summary>
    public IReadOnlyList<BoxShadow> Shadows { get; init; } = [];

    /// <summary>How opaque the box and everything in it are, as one group: 0 to 1.</summary>
    public float Opacity { get; init; } = 1f;

    /// <summary>Whether children are cut to the box's rounded shape.</summary>
    public bool ClipContent { get; init; }

    /// <summary>A transform of the box and its contents, about <see cref="TransformOrigin"/>; null for none.</summary>
    public Matrix3x2? Transform { get; init; }

    /// <summary>The point the transform is about, as a fraction of the size: (0.5, 0.5) is the centre.</summary>
    public Vector2 TransformOrigin { get; init; } = new(0.5f, 0.5f);

    /// <summary>What the box contains.</summary>
    public IReadOnlyList<Element?> Children { get; init; } = [];

    /// <summary>Whether the pointer can hit the box itself; false lets it through to what is below.</summary>
    public bool HitTestVisible { get; init; } = true;

    /// <summary>
    /// The pointer's shape over the box, or null to use its parent's. The deepest hovered box
    /// with a shape wins (<see cref="UIRoot.Cursor"/>); while a press is held, the box it began
    /// on keeps deciding, so a drag keeps its cursor wherever the pointer goes.
    /// </summary>
    public CursorShape? Cursor { get; init; }

    /// <summary>Whether the box can take keyboard focus (by click, or by Tab if <see cref="TabIndex"/> is not negative).</summary>
    public bool Focusable { get; init; }

    /// <summary>Tab order: 0 in tree order, negative to skip the box when tabbing.</summary>
    public int TabIndex { get; init; }

    /// <summary>
    /// Whether Tab stays inside the box: while it's the most recently shown trap, keyboard focus
    /// cycles among its focusable children only (a modal dialog, an open menu).
    /// </summary>
    public bool TrapFocus { get; init; }

    /// <summary>A handle that gives code the box's bounds and a way to focus it.</summary>
    public ElementRef? Ref { get; init; }

    /// <summary>What assistive technology should know about the box; null leaves it out (unless focusable).</summary>
    public Semantics? Semantics { get; init; }

    /// <summary>A pointer button pressed over the box or its children (bubbling up).</summary>
    public Action<PointerEventArgs>? OnPointerDown { get; init; }

    /// <summary>A pointer button pressed, on the way down to the target (before <see cref="OnPointerDown"/>).</summary>
    public Action<PointerEventArgs>? OnPointerDownCapture { get; init; }

    /// <summary>A pointer button released, over the box or after being pressed on it.</summary>
    public Action<PointerEventArgs>? OnPointerUp { get; init; }

    /// <summary>The pointer moved over the box, or anywhere while a press that began on it is held.</summary>
    public Action<PointerEventArgs>? OnPointerMove { get; init; }

    /// <summary>The pointer came over the box or one of its children (not bubbling).</summary>
    public Action<PointerEventArgs>? OnPointerEnter { get; init; }

    /// <summary>The pointer left the box and its children (not bubbling).</summary>
    public Action<PointerEventArgs>? OnPointerLeave { get; init; }

    /// <summary>The wheel or trackpad scrolled over the box.</summary>
    public Action<PointerEventArgs>? OnWheel { get; init; }

    /// <summary>A button pressed and released over the box; <see cref="PointerEventArgs.ClickCount"/> counts double clicks.</summary>
    public Action<PointerEventArgs>? OnClick { get; init; }

    /// <summary>Files dropped on the box or one of its children (bubbling up).</summary>
    public Action<FileDropEventArgs>? OnFileDrop { get; init; }

    /// <summary>A key pressed while the box or one of its children has focus.</summary>
    public Action<KeyEventArgs>? OnKeyDown { get; init; }

    /// <summary>A key pressed, on the way down to the focused box (before <see cref="OnKeyDown"/>).</summary>
    public Action<KeyEventArgs>? OnKeyDownCapture { get; init; }

    /// <summary>A key released while the box or one of its children has focus.</summary>
    public Action<KeyEventArgs>? OnKeyUp { get; init; }

    /// <summary>Text typed while the box or one of its children has focus.</summary>
    public Action<TextInputEventArgs>? OnTextInput { get; init; }

    /// <summary>The box took focus (not bubbling).</summary>
    public Action<FocusEventArgs>? OnFocus { get; init; }

    /// <summary>The box lost focus (not bubbling).</summary>
    public Action<FocusEventArgs>? OnBlur { get; init; }

    internal override RenderNode CreateRenderNode() => new BoxRenderNode();

    internal override IReadOnlyList<Element?> ChildElements => Children;
}
