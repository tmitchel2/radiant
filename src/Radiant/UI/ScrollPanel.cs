using System;
using System.Collections.Generic;
using System.Numerics;
using Radiant.Graphics2D;
using Radiant.Input;

namespace Radiant.UI;

/// <summary>
/// A vertically scrollable container for arbitrary child elements. Children
/// are positioned in absolute coordinates the same way they are inside a
/// normal <see cref="Panel"/>; scrolling shifts their positions by the delta
/// so existing "set position once at add time" code works unchanged.
/// The scrollable viewport is scissor-clipped via
/// <see cref="Renderer2D.PushClip"/>.
/// </summary>
public class ScrollPanel : UIElement
{
    private readonly List<UIElement> _children = [];
    private float _scrollOffset;
    private float _appliedOffset;

    public Vector4 BackgroundColor { get; set; } = UIColors.Background;
    public bool DrawBackground { get; set; } = true;
    public float ScrollbarWidth { get; set; } = 6f;
    public float WheelStep { get; set; } = 28f;

    /// <summary>
    /// Total height of content. Callers must set this after adding children
    /// (typically to the maximum bottom-edge of added children relative to
    /// this panel's top). Used to compute max scroll extent.
    /// </summary>
    public float ContentHeight { get; set; }

    public IReadOnlyList<UIElement> Children => _children;

    public override bool IsCapturingInput
    {
        get
        {
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
        _scrollOffset = 0;
        _appliedOffset = 0;
        ContentHeight = 0;
    }

    private float MaxScrollOffset => Math.Max(0f, ContentHeight - Size.Y);

    public override void Update(InputState input, double deltaTime)
    {
        if (!Visible || !Enabled) return;

        if (ContainsPoint(input.MousePosition) && MathF.Abs(input.ScrollDelta.Y) > 0.001f)
        {
            _scrollOffset -= input.ScrollDelta.Y * WheelStep;
            _scrollOffset = Math.Clamp(_scrollOffset, 0f, MaxScrollOffset);
        }

        var delta = _scrollOffset - _appliedOffset;
        if (MathF.Abs(delta) > 0.001f)
        {
            foreach (var c in _children) ShiftRecursive(c, -delta);
            _appliedOffset = _scrollOffset;
        }

        for (var i = _children.Count - 1; i >= 0; i--)
        {
            if (_children[i].Visible && _children[i].IsCapturingInput)
            {
                _children[i].Update(input, deltaTime);
                return;
            }
        }

        for (var i = _children.Count - 1; i >= 0; i--)
        {
            if (_children[i].Visible)
                _children[i].Update(input, deltaTime);
        }
    }

    private static void ShiftRecursive(UIElement element, float dy)
    {
        element.Position += new Vector2(0, dy);
        if (element is Panel panel)
        {
            foreach (var child in panel.Children)
                ShiftRecursive(child, dy);
        }
        else if (element is ScrollPanel sp)
        {
            foreach (var child in sp._children)
                ShiftRecursive(child, dy);
        }
    }

    public override void Draw(Renderer2D renderer)
    {
        if (!Visible) return;

        if (DrawBackground)
            renderer.DrawFilledRect(Position, Size, BackgroundColor);

        renderer.PushClip(Position.X, Position.Y, Size.X, Size.Y);
        foreach (var c in _children)
            if (c.Visible) c.Draw(renderer);
        renderer.PopClip();

        var maxScroll = MaxScrollOffset;
        if (maxScroll > 0f)
        {
            var trackPos = new Vector2(Position.X + Size.X - ScrollbarWidth, Position.Y);
            var trackSize = new Vector2(ScrollbarWidth, Size.Y);
            renderer.DrawFilledRect(trackPos, trackSize, UIColors.BackgroundLight);

            var thumbHeight = Math.Max(20f, Size.Y * (Size.Y / ContentHeight));
            var thumbY = trackPos.Y + (_scrollOffset / maxScroll) * (Size.Y - thumbHeight);
            renderer.DrawFilledRect(new Vector2(trackPos.X, thumbY), new Vector2(ScrollbarWidth, thumbHeight), UIColors.Border);
        }
    }

    public override void DrawOverlay(Renderer2D renderer)
    {
        // Overlays (dropdown popups) draw outside the clip so they can
        // extend beyond the scroll viewport.
        foreach (var c in _children)
            if (c.Visible) c.DrawOverlay(renderer);
    }

    public void ResetScroll()
    {
        if (MathF.Abs(_appliedOffset) > 0.001f)
        {
            foreach (var c in _children) ShiftRecursive(c, _appliedOffset);
        }
        _scrollOffset = 0;
        _appliedOffset = 0;
    }
}
