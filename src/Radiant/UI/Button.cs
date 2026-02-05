using System;
using System.Numerics;
using Radiant.Graphics2D;
using Radiant.Input;
using Silk.NET.Input;

namespace Radiant.UI;

/// <summary>
/// A clickable button UI element.
/// </summary>
public class Button : UIElement
{
    private bool _isHovered;
    private bool _isPressed;

    /// <summary>The button label text.</summary>
    public string Text { get; set; } = "Button";

    /// <summary>Background color when idle.</summary>
    public Vector4 BackgroundColor { get; set; } = UIColors.BackgroundLight;

    /// <summary>Background color when hovered.</summary>
    public Vector4 HoverColor { get; set; } = UIColors.Primary;

    /// <summary>Background color when pressed.</summary>
    public Vector4 PressedColor { get; set; } = UIColors.PrimaryActive;

    /// <summary>Text color.</summary>
    public Vector4 TextColor { get; set; } = UIColors.Text;

    /// <summary>Border color.</summary>
    public Vector4 BorderColor { get; set; } = UIColors.Border;

    /// <summary>Corner radius for rounded buttons.</summary>
    public float CornerRadius { get; set; } = 4f;

    /// <summary>Padding around the text.</summary>
    public float Padding { get; set; } = 8f;

    /// <summary>Event raised when the button is clicked.</summary>
    public event Action? Clicked;

    /// <inheritdoc/>
    public override bool IsCapturingInput => _isPressed;

    public Button() { }

    public Button(string text, Vector2 position, Vector2 size)
    {
        Text = text;
        Position = position;
        Size = size;
    }

    public override void Update(InputState input, double deltaTime)
    {
        if (!Visible || !Enabled) return;

        var wasHovered = _isHovered;
        _isHovered = ContainsPoint(input.MousePosition);

        if (_isHovered && !wasHovered)
            OnMouseEnter();
        else if (!_isHovered && wasHovered)
            OnMouseLeave();

        if (_isHovered)
        {
            if (input.IsMouseButtonPressed(MouseButton.Left))
            {
                _isPressed = true;
                OnMouseDown(input.MousePosition - Position);
            }

            if (input.IsMouseButtonReleased(MouseButton.Left) && _isPressed)
            {
                _isPressed = false;
                OnMouseUp(input.MousePosition - Position);
                Clicked?.Invoke();
            }
        }
        else
        {
            _isPressed = false;
        }
    }

    public override void Draw(Renderer2D renderer)
    {
        if (!Visible) return;

        var bgColor = _isPressed ? PressedColor :
                      _isHovered ? HoverColor :
                      BackgroundColor;

        if (!Enabled)
            bgColor = UIColors.BackgroundDark;

        // Draw background
        renderer.DrawFilledRect(Position, Size, bgColor);

        // Draw border
        renderer.DrawRect(Position, Size, BorderColor);

        // Draw text centered
        var textWidth = Text.Length * 8;
        var textHeight = 12f;
        var textPos = Position + new Vector2(
            (Size.X - textWidth) / 2,
            (Size.Y - textHeight) / 2);

        renderer.DrawText(Text, textPos, Enabled ? TextColor : UIColors.TextDisabled);
    }
}
