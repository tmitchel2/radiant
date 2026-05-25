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
            string codepointSet = "default";
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
                    case "--codepoints" when i + 1 < args.Length: codepointSet = args[++i]; break;
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

            var codepoints = codepointSet switch
            {
                "default" => DefaultCodepoints(),
                "drafting" => DraftingCodepoints(),
                "drafting-math" => DraftingMathCodepoints(),
                "drafting-shapes" => DraftingShapesCodepoints(),
                _ => throw new ArgumentException($"Unknown codepoint set: {codepointSet}. Use 'default', 'drafting', 'drafting-math', or 'drafting-shapes'.")
            };
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
            Console.WriteLine("Usage: MsdfBaker --font <ttf> --out <dir> [--name font] [--codepoints default|drafting] [--size 32] [--atlas 1024] [--range 4]");
        }

        /// <summary>
        /// Codepoint set for the primary "drafting" atlas. Targets glyphs that
        /// live in Misc Technical (U+232x), Geometric Shapes (U+25xx), and
        /// Enclosed Alphanumerics (U+24xx). Source: Noto Sans Symbols.
        /// Math Operators (U+22xx) are NOT in this font; the math-operator
        /// glyphs live in the "drafting-math" atlas via the fallback chain.
        /// </summary>
        private static List<int> DraftingCodepoints()
        {
            return
            [
                // Latin / Arrows — fallback (typically present in any general font).
                0x2014, // — straightness (em-dash)
                0x2197, // ↗ circular runout (NE arrow)

                // Misc Technical block (U+232x–U+233x) — Noto Sans Symbols has these.
                0x232D, // ⌭ cylindricity
                0x2312, // ⌒ profile of a line
                0x2313, // ⌓ profile of a surface
                0x2316, // ⌖ position
                0x232F, // ⌯ symmetry
                0x2330, // ⌰ total runout
                0x2300, // ⌀ diameter

                // Geometric Shapes block (U+25xx).
                0x25B1, // ▱ flatness
                0x25CB, // ○ circularity
                0x25CE, // ◎ concentricity

                // Enclosed Alphanumerics — material-condition modifiers.
                0x24C2, // Ⓜ maximum material
                0x24C1, // Ⓛ least material
                0x24C8, // Ⓢ regardless of feature size
            ];
        }

        /// <summary>
        /// Codepoint set for the "drafting-math" fallback atlas. Carries the
        /// Math Operators block glyphs (U+22xx) used by GD&amp;T frames —
        /// perpendicularity, angularity, parallelism — which Noto Sans Symbols
        /// does NOT cover. Source: Noto Sans Math.
        /// </summary>
        private static List<int> DraftingMathCodepoints()
        {
            return
            [
                0x22A5, // ⊥ perpendicularity
                0x2220, // ∠ angularity
                0x2225, // ∥ parallelism
                // Arrows / Latin tried as further fallback. Noto Sans Math
                // covers some of these; skipped by the bake-time filter if not.
                0x2014, // — straightness (em-dash) — typically present
                0x2197, // ↗ circular runout (NE arrow)
            ];
        }

        /// <summary>
        /// Codepoint set for the "drafting-shapes" fallback atlas. Carries the
        /// Geometric Shapes glyphs (U+25xx) and the position indicator (U+2316)
        /// that neither Noto Sans Symbols nor Noto Sans Math covers.
        /// Source: Noto Sans Symbols 2.
        /// </summary>
        private static List<int> DraftingShapesCodepoints()
        {
            return
            [
                0x2316, // ⌖ position (Misc Technical, position indicator)
                0x25B1, // ▱ flatness (parallelogram outline)
                0x25CB, // ○ circularity (circle outline)
                0x25CE, // ◎ concentricity (bullseye)
                // Try em-dash and NE arrow here too — last resort if neither
                // Noto Sans Symbols nor Noto Sans Math covers them.
                0x2014, // — straightness (em-dash)
                0x2197, // ↗ circular runout (NE arrow)
            ];
        }

        private static List<int> DefaultCodepoints()
        {
            var list = new List<int>();
            // Printable ASCII.
            for (var c = 0x20; c <= 0x7E; c++) list.Add(c);
            // Latin-1 supplement (covers Ø, ±, ×, µ, °, etc.).
            for (var c = 0xA0; c <= 0xFF; c++) list.Add(c);
            // Engineering / drafting symbols used across the Dynamis annotation surfaces.
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
                // GD&T frame symbols. Arial doesn't carry these — the baker
                // logs empty entries. They live in the "drafting" font
                // (Noto Sans Symbols 2); the default font keeps the codepoints
                // for backstop measurement.
                0x25B1, // ▱ flatness
                0x232D, // ⌭ cylindricity
                0x22A5, // ⊥ perpendicularity
                0x2220, // ∠ angularity
                0x2225, // ∥ parallelism
                0x25CE, // ◎ concentricity
                0x232F, // ⌯ symmetry
                0x2197, // ↗ circular runout
                0x2330, // ⌰ total runout
            ];
            list.AddRange(engineering);
            return list;
        }
    }
}
