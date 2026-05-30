using System;
using System.Collections.Generic;
using System.Numerics;
using Radiant.Graphics2D;
using Radiant.Input;
using Radiant.Scrolling;
using Silk.NET.Input;

namespace Radiant.UI;

/// <summary>
/// Vertical list of selectable text rows with mouse-wheel scrolling. Rows are
/// scissored to the list bounds via <see cref="Renderer2D.PushClip"/>. Scroll state
/// is delegated to the shared <see cref="ScrollController"/> (clamped, wheel-step =
/// <see cref="RowHeight"/>) so the offset/clamp/scrollbar logic isn't reimplemented here.
/// </summary>
public class ListView : UIElement
{
    private readonly MsdfFont _font;
    private readonly ScrollController _scroll =
        new(new ScrollBehaviour { Overscroll = OverscrollMode.Clamp, Indicators = IndicatorVisibility.None });
    private int _hoveredIndex = -1;
    private int _selectedIndex;
    private bool _mouseOver;

    public ListView(MsdfFont font) { _font = font; }

    /// <summary>Current vertical scroll offset (content units). Exposed for tests.</summary>
    internal float ScrollOffset => _scroll.Offset.Y;

    public IReadOnlyList<string> Items { get; set; } = Array.Empty<string>();

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => _selectedIndex = Math.Clamp(value, 0, Math.Max(0, Items.Count - 1));
    }

    public float RowHeight { get; set; } = 28f;
    public float Padding { get; set; } = 8f;
    public float ScrollbarWidth { get; set; } = 6f;
    public Vector4 BackgroundColor { get; set; } = UIColors.BackgroundDark;
    public Vector4 SelectedColor { get; set; } = UIColors.Primary;
    public Vector4 HoverColor { get; set; } = UIColors.BackgroundLight;
    public Vector4 TextColor { get; set; } = UIColors.Text;
    public Vector4 SelectedTextColor { get; set; } = UIColors.Text;

    public event Action<int>? SelectionChanged;

    public override bool IsCapturingInput => _mouseOver && _scroll.CanScrollVertical;

    private float VisibleHeight => Math.Max(0f, Size.Y - Padding * 2);
    private float ContentHeight => Items.Count * RowHeight;
    private float MaxScrollOffset => Math.Max(0f, ContentHeight - VisibleHeight);

    public override void Update(InputState input, double deltaTime)
    {
        if (!Visible || !Enabled) return;

        _mouseOver = ContainsPoint(input.MousePosition);

        _scroll.Behaviour = _scroll.Behaviour with { WheelStep = RowHeight };
        _scroll.SetExtents(new Vector2(0, VisibleHeight), new Vector2(0, ContentHeight));
        if (_mouseOver && MathF.Abs(input.ScrollDelta.Y) > 0.001f)
            _scroll.ApplyWheel(new Vector2(0, input.ScrollDelta.Y));
        _scroll.Update(deltaTime);

        _hoveredIndex = -1;
        if (_mouseOver)
        {
            var relY = input.MousePosition.Y - (Position.Y + Padding) + _scroll.Offset.Y;
            var idx = (int)(relY / RowHeight);
            if (idx >= 0 && idx < Items.Count) _hoveredIndex = idx;

            if (input.IsMouseButtonPressed(MouseButton.Left) && _hoveredIndex >= 0)
            {
                if (_hoveredIndex != _selectedIndex)
                {
                    _selectedIndex = _hoveredIndex;
                    SelectionChanged?.Invoke(_selectedIndex);
                }
            }
        }
    }

    public override void Draw(Renderer2D renderer)
    {
        if (!Visible) return;

        renderer.DrawFilledRect(Position, Size, BackgroundColor);

        var contentLeft = Position.X + Padding;
        var contentTop = Position.Y + Padding;
        var rowWidth = Math.Max(0f, Size.X - Padding * 2 - (MaxScrollOffset > 0 ? ScrollbarWidth + 4f : 0f));

        renderer.PushClip(contentLeft, contentTop, rowWidth, VisibleHeight);
        for (var i = 0; i < Items.Count; i++)
        {
            var y = contentTop + i * RowHeight - _scroll.Offset.Y;
            // Skip rows clearly outside the clipped band to save vertex work.
            if (y + RowHeight < contentTop - RowHeight) continue;
            if (y > contentTop + VisibleHeight + RowHeight) continue;

            var rowPos = new Vector2(contentLeft, y);
            var rowSize = new Vector2(rowWidth, RowHeight);

            if (i == _selectedIndex)
                renderer.DrawFilledRect(rowPos, rowSize, SelectedColor);
            else if (i == _hoveredIndex)
                renderer.DrawFilledRect(rowPos, rowSize, HoverColor);

            var textY = y + (RowHeight - 8f) / 2f;
            var color = i == _selectedIndex ? SelectedTextColor : TextColor;
            renderer.DrawText(_font, Items[i], new Vector2(rowPos.X + 8f, textY), color);
        }
        renderer.PopClip();

        var maxScroll = MaxScrollOffset;
        if (maxScroll > 0f)
        {
            var trackPos = new Vector2(Position.X + Size.X - Padding - ScrollbarWidth, contentTop);
            var trackSize = new Vector2(ScrollbarWidth, VisibleHeight);
            renderer.DrawFilledRect(trackPos, trackSize, UIColors.BackgroundLight);

            var thumbHeight = Math.Max(20f, VisibleHeight * (VisibleHeight / ContentHeight));
            var thumbY = trackPos.Y + (_scroll.Offset.Y / maxScroll) * (VisibleHeight - thumbHeight);
            renderer.DrawFilledRect(new Vector2(trackPos.X, thumbY), new Vector2(ScrollbarWidth, thumbHeight), UIColors.Border);
        }
    }
}
