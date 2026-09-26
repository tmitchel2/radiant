using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Radiant.ColorSystem;
using Radiant.Graphics2D;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;
using Image = Radiant.UI.Core.Image;

namespace Radiant.Components;

/// <summary>
/// Picks a colour in HCT (hue, chroma, tone), the space Radiant's themes are made in: a plane of
/// hue across and chroma up at the current tone, with the colours the screen can't show left
/// clear so the gamut's edge is visible; a strip of tone from black to white; a swatch; and a hex
/// field. Each part moves with the pointer or the keyboard (arrows step 1, Shift 10). Controlled:
/// shows <paramref name="Argb"/> and reports each change as ARGB.
/// </summary>
/// <param name="Argb">The colour, as ARGB.</param>
/// <param name="OnChange">Called with the new colour.</param>
public sealed record ColorPicker(int Argb, Action<int>? OnChange) : Component
{
    // The plane's image: hue in 2° columns, chroma in rows of 2 from MaxChroma down.
    private const int PlaneColumns = 180;
    private const int PlaneRows = 60;
    private const double MaxChroma = 120;
    private const int ToneStep = 2;

    // One plane per tone step, made once: 51 small images at most (images aren't freed yet).
    private static readonly Dictionary<int, ImageSource> s_planes = [];

    // Tone strips by hue and chroma in steps of 3: a few thousand 101 × 1 images at most.
    private const int StripStep = 3;
    private static readonly Dictionary<(int Hue, int Chroma), ImageSource> s_strips = [];

    /// <summary>What assistive technology calls the picker.</summary>
    public string Label { get; init; } = "Colour";

    /// <summary>The plane's width; the strip matches it.</summary>
    public float Width { get; init; } = 288f;

    /// <summary>The plane's height.</summary>
    public float PlaneHeight { get; init; } = 160f;

    /// <summary>The hue × chroma plane at a tone: what's out of the sRGB gamut is left clear.</summary>
    internal static ImageSource Plane(double tone)
    {
        var step = (int)Math.Round(Math.Clamp(tone, 0, 100) / ToneStep);
        lock (s_planes)
        {
            if (s_planes.TryGetValue(step, out var cached))
            {
                return cached;
            }
            var pixels = new byte[PlaneColumns * PlaneRows * 4];
            for (var row = 0; row < PlaneRows; row++)
            {
                var chroma = MaxChroma * (1 - (row + 0.5) / PlaneRows);
                for (var column = 0; column < PlaneColumns; column++)
                {
                    var hue = 360.0 * (column + 0.5) / PlaneColumns;
                    var hct = Hct.From(hue, chroma, step * ToneStep);
                    var argb = hct.ToInt();
                    var i = (row * PlaneColumns + column) * 4;
                    pixels[i] = (byte)argb;
                    pixels[i + 1] = (byte)(argb >> 8);
                    pixels[i + 2] = (byte)(argb >> 16);
                    // The solver gives the nearest colour it can: one well short of the chroma asked for is out of gamut.
                    pixels[i + 3] = hct.Chroma >= chroma - 3 ? (byte)255 : (byte)0;
                }
            }
            return s_planes[step] = ImageSource.FromBgra(PlaneColumns, PlaneRows, pixels);
        }
    }

    /// <summary>Tone 0 to 100 at a hue and chroma, one pixel per tone.</summary>
    internal static ImageSource Strip(double hue, double chroma)
    {
        var key = ((int)Math.Round(hue / StripStep) % (360 / StripStep), (int)Math.Round(Math.Clamp(chroma, 0, MaxChroma) / StripStep));
        lock (s_strips)
        {
            if (s_strips.TryGetValue(key, out var cached))
            {
                return cached;
            }
            var pixels = new byte[101 * 4];
            for (var tone = 0; tone <= 100; tone++)
            {
                var argb = Hct.From(key.Item1 * StripStep, key.Item2 * StripStep, tone).ToInt();
                pixels[tone * 4] = (byte)argb;
                pixels[tone * 4 + 1] = (byte)(argb >> 8);
                pixels[tone * 4 + 2] = (byte)(argb >> 16);
                pixels[tone * 4 + 3] = 255;
            }
            return s_strips[key] = ImageSource.FromBgra(101, 1, pixels);
        }
    }

    /// <summary>A colour as "#RRGGBB".</summary>
    public static string Hex(int argb) => string.Create(CultureInfo.InvariantCulture, $"#{argb & 0xFFFFFF:X6}");

