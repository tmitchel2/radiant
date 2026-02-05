using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Radiant.Graphics2D;
using Radiant.Input;

namespace Radiant.UI;

/// <summary>
/// A container panel that holds other UI elements.
/// </summary>
public class Panel : UIElement
{
    private readonly List<UIElement> _children = [];

    /// <summary>Background color.</summary>
    public Vector4 BackgroundColor { get; set; } = UIColors.Background;

    /// <summary>Border color.</summary>
    public Vector4 BorderColor { get; set; } = UIColors.Border;

    /// <summary>Whether to draw the background.</summary>
    public bool DrawBackground { get; set; } = true;

    /// <summary>Whether to draw the border.</summary>
    public bool DrawBorder { get; set; } = true;

    /// <summary>Padding inside the panel.</summary>
    public float Padding { get; set; } = 8f;

    /// <summary>Title displayed at the top of the panel.</summary>
    public string Title { get; set; } = "";

    /// <summary>Gets the children of this panel.</summary>
    public IReadOnlyList<UIElement> Children => _children;

    /// <inheritdoc/>
    public override bool IsCapturingInput => _children.Any(c => c.IsCapturingInput);

    public Panel() { }

    public Panel(Vector2 position, Vector2 size)
    {
        Position = position;
        Size = size;
    }

    /// <summary>Adds a child element to this panel.</summary>
    public T Add<T>(T element) where T : UIElement
    {
        _children.Add(element);
        return element;
    }

    /// <summary>Removes a child element from this panel.</summary>
    public bool Remove(UIElement element) => _children.Remove(element);

    /// <summary>Clears all children from this panel.</summary>
    public void Clear() => _children.Clear();

    public override void Update(InputState input, double deltaTime)
    {
        if (!Visible || !Enabled) return;

        // Update children in reverse order (top elements first)
        for (var i = _children.Count - 1; i >= 0; i--)
        {
            _children[i].Update(input, deltaTime);
        }
    }

    public override void Draw(Renderer2D renderer)
    {
        if (!Visible) return;

        // Draw background
        if (DrawBackground)
        {
            renderer.DrawFilledRect(Position, Size, BackgroundColor);
        }

        // Draw border
        if (DrawBorder)
        {
            renderer.DrawRect(Position, Size, BorderColor);
        }

        // Draw title
        if (!string.IsNullOrEmpty(Title))
        {
            var titlePos = Position + new Vector2(Padding, Padding);
            renderer.DrawText(Title, titlePos, UIColors.Text, 1.2f);
        }

        // Draw children
        foreach (var child in _children)
        {
            child.Draw(renderer);
        }
    }

    /// <summary>Creates a vertical layout of labeled sliders.</summary>
    public void AddSliderGroup(string groupLabel, float startY, float width, params (string label, float min, float max, float initial, Slider slider)[] sliders)
    {
        var currentY = startY;
        const float sliderHeight = 30f;

        if (!string.IsNullOrEmpty(groupLabel))
        {
            Add(new Label(groupLabel, new Vector2(Position.X + Padding, currentY)) { TextColor = UIColors.TextDim });
            currentY += 20f;
        }

        foreach (var (label, min, max, initial, slider) in sliders)
        {
            slider.Label = label;
            slider.MinValue = min;
            slider.MaxValue = max;
            slider.Value = initial;
            slider.Position = new Vector2(Position.X + Padding, currentY);
            slider.Size = new Vector2(width - Padding * 2, 24f);
            Add(slider);
            currentY += sliderHeight;
        }
    }
}
