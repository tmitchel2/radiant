using System;
using System.Numerics;
using Radiant.Graphics2D;
using Radiant.Input;
using Silk.NET.Input;

namespace Radiant.UI;

/// <summary>
/// A draggable slider UI element for numeric input.
/// </summary>
public class Slider : UIElement
{
    private bool _isHovered;
    private bool _isDragging;

    /// <summary>The current value.</summary>
    public float Value { get; set; }

    /// <summary>Minimum value.</summary>
    public float MinValue { get; set; }

    /// <summary>Maximum value.</summary>
    public float MaxValue { get; set; } = 1f;

    /// <summary>Step increment (0 for continuous).</summary>
    public float Step { get; set; }

    /// <summary>Optional label shown before the slider.</summary>
    public string Label { get; set; } = "";

    /// <summary>Whether to show the current value as text.</summary>
    public bool ShowValue { get; set; } = true;

    /// <summary>Format string for displaying the value.</summary>
    public string ValueFormat { get; set; } = "F2";

    /// <summary>Track color.</summary>
    public Vector4 TrackColor { get; set; } = UIColors.SliderTrack;

    /// <summary>Filled track color.</summary>
    public Vector4 FillColor { get; set; } = UIColors.Primary;

    /// <summary>Handle color.</summary>
    public Vector4 HandleColor { get; set; } = UIColors.SliderHandle;

    /// <summary>Handle color when hovered.</summary>
    public Vector4 HandleHoverColor { get; set; } = UIColors.SliderHandleHover;

    /// <summary>Handle color when dragging.</summary>
    public Vector4 HandleActiveColor { get; set; } = UIColors.SliderHandleActive;

    /// <summary>Text color.</summary>
    public Vector4 TextColor { get; set; } = UIColors.Text;

    /// <summary>Whether this slider has keyboard focus.</summary>
    public bool IsFocused { get; set; }

    /// <summary>Border color when focused.</summary>
    public Vector4 FocusBorderColor { get; set; } = UIColors.Primary;

    /// <summary>Height of the track.</summary>
    public float TrackHeight { get; set; } = 4f;

    /// <summary>Radius of the handle.</summary>
    public float HandleRadius { get; set; } = 8f;

    /// <summary>Event raised when the value changes.</summary>
    public event Action<float>? ValueChanged;

    /// <inheritdoc/>
    public override bool IsCapturingInput => _isDragging;

    public Slider() { }

    public Slider(string label, float minValue, float maxValue, float initialValue, Vector2 position, float width)
    {
        Label = label;
        MinValue = minValue;
        MaxValue = maxValue;
        Value = initialValue;
        Position = position;
        Size = new Vector2(width, 24f);
    }

    /// <summary>Normalized value between 0 and 1.</summary>
    public float NormalizedValue
    {
        get => MaxValue > MinValue ? (Value - MinValue) / (MaxValue - MinValue) : 0f;
        set => SetValue(MinValue + value * (MaxValue - MinValue));
    }

    public void SetValue(float value)
    {
        value = Math.Clamp(value, MinValue, MaxValue);

        if (Step > 0)
        {
            value = MathF.Round(value / Step) * Step;
        }

        if (Math.Abs(Value - value) > float.Epsilon)
        {
            Value = value;
            ValueChanged?.Invoke(Value);
        }
    }

    private (float trackX, float trackWidth, float handleX) GetTrackGeometry()
    {
        var labelWidth = string.IsNullOrEmpty(Label) ? 0f : (Label.Length * 8 + 8);
        var valueWidth = ShowValue ? 50f : 0f;

        var trackX = Position.X + labelWidth;
        var trackWidth = Size.X - labelWidth - valueWidth - HandleRadius;
        var handleX = trackX + NormalizedValue * trackWidth;

        return (trackX, trackWidth, handleX);
    }

    public override void Update(InputState input, double deltaTime)
    {
        if (!Visible || !Enabled) return;

        var (trackX, trackWidth, handleX) = GetTrackGeometry();
        var handleY = Position.Y + Size.Y / 2;
        var handleBounds = new Rectangle(
            handleX - HandleRadius,
            handleY - HandleRadius,
            HandleRadius * 2,
            HandleRadius * 2);

        _isHovered = handleBounds.Contains(input.MousePosition) || ContainsPoint(input.MousePosition);

        if (_isHovered && input.IsMouseButtonPressed(MouseButton.Left))
        {
            _isDragging = true;
        }

        if (_isDragging)
        {
            if (input.IsMouseButtonDown(MouseButton.Left))
            {
                var normalizedX = (input.MousePosition.X - trackX) / trackWidth;
                NormalizedValue = Math.Clamp(normalizedX, 0f, 1f);
            }
            else
            {
                _isDragging = false;
            }
        }
    }

    public override void Draw(Renderer2D renderer)
    {
        if (!Visible) return;

        // Draw focus border
        if (IsFocused)
        {
            renderer.DrawRect(Position - new Vector2(2, 2), Size + new Vector2(4, 4), FocusBorderColor);
        }

        var (trackX, trackWidth, handleX) = GetTrackGeometry();
        var centerY = Position.Y + Size.Y / 2;

        // Draw label
        if (!string.IsNullOrEmpty(Label))
        {
            renderer.DrawText(Label, new Vector2(Position.X, centerY - 6), TextColor);
        }

        // Draw track background
        var trackY = centerY - TrackHeight / 2;
        renderer.DrawFilledRect(
            new Vector2(trackX, trackY),
            new Vector2(trackWidth, TrackHeight),
            TrackColor);

        // Draw filled portion
        var fillWidth = NormalizedValue * trackWidth;
        if (fillWidth > 0)
        {
            renderer.DrawFilledRect(
                new Vector2(trackX, trackY),
                new Vector2(fillWidth, TrackHeight),
                FillColor);
        }

        // Draw handle
        var handleColor = _isDragging ? HandleActiveColor :
                          _isHovered ? HandleHoverColor :
                          HandleColor;

        renderer.DrawFilledCircle(new Vector2(handleX, centerY), HandleRadius, handleColor);

        // Draw value text
        if (ShowValue)
        {
            var valueText = Value.ToString(ValueFormat, System.Globalization.CultureInfo.InvariantCulture);
            var valueX = trackX + trackWidth + HandleRadius + 4;
            renderer.DrawText(valueText, new Vector2(valueX, centerY - 6), TextColor);
        }
    }
}
