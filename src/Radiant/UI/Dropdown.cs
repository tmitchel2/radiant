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

    public override void Update(InputState input, double deltaTime)
    {
        if (!Visible || !Enabled) return;

        var mousePos = input.MousePosition;
        var clicked = input.IsMouseButtonPressed(MouseButton.Left);

        if (_isOpen)
        {
            // Check hover over list items
            var listTop = Position.Y + Size.Y;
            _hoveredIndex = -1;

            for (var i = 0; i < Items.Length; i++)
            {
                var itemY = listTop + i * ItemHeight;
                if (mousePos.X >= Position.X && mousePos.X < Position.X + Size.X &&
                    mousePos.Y >= itemY && mousePos.Y < itemY + ItemHeight)
                {
                    _hoveredIndex = i;
                    break;
                }
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
        var listHeight = Items.Length * ItemHeight;
        var listPos = new Vector2(Position.X, listTop);
        var listSize = new Vector2(Size.X, listHeight);

        // Draw list background
        renderer.DrawFilledRect(listPos, listSize, ListBackgroundColor);

        // Draw items
        for (var i = 0; i < Items.Length; i++)
        {
            var itemY = listTop + i * ItemHeight;
            var itemPos = new Vector2(Position.X, itemY);
            var itemSize = new Vector2(Size.X, ItemHeight);

            if (i == _hoveredIndex)
            {
                renderer.DrawFilledRect(itemPos, itemSize, HoverColor);
            }
            else if (i == _selectedIndex)
            {
                renderer.DrawFilledRect(itemPos, itemSize, UIColors.BackgroundLight);
            }

            var textHeight = 12f;
            var textPos = itemPos + new Vector2(8, (ItemHeight - textHeight) / 2);
            renderer.DrawText(Items[i], textPos, TextColor);
        }

        // Draw list border
        renderer.DrawRect(listPos, listSize, BorderColor);
    }
}
