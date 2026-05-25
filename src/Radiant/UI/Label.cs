using System.Numerics;
using Radiant.Graphics2D;

namespace Radiant.UI;

/// <summary>
/// A text label UI element.
/// </summary>
public class Label : UIElement
{
    private readonly MsdfFont _font;

    /// <summary>The text to display.</summary>
    public string Text { get; set; } = "";

    /// <summary>Text color.</summary>
    public Vector4 TextColor { get; set; } = UIColors.Text;

    /// <summary>Text scale (1.0 = normal size).</summary>
    public float TextScale { get; set; } = 1f;

    public Label(MsdfFont font) { _font = font; }

    public Label(MsdfFont font, string text, Vector2 position)
    {
        _font = font;
        Text = text;
        Position = position;
        Size = new Vector2(text.Length * 8 * TextScale, 16 * TextScale);
    }

    public override void Draw(Renderer2D renderer)
    {
        if (!Visible || string.IsNullOrEmpty(Text)) return;

        renderer.DrawText(_font, Text, Position, TextColor, TextScale * 7f);
    }
}
