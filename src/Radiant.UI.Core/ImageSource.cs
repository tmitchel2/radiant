using System;
using System.IO;
using System.Runtime.CompilerServices;
using Radiant.Graphics2D;
using SixLabors.ImageSharp.PixelFormats;
using Silk.NET.WebGPU;

namespace Radiant.UI.Core;

/// <summary>
/// A picture for <see cref="Image"/>: decoded once (PNG, JPEG, GIF, BMP, WebP…) into premultiplied
/// pixels, and uploaded to each renderer that draws it the first time it does. Share one source
/// between the elements that show the same picture.
/// </summary>
public sealed class ImageSource
{
    private readonly byte[] _pixels;
    private readonly ConditionalWeakTable<Renderer2D, Texture2D> _textures = [];

    private ImageSource(int width, int height, byte[] premultipliedBgra)
    {
        Width = width;
        Height = height;
        _pixels = premultipliedBgra;
    }

    /// <summary>The picture's width in pixels.</summary>
    public int Width { get; }

    /// <summary>The picture's height in pixels.</summary>
    public int Height { get; }

    /// <summary>Decodes an image file.</summary>
    public static ImageSource FromFile(string path) => FromBytes(File.ReadAllBytes(path));

    /// <summary>Decodes an encoded image (PNG, JPEG, …).</summary>
    public static ImageSource FromBytes(ReadOnlySpan<byte> encoded)
    {
        using var image = SixLabors.ImageSharp.Image.Load<Bgra32>(encoded);
        var pixels = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(pixels);
        return FromBgra(image.Width, image.Height, pixels);
    }

    /// <summary>A picture from straight-alpha, sRGB-encoded BGRA pixels, row by row from the top.</summary>
    public static ImageSource FromBgra(int width, int height, byte[] straightBgra)
    {
        ArgumentNullException.ThrowIfNull(straightBgra);
        ArgumentOutOfRangeException.ThrowIfLessThan(straightBgra.Length, width * height * 4);
        // The renderer blends premultiplied alpha, so premultiply once here.
        var pixels = (byte[])straightBgra.Clone();
        for (var i = 0; i < width * height * 4; i += 4)
        {
            var a = pixels[i + 3];
            if (a != 255)
            {
                pixels[i] = (byte)((pixels[i] * a + 127) / 255);
                pixels[i + 1] = (byte)((pixels[i + 1] * a + 127) / 255);
                pixels[i + 2] = (byte)((pixels[i + 2] * a + 127) / 255);
            }
        }
        return new ImageSource(width, height, pixels);
    }

    /// <summary>
    /// The picture as a texture for <paramref name="renderer"/>, uploaded the first time. sRGB, so
    /// sampling decodes it into the linear light the renderer blends in.
    /// </summary>
    internal Texture2D TextureFor(Renderer2D renderer) => _textures.GetValue(renderer, r =>
    {
        var texture = Texture2D.Create(r, Width, Height, TextureFormat.Bgra8UnormSrgb);
        texture.Update(_pixels);
        return texture;
    });
}
