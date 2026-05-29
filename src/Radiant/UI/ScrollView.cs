using System;
using System.Collections.Generic;
using System.Numerics;
using Radiant.Animation;
using Radiant.Graphics2D;
using Radiant.Input;
using Radiant.Layout;
using Radiant.Scrolling;

namespace Radiant.UI;

/// <summary>
/// Scrollable container backed by the shared <see cref="ScrollController"/> physics
/// engine. Unlike the legacy <see cref="ScrollPanel"/> (which physically shifts each
/// child's position), <see cref="ScrollView"/> keeps children at their natural layout
/// positions and scrolls by a <em>render-time translate</em>
/// (<see cref="Renderer2D.PushScrollOffset"/>) — O(1) per frame, sub-pixel safe, and
/// able to express momentum, bounce, snap and animated programmatic scroll.
///
/// Children are hit-tested in content space: while updating children the pointer is
/// shifted by the current offset so input lines up with the translated render.
/// </summary>
public class ScrollView : UIElement, IUiContainer, ILayoutBoundary, IAnimating
{
    private readonly List<UIElement> _children = [];
    private bool _mouseOver;

    public ScrollView(ScrollBehaviour? behaviour = null)
    {
        Controller = new ScrollController(behaviour ?? new ScrollBehaviour());
    }

    /// <summary>The scroll physics engine. Subscribe to its events; call its programmatic API.</summary>
    public ScrollController Controller { get; }

    public Vector4 BackgroundColor { get; set; } = UIColors.Background;
    public bool DrawBackground { get; set; } = true;
    public float ScrollbarWidth { get; set; } = 6f;

    public IReadOnlyList<UIElement> Children => _children;

    /// <inheritdoc/>
    public bool IsAnimating => Controller.IsAnimating;

    public override bool IsCapturingInput
    {
        get
        {
            // Hovering scrollable content consumes the wheel — report capture so the
            // event can't fall through to whatever is underneath (e.g. a 3D camera).
            if (_mouseOver && (Controller.CanScrollVertical || Controller.CanScrollHorizontal))
                return true;
            foreach (var c in _children)
                if (c.IsCapturingInput) return true;
            return false;
        }
    }

    public T Add<T>(T element) where T : UIElement
    {
        _children.Add(element);
        return element;
    }

    public void Clear()
    {
        _children.Clear();
        Controller.ScrollTo(Vector2.Zero, animated: false);
    }

    /// <summary>Scroll to an absolute content offset.</summary>
    public void ScrollTo(Vector2 offset, bool animated) => Controller.ScrollTo(offset, animated);

    /// <summary>Reset the scroll position to the origin.</summary>
    public void ResetScroll() => Controller.ScrollTo(Vector2.Zero, animated: false);

    public override void Update(InputState input, double deltaTime)
    {
        if (!Visible || !Enabled) return;

        _mouseOver = ContainsPoint(input.MousePosition);
        Controller.SetExtents(Size, MeasureContentSize());

        // Phase 2: poll the wheel directly (the gesture arbiter replaces this in Phase 3).
        if (_mouseOver && MathF.Abs(input.ScrollDelta.Y) > 0.001f && Controller.CanScrollVertical)
            Controller.ApplyWheel(new Vector2(0, input.ScrollDelta.Y));
        if (_mouseOver && MathF.Abs(input.ScrollDelta.X) > 0.001f && Controller.CanScrollHorizontal)
            Controller.ApplyWheel(new Vector2(input.ScrollDelta.X, 0));

        Controller.Update(deltaTime);

        // Update children in content space so their hit-testing matches the translated render.
        var offset = Controller.Offset;
        var savedMouse = input.MousePosition;
        input.MousePosition = savedMouse + offset;

        for (var i = _children.Count - 1; i >= 0; i--)
        {
            if (_children[i].Visible && _children[i].IsCapturingInput)
            {
                _children[i].Update(input, deltaTime);
                input.MousePosition = savedMouse;
                return;
            }
        }

        for (var i = _children.Count - 1; i >= 0; i--)
            if (_children[i].Visible)
                _children[i].Update(input, deltaTime);

        input.MousePosition = savedMouse;
    }

    public override void Draw(Renderer2D renderer)
    {
        if (!Visible) return;

        if (DrawBackground)
            renderer.DrawFilledRect(Position, Size, BackgroundColor);

        renderer.PushClip(Position.X, Position.Y, Size.X, Size.Y);
        renderer.PushScrollOffset(-Controller.Offset);
        foreach (var c in _children)
            if (c.Visible) c.Draw(renderer);
        renderer.PopScrollOffset();
        renderer.PopClip();

        DrawScrollbar(renderer);
    }

    public override void DrawOverlay(Renderer2D renderer)
    {
        // Overlays (e.g. dropdown popups) draw outside the clip but still follow the scroll.
        renderer.PushScrollOffset(-Controller.Offset);
        foreach (var c in _children)
            if (c.Visible) c.DrawOverlay(renderer);
        renderer.PopScrollOffset();
    }

    private Vector2 MeasureContentSize()
    {
        var overrideExtent = Controller.Behaviour.ContentExtentOverride;
        var width = Size.X;
        var height = Size.Y;

        if (overrideExtent is { } ov)
        {
            // The override applies to the primary scroll axis.
            if (Controller.Behaviour.Axes == ScrollAxes.Horizontal) width = ov;
            else height = ov;
            return new Vector2(width, height);
        }

        var maxRight = 0f;
        var maxBottom = 0f;
        foreach (var c in _children)
        {
            if (!c.Visible) continue;
            maxRight = MathF.Max(maxRight, (c.Position.X - Position.X) + c.Size.X);
            maxBottom = MathF.Max(maxBottom, (c.Position.Y - Position.Y) + c.Size.Y);
        }
        return new Vector2(MathF.Max(width, maxRight), MathF.Max(height, maxBottom));
    }

    private void DrawScrollbar(Renderer2D renderer)
    {
        if (Controller.Behaviour.Indicators == IndicatorVisibility.None) return;
        if (!Controller.CanScrollVertical) return;

        var maxScroll = Controller.MaxOffset.Y;
        var content = Controller.ContentSize.Y;
        if (maxScroll <= 0f || content <= 0f) return;

        var trackPos = new Vector2(Position.X + Size.X - ScrollbarWidth, Position.Y);
        var trackSize = new Vector2(ScrollbarWidth, Size.Y);
        renderer.DrawFilledRect(trackPos, trackSize, UIColors.BackgroundLight);

        var thumbHeight = MathF.Max(20f, Size.Y * (Size.Y / content));
        var thumbY = trackPos.Y + (Controller.Offset.Y / maxScroll) * (Size.Y - thumbHeight);
        renderer.DrawFilledRect(
            new Vector2(trackPos.X, thumbY),
            new Vector2(ScrollbarWidth, thumbHeight),
            UIColors.Border);
    }
}
