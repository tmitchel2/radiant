using System.Numerics;
using Radiant.Graphics;
using Radiant.Graphics2D;
using Radiant.Layout;
using Radiant.Text;
using Radiant.UI.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Silk.NET.WebGPU;

namespace Radiant.UI.Automation;

/// <summary>
/// Draws the UI to an image, on a headless GPU device of its own, whether or not the app has a window:
/// what a screenshot is. It's a fresh drawing of the tree, so it has no window chrome, native menus or
/// input method windows. Made on first use; on the UI thread only.
/// </summary>
internal sealed unsafe class FrameCapture : IDisposable
{
    private static readonly Vector4[] s_markColors =
    [
        new(0.91f, 0.12f, 0.39f, 1f),
        new(0.13f, 0.59f, 0.95f, 1f),
        new(0.30f, 0.69f, 0.31f, 1f),
        new(1.00f, 0.60f, 0.00f, 1f),
        new(0.61f, 0.15f, 0.69f, 1f),
        new(0.00f, 0.59f, 0.53f, 1f),
    ];

    private HeadlessGpu? _gpu;
    private Renderer2D? _renderer;
    private OffscreenReadback? _target;
    private (int Width, int Height, float Scale) _size;

    /// <summary>Whether a GPU could be had: false where there's none (a screenshot is then unsupported).</summary>
    public bool TryStart()
    {
        if (_gpu is not null)
        {
            return true;
        }
        try
        {
            _gpu = new HeadlessGpu(TextureFormat.Bgra8UnormSrgb);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// The UI at <paramref name="size"/> logical points and <paramref name="pixelScale"/>, with the marks
    /// drawn over it, as RGBA rows.
    /// </summary>
    public (byte[] Rgba, int Width, int Height) Render(UIAppSession session, Vector2 size, float pixelScale, IReadOnlyList<(int Mark, System.Drawing.RectangleF Bounds)> marks)
    {
        var width = Math.Max(1, (int)MathF.Round(size.X * pixelScale));
        var height = Math.Max(1, (int)MathF.Round(size.Y * pixelScale));
        if (_size != (width, height, pixelScale))
        {
            _target?.Dispose();
            _renderer?.Dispose();
            _renderer = new Renderer2D();
            _renderer.Initialize(_gpu!.State, new Camera2D(size.X, size.Y, Handedness.RightHanded), 1);
            _target = new OffscreenReadback(_gpu, width, height, TextureFormat.Bgra8UnormSrgb, 1);
            _size = (width, height, pixelScale);
        }
        using var overlay = marks.Count == 0 ? null : new UIRoot(Overlay(marks), session.Fonts);
        overlay?.Update(size);
        var renderer = _renderer!;
        var bgra = _target!.RenderAndRead(session.Background, pass =>
        {
            renderer.BeginFrame((uint)width, (uint)height, pixelScale);
            session.Root.Paint(renderer);
            overlay?.Paint(renderer);
            renderer.EndFrame((RenderPassEncoder*)pass);
        });
        for (var i = 0; i < bgra.Length; i += 4)
        {
            (bgra[i], bgra[i + 2]) = (bgra[i + 2], bgra[i]);
        }
        return (bgra, width, height);
    }

    /// <summary>Writes RGBA rows to a PNG, cut to <paramref name="crop"/> (pixels) if given.</summary>
    public static (int Width, int Height) SavePng(string path, byte[] rgba, int width, int height, System.Drawing.Rectangle? crop)
    {
        using var image = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(rgba, width, height);
        if (crop is { } area)
        {
            var bounded = System.Drawing.Rectangle.Intersect(area, new System.Drawing.Rectangle(0, 0, width, height));
            if (bounded.Width > 0 && bounded.Height > 0)
            {
                image.Mutate(context => context.Crop(new Rectangle(bounded.X, bounded.Y, bounded.Width, bounded.Height)));
            }
        }
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        image.SaveAsPng(path);
        return (image.Width, image.Height);
    }

    public void Dispose()
    {
        _target?.Dispose();
        _renderer?.Dispose();
        _gpu?.Dispose();
    }

    // Each mark: an outline round the node, and its number in a tab at its top left.
    private static Box Overlay(IReadOnlyList<(int Mark, System.Drawing.RectangleF Bounds)> marks) => new()
    {
        Layout = new LayoutStyle { FlexGrow = 1 },
        HitTestVisible = false,
        Children = [.. marks.SelectMany(mark =>
        {
            var color = s_markColors[(mark.Mark - 1) % s_markColors.Length];
            var bounds = mark.Bounds;
            return new Element?[]
            {
                new Box
                {
                    Layout = At(bounds.X, bounds.Y, bounds.Width, bounds.Height),
                    BorderWidth = 1.5f,
                    BorderColor = color,
                },
                new Box
                {
                    Layout = new LayoutStyle
                    {
                        Position = PositionType.Absolute,
                        Inset = new Edges(bounds.X, Math.Max(0, bounds.Y - 14), Dimension.Undefined, Dimension.Undefined),
                        Padding = new Edges(3, 0, 3, 0),
                        Height = 14,
                    },
                    Background = color,
                    CornerRadii = CornerRadii.All(3),
                    Children = [new TextBlock(mark.Mark.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    {
                        Style = TextStyle.Default with { Size = 10, Weight = FontWeight.Bold, Color = Vector4.One },
                        Wrap = false,
                    }],
                },
            };
        })],
    };

    private static LayoutStyle At(float x, float y, float width, float height) => new()
    {
        Position = PositionType.Absolute,
        Inset = new Edges(x, y, Dimension.Undefined, Dimension.Undefined),
        Width = width,
        Height = height,
    };
}
