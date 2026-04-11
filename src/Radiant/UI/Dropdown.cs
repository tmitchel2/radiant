using System;
using System.Numerics;
using Radiant.Graphics2D;
using Radiant.Input;
using Silk.NET.Input;

namespace Radiant.UI;

/// <summary>
/// A dropdown selector UI element that shows a list of items when clicked.
/// </summary>
public class Dropdown : UIElement
{
    private bool _isOpen;
    private int _hoveredIndex = -1;
    private int _selectedIndex;
    private float _scrollOffset;

    /// <summary>The items to display in the dropdown list.</summary>
    public string[] Items { get; set; } = [];

    /// <summary>The currently selected item index.</summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set => _selectedIndex = Math.Clamp(value, 0, Math.Max(0, Items.Length - 1));
    }

    /// <summary>The text of the currently selected item.</summary>
    public string SelectedText => Items.Length > 0 && _selectedIndex >= 0 && _selectedIndex < Items.Length
        ? Items[_selectedIndex]
        : "";

    /// <summary>Background color when idle.</summary>
    public Vector4 BackgroundColor { get; set; } = UIColors.BackgroundLight;

    /// <summary>Background color when hovered.</summary>
    public Vector4 HoverColor { get; set; } = UIColors.Primary;

    /// <summary>Text color.</summary>
    public Vector4 TextColor { get; set; } = UIColors.Text;

    /// <summary>Border color.</summary>
    public Vector4 BorderColor { get; set; } = UIColors.Border;

    /// <summary>Background color for the dropdown list.</summary>
    public Vector4 ListBackgroundColor { get; set; } = UIColors.BackgroundDark;

    /// <summary>Height of each item row in the dropdown list.</summary>
    public float ItemHeight { get; set; } = 25f;

    /// <summary>Maximum height of the open list. When the full list would exceed this,
    /// the list becomes scrollable via the mouse wheel.</summary>
    public float MaxListHeight { get; set; } = 200f;

    /// <summary>Width of the scrollbar track drawn when the list is scrollable.</summary>
    public float ScrollbarWidth { get; set; } = 6f;

    /// <summary>Event raised when the selection changes.</summary>
    public event Action<int>? SelectionChanged;

    /// <inheritdoc/>
    public override bool IsCapturingInput => _isOpen;

    public Dropdown() { }

    public Dropdown(string[] items, int selectedIndex, Vector2 position, Vector2 size)
    {
        Items = items;
        _selectedIndex = selectedIndex;
        Position = position;
        Size = size;
    }

    private float VisibleListHeight =>
        Math.Min(Items.Length * ItemHeight, MaxListHeight);

    private float MaxScrollOffset =>
        Math.Max(0f, Items.Length * ItemHeight - VisibleListHeight);

    public override void Update(InputState input, double deltaTime)
    {
        if (!Visible || !Enabled) return;

        var mousePos = input.MousePosition;
        var clicked = input.IsMouseButtonPressed(MouseButton.Left);

        if (_isOpen)
        {
            var listTop = Position.Y + Size.Y;
            var visibleHeight = VisibleListHeight;
            var listBottom = listTop + visibleHeight;
            var mouseOverList =
                mousePos.X >= Position.X && mousePos.X < Position.X + Size.X &&
                mousePos.Y >= listTop && mousePos.Y < listBottom;

            // Scroll wheel handling
            if (mouseOverList && MathF.Abs(input.ScrollDelta.Y) > 0.001f)
            {
                _scrollOffset -= input.ScrollDelta.Y * ItemHeight;
                _scrollOffset = Math.Clamp(_scrollOffset, 0f, MaxScrollOffset);
            }

            // Check hover over list items (accounting for scroll offset)
            _hoveredIndex = -1;
            if (mouseOverList)
            {
                var relativeY = mousePos.Y - listTop + _scrollOffset;
                var idx = (int)(relativeY / ItemHeight);
                if (idx >= 0 && idx < Items.Length)
                    _hoveredIndex = idx;
            }

            if (clicked)
            {
                if (_hoveredIndex >= 0)
                {
                    _selectedIndex = _hoveredIndex;
                    _isOpen = false;
                    _hoveredIndex = -1;
                    SelectionChanged?.Invoke(_selectedIndex);
                }
                else
                {
                    // Click outside list — close without changing selection
                    _isOpen = false;
                    _hoveredIndex = -1;
                }
            }
        }
        else
        {
            if (clicked && ContainsPoint(mousePos))
            {
                _isOpen = true;
                // Scroll so the selected item is visible when the list opens
                var selectedTop = _selectedIndex * ItemHeight;
                var visibleHeight = VisibleListHeight;
                if (selectedTop < _scrollOffset)
                    _scrollOffset = selectedTop;
                else if (selectedTop + ItemHeight > _scrollOffset + visibleHeight)
                    _scrollOffset = selectedTop + ItemHeight - visibleHeight;
                _scrollOffset = Math.Clamp(_scrollOffset, 0f, MaxScrollOffset);
            }
        }
    }

    public override void Draw(Renderer2D renderer)
    {
        if (!Visible) return;

        // Draw header (same style as a button)
        var bgColor = _isOpen ? UIColors.PrimaryActive : BackgroundColor;
        renderer.DrawFilledRect(Position, Size, bgColor);
        renderer.DrawRect(Position, Size, BorderColor);

        // Draw selected text
        var textHeight = 12f;
        var textPos = Position + new Vector2(8, (Size.Y - textHeight) / 2);
        renderer.DrawText(SelectedText, textPos, TextColor);

        // Draw chevron arrow
        var chevron = _isOpen ? "^" : "v";
        var chevronPos = Position + new Vector2(Size.X - 16, (Size.Y - textHeight) / 2);
        renderer.DrawText(chevron, chevronPos, TextColor);
    }

    public override void DrawOverlay(Renderer2D renderer)
    {
        if (!Visible || !_isOpen) return;

        var listTop = Position.Y + Size.Y;
        var visibleHeight = VisibleListHeight;
        var listPos = new Vector2(Position.X, listTop);
        var listSize = new Vector2(Size.X, visibleHeight);

        // Draw list background
        renderer.DrawFilledRect(listPos, listSize, ListBackgroundColor);

        // Draw only the items that fall within the visible window
        var textHeight = 12f;
        var firstVisible = (int)MathF.Floor(_scrollOffset / ItemHeight);
        var lastVisible = (int)MathF.Ceiling((_scrollOffset + visibleHeight) / ItemHeight);
        firstVisible = Math.Max(0, firstVisible);
        lastVisible = Math.Min(Items.Length, lastVisible);

        for (var i = firstVisible; i < lastVisible; i++)
        {
            var itemY = listTop + i * ItemHeight - _scrollOffset;
            var itemPos = new Vector2(Position.X, itemY);
            var itemSize = new Vector2(Size.X, ItemHeight);

            // Top/bottom edges of the item may fall outside the visible window —
            // this pass doesn't clip, so partially-visible rows are drawn in full.
            if (i == _hoveredIndex)
            {
                renderer.DrawFilledRect(itemPos, itemSize, HoverColor);
            }
            else if (i == _selectedIndex)
            {
                renderer.DrawFilledRect(itemPos, itemSize, UIColors.BackgroundLight);
            }

            var textPos = itemPos + new Vector2(8, (ItemHeight - textHeight) / 2);
            renderer.DrawText(Items[i], textPos, TextColor);
        }

        // Draw scrollbar if the list is longer than the visible window
        var maxScroll = MaxScrollOffset;
        if (maxScroll > 0f)
        {
            var trackPos = new Vector2(listPos.X + listSize.X - ScrollbarWidth, listPos.Y);
            var trackSize = new Vector2(ScrollbarWidth, listSize.Y);
            renderer.DrawFilledRect(trackPos, trackSize, UIColors.BackgroundLight);

            var totalContent = Items.Length * ItemHeight;
            var thumbHeight = Math.Max(20f, listSize.Y * (visibleHeight / totalContent));
            var thumbY = listPos.Y + (_scrollOffset / maxScroll) * (listSize.Y - thumbHeight);
            var thumbPos = new Vector2(trackPos.X, thumbY);
            var thumbSize = new Vector2(ScrollbarWidth, thumbHeight);
            renderer.DrawFilledRect(thumbPos, thumbSize, UIColors.Border);
        }

        // Draw list border
        renderer.DrawRect(listPos, listSize, BorderColor);
    }
}
