using System;
using System.IO;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Radiant.MsdfBaker
{
    public static class AtlasWriter
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public static void WritePng(string path, float[] rgb, int width, int height)
        {
            using var img = new Image<Rgba32>(width, height);
            img.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < width; x++)
                    {
                        var i = (y * width + x) * 3;
                        row[x] = new Rgba32(
                            ToByte(rgb[i + 0]),
                            ToByte(rgb[i + 1]),
                            ToByte(rgb[i + 2]),
                            255);
                    }
                }
            });
            img.SaveAsPng(path);
        }

        public static void WriteJson(string path, AtlasManifest manifest)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(manifest, JsonOptions));
        }

        private static byte ToByte(float v)
        {
            var c = (int)Math.Round(Math.Clamp(v, 0f, 1f) * 255f);
            return (byte)c;
        }
    }
}