    /// <summary>Reads "#RRGGBB", "RRGGBB" or "#RGB" as an opaque ARGB colour.</summary>
    public static bool TryParseHex(string text, out int argb)
    {
        ArgumentNullException.ThrowIfNull(text);
        var hex = text.Trim().TrimStart('#');
        if (hex.Length == 3)
        {
            hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);
        }
        if (hex.Length == 6 && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            argb = unchecked((int)0xFF000000) | rgb;
            return true;
        }
        argb = 0;
        return false;
    }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        // The picker keeps the hue, chroma and tone asked for: chroma out of gamut, or hue at grey,
        // wouldn't survive a round trip through ARGB.
        var hct = context.UseState(() => { var h = Hct.FromInt(Argb); return (Hue: h.Hue, Chroma: h.Chroma, Tone: h.Tone); });
        var hex = context.UseState(() => TextEditState.From(Hex(Argb)));
        var planeRing = context.UseState(false);
        var stripRing = context.UseState(false);
        // Which part a press began on: moves then follow the pointer anywhere until it's released.
        var dragging = context.UseRef(0);
        var latest = context.UseRef(this);
        latest.Value = this;

        var argb = Argb;
        context.UseEffect(() =>
        {
            var current = hct.Value;
            if (Hct.From(current.Hue, current.Chroma, current.Tone).ToInt() != argb)
            {
                var h = Hct.FromInt(argb);
                hct.Set((h.Hue, h.Chroma, h.Tone));
            }
            hex.Set(TextEditState.From(Hex(argb)));
            return null;
        }, argb);

        void Set(double hue, double chroma, double tone)
        {
            var next = ((hue % 360 + 360) % 360, Math.Clamp(chroma, 0, MaxChroma), Math.Clamp(tone, 0, 100));
            hct.Set(next);
            var color = Hct.From(next.Item1, next.Item2, next.Item3).ToInt();
            hex.Set(TextEditState.From(Hex(color)));
            if (color != latest.Value.Argb)
            {
                latest.Value.OnChange?.Invoke(color);
            }
        }

        var (hue, chroma, tone) = hct.Value;
        var width = Width;
        var planeHeight = PlaneHeight;
        var shown = Hct.From(hue, chroma, tone).ToInt();
        var shownColor = (Vector4)Color.FromArgb(shown);
        float Step(KeyEventArgs e) => (e.Modifiers & KeyModifiers.Shift) != 0 ? 10f : 1f;

        void FromPlane(PointerEventArgs e) =>
            Set(Math.Clamp(e.LocalPosition.X / width, 0, 0.9999) * 360, (1 - Math.Clamp(e.LocalPosition.Y / planeHeight, 0, 1)) * MaxChroma, hct.Value.Tone);

        void FromStrip(PointerEventArgs e) =>
            Set(hct.Value.Hue, hct.Value.Chroma, Math.Clamp(e.LocalPosition.X / width, 0, 1) * 100);

        Element Thumb(float x, float y, bool ring) => new Box
        {
            HitTestVisible = false,
            Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = new Edges(x - 10, y - 10, Dimension.Undefined, Dimension.Undefined), Width = 20, Height = 20 },
            Background = shownColor,
            BorderWidth = ring ? 3f : 2f,
            BorderColor = ring ? theme.Get(SurfaceName.Primary) : new Vector4(1, 1, 1, 1),
            CornerRadii = CornerRadii.All(10),
            Shadows = theme.Elevation(ElevationLevel.Level1),
        };

        return new Box
        {
            Semantics = new Semantics { Role = SemanticsRole.Group, Label = Label },
            Layout = new LayoutStyle { Width = width, RowGap = 12 },
            Children =
            [
                new Box
                {
                    Focusable = true,
                    Semantics = new Semantics
                    {
                        Role = SemanticsRole.Slider,
                        Label = "Hue and chroma",
                        Value = string.Create(CultureInfo.InvariantCulture, $"hue {hue:0}, chroma {chroma:0}"),
                    },
                    Cursor = Radiant.Platform.CursorShape.Crosshair,
                    // Hue and tone run left to right in any language, as the plane and strip are drawn.
                    Layout = new LayoutStyle { Width = width, Height = planeHeight, Direction = Radiant.Text.TextDirection.LeftToRight },
                    Background = theme.Get(SurfaceName.SurfaceContainerHighest),
                    CornerRadii = theme.Corners(CornerShapeRole.Small),
                    OnPointerDown = e =>
                    {
                        dragging.Value = 1;
                        FromPlane(e);
                        e.Handled = true;
                    },
                    OnPointerMove = e =>
                    {
                        if (dragging.Value == 1)
                        {
                            FromPlane(e);
                        }
                    },
                    OnPointerUp = _ => dragging.Value = 0,
                    OnKeyDown = e =>
                    {
                        var (h, c, t) = hct.Value;
                        var s = Step(e);
                        var handled = true;
                        switch (e.Key)
                        {
                            case KeyCode.Left: Set(h - s, c, t); break;
                            case KeyCode.Right: Set(h + s, c, t); break;
                            case KeyCode.Up: Set(h, c + s, t); break;
                            case KeyCode.Down: Set(h, c - s, t); break;
                            default: handled = false; break;
                        }
                        e.Handled |= handled;
                    },
                    OnFocus = e => planeRing.Set(e.IsFocusVisible),
                    OnBlur = _ => planeRing.Set(false),
                    Children =
                    [
                        new Image(Plane(tone))
                        {
                            Fit = ImageFit.Fill,
                            CornerRadii = theme.Corners(CornerShapeRole.Small),
                            Layout = new LayoutStyle { Width = width, Height = planeHeight },
                        },
                        Thumb((float)(hue / 360 * width), (float)((1 - chroma / MaxChroma) * planeHeight), planeRing.Value),
                    ],
                },
                new Box
                {
                    Focusable = true,
                    Semantics = new Semantics { Role = SemanticsRole.Slider, Label = "Tone", Value = tone.ToString("0", CultureInfo.InvariantCulture) },
                    Layout = new LayoutStyle { Width = width, Height = 20, FlexDirection = FlexDirection.Row, Direction = Radiant.Text.TextDirection.LeftToRight },
                    CornerRadii = CornerRadii.All(10),
                    OnPointerDown = e =>
                    {
                        dragging.Value = 2;
                        FromStrip(e);
                        e.Handled = true;
                    },
                    OnPointerMove = e =>
                    {
                        if (dragging.Value == 2)
                        {
                            FromStrip(e);
                        }
                    },
                    OnPointerUp = _ => dragging.Value = 0,
                    OnKeyDown = e =>
                    {
                        var (h, c, t) = hct.Value;
                        var handled = true;
                        switch (e.Key)
                        {
                            case KeyCode.Left or KeyCode.Down: Set(h, c, t - Step(e)); break;
                            case KeyCode.Right or KeyCode.Up: Set(h, c, t + Step(e)); break;
                            case KeyCode.Home: Set(h, c, 0); break;
                            case KeyCode.End: Set(h, c, 100); break;
                            default: handled = false; break;
                        }
                        e.Handled |= handled;
                    },
                    OnFocus = e => stripRing.Set(e.IsFocusVisible),
                    OnBlur = _ => stripRing.Set(false),
                    Children =
                    [
                        new Image(Strip(hue, chroma))
                        {
                            Fit = ImageFit.Fill,
                            CornerRadii = CornerRadii.All(10),
                            Layout = new LayoutStyle { Width = width, Height = 20 },
                        },
                        Thumb((float)(tone / 100 * width), 10, stripRing.Value),
                    ],
                },
                new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 12 },
                    Children =
                    [
                        new Box
                        {
                            Semantics = new Semantics { Role = SemanticsRole.Image, Label = Hex(shown) },
                            Layout = new LayoutStyle { Width = 48, Height = 48, FlexShrink = 0 },
                            Background = shownColor,
                            BorderWidth = 1f,
                            BorderColor = theme.OutlineVariant,
                            CornerRadii = theme.Corners(CornerShapeRole.Medium),
                        },
                        new TextField("Hex")
                        {
                            Value = hex.Value,
                            OnChange = hex.Set,
                            Variant = TextFieldVariant.Outlined,
                            Layout = new LayoutStyle { MinWidth = 0, FlexGrow = 1, FlexShrink = 1 },
                            OnSubmit = () => Commit(),
                            OnFocusChange = focused =>
                            {
                                if (!focused)
                                {
                                    Commit();
                                }
                            },
                        },
                    ],
                },
                new SurfaceText(string.Create(CultureInfo.InvariantCulture, $"H {hue:0}   C {chroma:0}   T {tone:0}"))
                {
                    TextType = TextType.LabelMedium,
                    Legibility = Legibility.Medium,
                },
            ],
        };

        // A valid hex colour replaces the colour; anything else puts the field back.
        void Commit()
        {
            if (TryParseHex(hex.Value.Text, out var parsed))
            {
                var h = Hct.FromInt(parsed);
                hct.Set((h.Hue, h.Chroma, h.Tone));
                hex.Set(TextEditState.From(Hex(parsed)));
                if (parsed != latest.Value.Argb)
                {
                    latest.Value.OnChange?.Invoke(parsed);
                }
            }
            else
            {
                hex.Set(TextEditState.From(Hex(Hct.From(hct.Value.Hue, hct.Value.Chroma, hct.Value.Tone).ToInt())));
            }
        }
    }
}
