using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Radiant.MsdfBaker
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            string? fontPath = null;
            string? outDir = null;
            string outName = "font";
            var pixelSize = 32;
            var atlasSize = 1024;
            var rangePx = 4f;

            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--font" when i + 1 < args.Length: fontPath = args[++i]; break;
                    case "--out" when i + 1 < args.Length: outDir = args[++i]; break;
                    case "--name" when i + 1 < args.Length: outName = args[++i]; break;
                    case "--size" when i + 1 < args.Length: pixelSize = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--atlas" when i + 1 < args.Length: atlasSize = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--range" when i + 1 < args.Length: rangePx = float.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--help" or "-h":
                        PrintUsage();
                        return 0;
                    default:
                        Console.Error.WriteLine($"Unknown argument: {args[i]}");
                        PrintUsage();
                        return 2;
                }
            }

            if (fontPath is null || outDir is null)
            {
                PrintUsage();
                return 2;
            }
            if (!File.Exists(fontPath))
            {
                Console.Error.WriteLine($"Font file not found: {fontPath}");
                return 2;
            }

            var codepoints = DefaultCodepoints();
            var request = new BakeRequest
            {
                FontPath = fontPath,
                OutputDirectory = outDir,
                OutputName = outName,
                GlyphPixelSize = pixelSize,
                AtlasSize = atlasSize,
                DistanceRangePx = rangePx,
                Codepoints = codepoints,
            };

            var manifest = Baker.Bake(request);
            Console.WriteLine($"Baked {manifest.Glyphs.Count} glyphs from {manifest.FontFamily} into {outDir}/{outName}.{{png,json}} at {atlasSize}x{atlasSize}.");

            // Self-test: render a sample string at a few sizes so we can
            // eyeball whether the MSDF actually resolves to sharp glyphs.
            var sample = "Hello Dynamis Ø25.4 ±0.05";
            foreach (var px in (ReadOnlySpan<int>)[16, 32, 64, 128])
            {
                var path = Path.Combine(outDir, $"{outName}-sample-{px}px.png");
                SelfTest.RenderString(outDir, outName, sample, px, path);
                Console.WriteLine($"  self-test: {path}");
            }
            return 0;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: MsdfBaker --font <ttf> --out <dir> [--name font] [--size 32] [--atlas 1024] [--range 4]");
        }

        private static List<int> DefaultCodepoints()
        {
            var list = new List<int>();
            // Printable ASCII.
            for (var c = 0x20; c <= 0x7E; c++) list.Add(c);
            // Latin-1 supplement (covers Ø, ±, ×, µ, °, etc.).
            for (var c = 0xA0; c <= 0xFF; c++) list.Add(c);
            // Engineering / drafting symbols already used by BitmapFont.
            int[] engineering =
            [
                0x2212, // − minus
                0x221A, // √ radical
                0x2713, // ✓ check
                0x25CB, // ○ circle
                0x2300, // ⌀ diameter
                0x2302, // ⌂
                0x2312, // ⌒ arc
                0x2313, // ⌓
                0x2326, // ⌦
                0x2327, // ⌧
                0x2334, // ⌴
                0x2335, // ⌵
                0x2336, // ⌶
                0x2295, // ⊕
                0x2298, // ⊘
                0x22A1, // ⊡
                0x29B5, // ⦵
                0x2032, // ′ prime
                0x2033, // ″ double prime
                0x24C2, // Ⓜ
                0x24C1, // Ⓛ
                0x24C8, // Ⓢ
                0x2316, // ⌖ position
                0x21A7, // ↧ down arrow
                0x25A1, // □ square
                0x2014, // —
                0x2018, 0x2019, 0x201C, 0x201D, // smart quotes
            ];
            list.AddRange(engineering);
            return list;
        }
    }
}
