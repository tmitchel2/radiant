using System;
using System.Globalization;
using System.Numerics;
using Radiant.Graphics2D;
using Radiant.Input;
using Silk.NET.Input;

namespace Radiant.UI;

/// <summary>
/// Label + [-] [readout] [+] control for numeric input over an unbounded or
/// wide dynamic range. Linear stepping by default; set <see cref="LogStep"/> to
/// multiply/divide by <see cref="Step"/> for fields like tolerance / limits.
/// </summary>
public class NumberStepper : UIElement
{
    private bool _minusHovered;
    private bool _plusHovered;
    private bool _minusPressed;
    private bool _plusPressed;
    private double _repeatTimer;
    private bool _didRepeat;

    public string Label { get; set; } = "";
    public double Value { get; set; }
    public double Step { get; set; } = 1.0;
    public double? Min { get; set; }
    public double? Max { get; set; }
    public string ValueFormat { get; set; } = "G6";

    /// <summary>When true, +/- multiply/divide by Step rather than add/subtract.</summary>
    public bool LogStep { get; set; }

    public Vector4 TextColor { get; set; } = UIColors.Text;
    public Vector4 ButtonColor { get; set; } = UIColors.BackgroundLight;
    public Vector4 ButtonHoverColor { get; set; } = UIColors.Primary;
    public Vector4 ButtonPressedColor { get; set; } = UIColors.PrimaryActive;
    public Vector4 ReadoutBg { get; set; } = UIColors.BackgroundDark;
    public Vector4 BorderColor { get; set; } = UIColors.Border;

    public float ButtonWidth { get; set; } = 22f;

    public event Action<double>? ValueChanged;

    public override bool IsCapturingInput => _minusPressed || _plusPressed;

    public NumberStepper() { }

    public NumberStepper(string label, double value, double step, Vector2 position, float width)
    {
        Label = label;
        Value = value;
        Step = step;
        Position = position;
        Size = new Vector2(width, 24f);
    }

    public void SetValue(double value)
    {
        if (Min is { } min && value < min) value = min;
        if (Max is { } max && value > max) value = max;
        if (Math.Abs(Value - value) > 1e-15)
        {
            Value = value;
            ValueChanged?.Invoke(Value);
        }
    }

    private (Rectangle minus, Rectangle readout, Rectangle plus) GetGeometry()
    {
        var labelWidth = string.IsNullOrEmpty(Label) ? 0f : MathF.Min(Label.Length * 8 + 8, Size.X * 0.5f);
        var left = Position.X + labelWidth;
        var right = Position.X + Size.X;
        var minus = new Rectangle(left, Position.Y, ButtonWidth, Size.Y);
        var plus = new Rectangle(right - ButtonWidth, Position.Y, ButtonWidth, Size.Y);
        var readoutX = minus.X + ButtonWidth;
        var readoutW = MathF.Max(0f, plus.X - readoutX);
        var readout = new Rectangle(readoutX, Position.Y, readoutW, Size.Y);
        return (minus, readout, plus);
    }

    public override void Update(InputState input, double deltaTime)
    {
        if (!Visible || !Enabled) return;

        var (minus, _, plus) = GetGeometry();
        var mouse = input.MousePosition;
        _minusHovered = minus.Contains(mouse);
        _plusHovered = plus.Contains(mouse);

        var pressed = input.IsMouseButtonPressed(MouseButton.Left);
        var released = input.IsMouseButtonReleased(MouseButton.Left);
        var held = input.IsMouseButtonDown(MouseButton.Left);

        if (_minusHovered && pressed)
        {
            _minusPressed = true;
            _repeatTimer = 0;
            _didRepeat = false;
            Decrement();
        }
        if (_plusHovered && pressed)
        {
            _plusPressed = true;
            _repeatTimer = 0;
            _didRepeat = false;
            Increment();
        }

        if (held && (_minusPressed || _plusPressed))
        {
            _repeatTimer += deltaTime;
            const double initialDelay = 0.4;
            const double repeatInterval = 0.05;
            if (!_didRepeat && _repeatTimer >= initialDelay)
            {
                _didRepeat = true;
                _repeatTimer = initialDelay;
            }
            if (_didRepeat)
            {
                while (_repeatTimer - initialDelay >= repeatInterval)
                {
                    _repeatTimer -= repeatInterval;
                    if (_minusPressed && _minusHovered) Decrement();
                    else if (_plusPressed && _plusHovered) Increment();
                    else break;
                }
            }
        }

        if (released)
        {
            _minusPressed = false;
            _plusPressed = false;
        }
    }

    private void Increment()
    {
        if (LogStep)
        {
            var factor = Step > 0 ? Step : 2.0;
            var v = Value == 0 ? factor : Value * factor;
            SetValue(v);
        }
        else
        {
            SetValue(Value + Step);
        }
    }

    private void Decrement()
    {
        if (LogStep)
        {
            var factor = Step > 0 ? Step : 2.0;
            var v = Value == 0 ? -factor : Value / factor;
            SetValue(v);
        }
        else
        {
            SetValue(Value - Step);
        }
    }

    public override void Draw(Renderer2D renderer)
    {
        if (!Visible) return;

        var (minus, readout, plus) = GetGeometry();
        var centerY = Position.Y + Size.Y / 2;

        if (!string.IsNullOrEmpty(Label))
        {
            renderer.DrawText(Label, new Vector2(Position.X, centerY - 6), TextColor);
        }

        DrawButton(renderer, minus, "-", _minusHovered, _minusPressed);
        DrawButton(renderer, plus, "+", _plusHovered, _plusPressed);

        renderer.DrawFilledRect(new Vector2(readout.X, readout.Y), new Vector2(readout.Width, readout.Height), ReadoutBg);
        renderer.DrawRect(new Vector2(readout.X, readout.Y), new Vector2(readout.Width, readout.Height), BorderColor);
        var text = Value.ToString(ValueFormat, CultureInfo.InvariantCulture);
        var textX = readout.X + 4;
        renderer.DrawText(text, new Vector2(textX, centerY - 6), TextColor);
    }

    private void DrawButton(Renderer2D renderer, Rectangle rect, string glyph, bool hovered, bool pressed)
    {
        var color = pressed ? ButtonPressedColor : hovered ? ButtonHoverColor : ButtonColor;
        renderer.DrawFilledRect(new Vector2(rect.X, rect.Y), new Vector2(rect.Width, rect.Height), color);
        renderer.DrawRect(new Vector2(rect.X, rect.Y), new Vector2(rect.Width, rect.Height), BorderColor);
        var textX = rect.X + (rect.Width - 8) / 2f;
        var textY = rect.Y + (rect.Height - 12) / 2f;
        renderer.DrawText(glyph, new Vector2(textX, textY), TextColor);
    }
}
