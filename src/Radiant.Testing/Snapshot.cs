using System;
using System.Collections.Generic;
using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Radiant.Testing;

/// <summary>
/// A rendered image: straight-alpha RGBA bytes, row by row from the top left, in the sRGB encoding
/// the screen shows.
/// </summary>
public sealed class Snapshot
{
    /// <summary>An image of <paramref name="width"/> × <paramref name="height"/> pixels from its RGBA bytes.</summary>
    public Snapshot(int width, int height, byte[] rgba)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        if (rgba.Length != width * height * 4)
        {
            throw new ArgumentException($"Expected {width * height * 4} bytes for {width}×{height}, got {rgba.Length}.", nameof(rgba));
        }
        Width = width;
        Height = height;
        Rgba = rgba;
    }

    /// <summary>The width in pixels.</summary>
    public int Width { get; }

    /// <summary>The height in pixels.</summary>
    public int Height { get; }

    /// <summary>The pixels: four bytes each (R, G, B, A).</summary>
    public byte[] Rgba { get; }

    /// <summary>One pixel's (R, G, B, A).</summary>
    public (byte R, byte G, byte B, byte A) PixelAt(int x, int y)
    {
        var i = (y * Width + x) * 4;
        return (Rgba[i], Rgba[i + 1], Rgba[i + 2], Rgba[i + 3]);
    }

    /// <summary>Writes the image as a PNG, making its directory if need be.</summary>
    public void SavePng(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (System.IO.Path.GetDirectoryName(path) is { Length: > 0 } directory)
        {
            System.IO.Directory.CreateDirectory(directory);
        }
        using var image = Image.LoadPixelData<Rgba32>(Rgba, Width, Height);
        image.SaveAsPng(path);
    }

    /// <summary>Reads a PNG.</summary>
    public static Snapshot LoadPng(string path)
    {
        using var image = Image.Load<Rgba32>(path);
        var rgba = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(rgba);
        return new Snapshot(image.Width, image.Height, rgba);
    }

    /// <summary>Images side by side, top-aligned, <paramref name="gap"/> apart, on <paramref name="background"/> (white by default).</summary>
    public static Snapshot Row(IReadOnlyList<Snapshot> parts, int gap = 0, Vector4? background = null) => Join(parts, gap, background, across: true);

    /// <summary>Images one under another, left-aligned, <paramref name="gap"/> apart, on <paramref name="background"/> (white by default).</summary>
    public static Snapshot Column(IReadOnlyList<Snapshot> parts, int gap = 0, Vector4? background = null) => Join(parts, gap, background, across: false);

    private static Snapshot Join(IReadOnlyList<Snapshot> parts, int gap, Vector4? background, bool across)
    {
        ArgumentNullException.ThrowIfNull(parts);
        var (width, height) = (0, 0);
        foreach (var part in parts)
        {
            (width, height) = across
                ? (width + part.Width, Math.Max(height, part.Height))
                : (Math.Max(width, part.Width), height + part.Height);
        }
        var gaps = Math.Max(0, parts.Count - 1) * gap;
        (width, height) = across ? (width + gaps, height) : (width, height + gaps);

        var fill = background ?? Vector4.One;
        var bytes = new byte[width * height * 4];
        var colour = new[] { Byte(fill.X), Byte(fill.Y), Byte(fill.Z), Byte(fill.W) };
        for (var i = 0; i < bytes.Length; i += 4)
        {
            colour.CopyTo(bytes, i);
        }
        var at = 0;
        foreach (var part in parts)
        {
            var (ox, oy) = across ? (at, 0) : (0, at);
            for (var y = 0; y < part.Height; y++)
            {
                Array.Copy(part.Rgba, y * part.Width * 4, bytes, ((oy + y) * width + ox) * 4, part.Width * 4);
            }
            at += (across ? part.Width : part.Height) + gap;
        }
        return new Snapshot(width, height, bytes);

        // Background components are gamma-encoded 0–1 values, as the image stores them.
        static byte Byte(float v) => (byte)Math.Clamp(MathF.Round(v * 255f), 0f, 255f);
    }
}
