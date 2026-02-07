using System;
using System.Numerics;

namespace Radiant
{
    /// <summary>
    /// Tailwind CSS color palette as Vector3 (RGB) values.
    /// Colors are normalized to 0-1 range. Use .WithAlpha() to get a Vector4 with alpha.
    /// </summary>
    public static class Colors
    {
        // Special colors
        public static readonly Vector3 Black = new(0f, 0f, 0f);
        public static readonly Vector3 White = new(1f, 1f, 1f);

        // Slate
        public static readonly Vector3 Slate50 = FromHex("#f8fafc");
        public static readonly Vector3 Slate100 = FromHex("#f1f5f9");
        public static readonly Vector3 Slate200 = FromHex("#e2e8f0");
        public static readonly Vector3 Slate300 = FromHex("#cbd5e1");
        public static readonly Vector3 Slate400 = FromHex("#94a3b8");
        public static readonly Vector3 Slate500 = FromHex("#64748b");
        public static readonly Vector3 Slate600 = FromHex("#475569");
        public static readonly Vector3 Slate700 = FromHex("#334155");
        public static readonly Vector3 Slate800 = FromHex("#1e293b");
        public static readonly Vector3 Slate900 = FromHex("#0f172a");
        public static readonly Vector3 Slate950 = FromHex("#020617");

        // Gray
        public static readonly Vector3 Gray50 = FromHex("#f9fafb");
        public static readonly Vector3 Gray100 = FromHex("#f3f4f6");
        public static readonly Vector3 Gray200 = FromHex("#e5e7eb");
        public static readonly Vector3 Gray300 = FromHex("#d1d5db");
        public static readonly Vector3 Gray400 = FromHex("#9ca3af");
        public static readonly Vector3 Gray500 = FromHex("#6b7280");
        public static readonly Vector3 Gray600 = FromHex("#4b5563");
        public static readonly Vector3 Gray700 = FromHex("#374151");
        public static readonly Vector3 Gray800 = FromHex("#1f2937");
        public static readonly Vector3 Gray900 = FromHex("#111827");
        public static readonly Vector3 Gray950 = FromHex("#030712");

        // Zinc
        public static readonly Vector3 Zinc50 = FromHex("#fafafa");
        public static readonly Vector3 Zinc100 = FromHex("#f4f4f5");
        public static readonly Vector3 Zinc200 = FromHex("#e4e4e7");
        public static readonly Vector3 Zinc300 = FromHex("#d4d4d8");
        public static readonly Vector3 Zinc400 = FromHex("#a1a1aa");
        public static readonly Vector3 Zinc500 = FromHex("#71717a");
        public static readonly Vector3 Zinc600 = FromHex("#52525b");
        public static readonly Vector3 Zinc700 = FromHex("#3f3f46");
        public static readonly Vector3 Zinc800 = FromHex("#27272a");
        public static readonly Vector3 Zinc900 = FromHex("#18181b");
        public static readonly Vector3 Zinc950 = FromHex("#09090b");

        // Neutral
        public static readonly Vector3 Neutral50 = FromHex("#fafafa");
        public static readonly Vector3 Neutral100 = FromHex("#f5f5f5");
        public static readonly Vector3 Neutral200 = FromHex("#e5e5e5");
        public static readonly Vector3 Neutral300 = FromHex("#d4d4d4");
        public static readonly Vector3 Neutral400 = FromHex("#a3a3a3");
        public static readonly Vector3 Neutral500 = FromHex("#737373");
        public static readonly Vector3 Neutral600 = FromHex("#525252");
        public static readonly Vector3 Neutral700 = FromHex("#404040");
        public static readonly Vector3 Neutral800 = FromHex("#262626");
        public static readonly Vector3 Neutral900 = FromHex("#171717");
        public static readonly Vector3 Neutral950 = FromHex("#0a0a0a");

        // Stone
        public static readonly Vector3 Stone50 = FromHex("#fafaf9");
        public static readonly Vector3 Stone100 = FromHex("#f5f5f4");
        public static readonly Vector3 Stone200 = FromHex("#e7e5e4");
        public static readonly Vector3 Stone300 = FromHex("#d6d3d1");
        public static readonly Vector3 Stone400 = FromHex("#a8a29e");
        public static readonly Vector3 Stone500 = FromHex("#78716c");
        public static readonly Vector3 Stone600 = FromHex("#57534e");
        public static readonly Vector3 Stone700 = FromHex("#44403c");
        public static readonly Vector3 Stone800 = FromHex("#292524");
        public static readonly Vector3 Stone900 = FromHex("#1c1917");
        public static readonly Vector3 Stone950 = FromHex("#0c0a09");

        // Red
        public static readonly Vector3 Red50 = FromHex("#fef2f2");
        public static readonly Vector3 Red100 = FromHex("#fee2e2");
        public static readonly Vector3 Red200 = FromHex("#fecaca");
        public static readonly Vector3 Red300 = FromHex("#fca5a5");
        public static readonly Vector3 Red400 = FromHex("#f87171");
        public static readonly Vector3 Red500 = FromHex("#ef4444");
        public static readonly Vector3 Red600 = FromHex("#dc2626");
        public static readonly Vector3 Red700 = FromHex("#b91c1c");
        public static readonly Vector3 Red800 = FromHex("#991b1b");
        public static readonly Vector3 Red900 = FromHex("#7f1d1d");
        public static readonly Vector3 Red950 = FromHex("#450a0a");

        // Orange
        public static readonly Vector3 Orange50 = FromHex("#fff7ed");
        public static readonly Vector3 Orange100 = FromHex("#ffedd5");
        public static readonly Vector3 Orange200 = FromHex("#fed7aa");
        public static readonly Vector3 Orange300 = FromHex("#fdba74");
        public static readonly Vector3 Orange400 = FromHex("#fb923c");
        public static readonly Vector3 Orange500 = FromHex("#f97316");
        public static readonly Vector3 Orange600 = FromHex("#ea580c");
        public static readonly Vector3 Orange700 = FromHex("#c2410c");
        public static readonly Vector3 Orange800 = FromHex("#9a3412");
        public static readonly Vector3 Orange900 = FromHex("#7c2d12");
        public static readonly Vector3 Orange950 = FromHex("#431407");

        // Amber
        public static readonly Vector3 Amber50 = FromHex("#fffbeb");
        public static readonly Vector3 Amber100 = FromHex("#fef3c7");
        public static readonly Vector3 Amber200 = FromHex("#fde68a");
        public static readonly Vector3 Amber300 = FromHex("#fcd34d");
        public static readonly Vector3 Amber400 = FromHex("#fbbf24");
        public static readonly Vector3 Amber500 = FromHex("#f59e0b");
        public static readonly Vector3 Amber600 = FromHex("#d97706");
        public static readonly Vector3 Amber700 = FromHex("#b45309");
        public static readonly Vector3 Amber800 = FromHex("#92400e");
        public static readonly Vector3 Amber900 = FromHex("#78350f");
        public static readonly Vector3 Amber950 = FromHex("#451a03");

        // Yellow
        public static readonly Vector3 Yellow50 = FromHex("#fefce8");
        public static readonly Vector3 Yellow100 = FromHex("#fef9c3");
        public static readonly Vector3 Yellow200 = FromHex("#fef08a");
        public static readonly Vector3 Yellow300 = FromHex("#fde047");
        public static readonly Vector3 Yellow400 = FromHex("#facc15");
        public static readonly Vector3 Yellow500 = FromHex("#eab308");
        public static readonly Vector3 Yellow600 = FromHex("#ca8a04");
        public static readonly Vector3 Yellow700 = FromHex("#a16207");
        public static readonly Vector3 Yellow800 = FromHex("#854d0e");
        public static readonly Vector3 Yellow900 = FromHex("#713f12");
        public static readonly Vector3 Yellow950 = FromHex("#422006");

        // Lime
        public static readonly Vector3 Lime50 = FromHex("#f7fee7");
        public static readonly Vector3 Lime100 = FromHex("#ecfccb");
        public static readonly Vector3 Lime200 = FromHex("#d9f99d");
        public static readonly Vector3 Lime300 = FromHex("#bef264");
        public static readonly Vector3 Lime400 = FromHex("#a3e635");
        public static readonly Vector3 Lime500 = FromHex("#84cc16");
        public static readonly Vector3 Lime600 = FromHex("#65a30d");
        public static readonly Vector3 Lime700 = FromHex("#4d7c0f");
        public static readonly Vector3 Lime800 = FromHex("#3f6212");
        public static readonly Vector3 Lime900 = FromHex("#365314");
        public static readonly Vector3 Lime950 = FromHex("#1a2e05");

        // Green
        public static readonly Vector3 Green50 = FromHex("#f0fdf4");
        public static readonly Vector3 Green100 = FromHex("#dcfce7");
        public static readonly Vector3 Green200 = FromHex("#bbf7d0");
        public static readonly Vector3 Green300 = FromHex("#86efac");
        public static readonly Vector3 Green400 = FromHex("#4ade80");
        public static readonly Vector3 Green500 = FromHex("#22c55e");
        public static readonly Vector3 Green600 = FromHex("#16a34a");
        public static readonly Vector3 Green700 = FromHex("#15803d");
        public static readonly Vector3 Green800 = FromHex("#166534");
        public static readonly Vector3 Green900 = FromHex("#14532d");
        public static readonly Vector3 Green950 = FromHex("#052e16");

        // Emerald
        public static readonly Vector3 Emerald50 = FromHex("#ecfdf5");
        public static readonly Vector3 Emerald100 = FromHex("#d1fae5");
        public static readonly Vector3 Emerald200 = FromHex("#a7f3d0");
        public static readonly Vector3 Emerald300 = FromHex("#6ee7b7");
        public static readonly Vector3 Emerald400 = FromHex("#34d399");
        public static readonly Vector3 Emerald500 = FromHex("#10b981");
        public static readonly Vector3 Emerald600 = FromHex("#059669");
        public static readonly Vector3 Emerald700 = FromHex("#047857");
        public static readonly Vector3 Emerald800 = FromHex("#065f46");
        public static readonly Vector3 Emerald900 = FromHex("#064e3b");
        public static readonly Vector3 Emerald950 = FromHex("#022c22");

        // Teal
        public static readonly Vector3 Teal50 = FromHex("#f0fdfa");
        public static readonly Vector3 Teal100 = FromHex("#ccfbf1");
        public static readonly Vector3 Teal200 = FromHex("#99f6e4");
        public static readonly Vector3 Teal300 = FromHex("#5eead4");
        public static readonly Vector3 Teal400 = FromHex("#2dd4bf");
        public static readonly Vector3 Teal500 = FromHex("#14b8a6");
        public static readonly Vector3 Teal600 = FromHex("#0d9488");
        public static readonly Vector3 Teal700 = FromHex("#0f766e");
        public static readonly Vector3 Teal800 = FromHex("#115e59");
        public static readonly Vector3 Teal900 = FromHex("#134e4a");
        public static readonly Vector3 Teal950 = FromHex("#042f2e");

        // Cyan
        public static readonly Vector3 Cyan50 = FromHex("#ecfeff");
        public static readonly Vector3 Cyan100 = FromHex("#cffafe");
        public static readonly Vector3 Cyan200 = FromHex("#a5f3fc");
        public static readonly Vector3 Cyan300 = FromHex("#67e8f9");
        public static readonly Vector3 Cyan400 = FromHex("#22d3ee");
        public static readonly Vector3 Cyan500 = FromHex("#06b6d4");
        public static readonly Vector3 Cyan600 = FromHex("#0891b2");
        public static readonly Vector3 Cyan700 = FromHex("#0e7490");
        public static readonly Vector3 Cyan800 = FromHex("#155e75");
        public static readonly Vector3 Cyan900 = FromHex("#164e63");
        public static readonly Vector3 Cyan950 = FromHex("#083344");

        // Sky
        public static readonly Vector3 Sky50 = FromHex("#f0f9ff");
        public static readonly Vector3 Sky100 = FromHex("#e0f2fe");
        public static readonly Vector3 Sky200 = FromHex("#bae6fd");
        public static readonly Vector3 Sky300 = FromHex("#7dd3fc");
        public static readonly Vector3 Sky400 = FromHex("#38bdf8");
        public static readonly Vector3 Sky500 = FromHex("#0ea5e9");
        public static readonly Vector3 Sky600 = FromHex("#0284c7");
        public static readonly Vector3 Sky700 = FromHex("#0369a1");
        public static readonly Vector3 Sky800 = FromHex("#075985");
        public static readonly Vector3 Sky900 = FromHex("#0c4a6e");
        public static readonly Vector3 Sky950 = FromHex("#082f49");

        // Blue
        public static readonly Vector3 Blue50 = FromHex("#eff6ff");
        public static readonly Vector3 Blue100 = FromHex("#dbeafe");
        public static readonly Vector3 Blue200 = FromHex("#bfdbfe");
        public static readonly Vector3 Blue300 = FromHex("#93c5fd");
        public static readonly Vector3 Blue400 = FromHex("#60a5fa");
        public static readonly Vector3 Blue500 = FromHex("#3b82f6");
        public static readonly Vector3 Blue600 = FromHex("#2563eb");
        public static readonly Vector3 Blue700 = FromHex("#1d4ed8");
        public static readonly Vector3 Blue800 = FromHex("#1e40af");
        public static readonly Vector3 Blue900 = FromHex("#1e3a8a");
        public static readonly Vector3 Blue950 = FromHex("#172554");

        // Indigo
        public static readonly Vector3 Indigo50 = FromHex("#eef2ff");
        public static readonly Vector3 Indigo100 = FromHex("#e0e7ff");
        public static readonly Vector3 Indigo200 = FromHex("#c7d2fe");
        public static readonly Vector3 Indigo300 = FromHex("#a5b4fc");
        public static readonly Vector3 Indigo400 = FromHex("#818cf8");
        public static readonly Vector3 Indigo500 = FromHex("#6366f1");
        public static readonly Vector3 Indigo600 = FromHex("#4f46e5");
        public static readonly Vector3 Indigo700 = FromHex("#4338ca");
        public static readonly Vector3 Indigo800 = FromHex("#3730a3");
        public static readonly Vector3 Indigo900 = FromHex("#312e81");
        public static readonly Vector3 Indigo950 = FromHex("#1e1b4b");

        // Violet
        public static readonly Vector3 Violet50 = FromHex("#f5f3ff");
        public static readonly Vector3 Violet100 = FromHex("#ede9fe");
        public static readonly Vector3 Violet200 = FromHex("#ddd6fe");
        public static readonly Vector3 Violet300 = FromHex("#c4b5fd");
        public static readonly Vector3 Violet400 = FromHex("#a78bfa");
        public static readonly Vector3 Violet500 = FromHex("#8b5cf6");
        public static readonly Vector3 Violet600 = FromHex("#7c3aed");
        public static readonly Vector3 Violet700 = FromHex("#6d28d9");
        public static readonly Vector3 Violet800 = FromHex("#5b21b6");
        public static readonly Vector3 Violet900 = FromHex("#4c1d95");
        public static readonly Vector3 Violet950 = FromHex("#2e1065");

        // Purple
        public static readonly Vector3 Purple50 = FromHex("#faf5ff");
        public static readonly Vector3 Purple100 = FromHex("#f3e8ff");
        public static readonly Vector3 Purple200 = FromHex("#e9d5ff");
        public static readonly Vector3 Purple300 = FromHex("#d8b4fe");
        public static readonly Vector3 Purple400 = FromHex("#c084fc");
        public static readonly Vector3 Purple500 = FromHex("#a855f7");
        public static readonly Vector3 Purple600 = FromHex("#9333ea");
        public static readonly Vector3 Purple700 = FromHex("#7e22ce");
        public static readonly Vector3 Purple800 = FromHex("#6b21a8");
        public static readonly Vector3 Purple900 = FromHex("#581c87");
        public static readonly Vector3 Purple950 = FromHex("#3b0764");

        // Fuchsia
        public static readonly Vector3 Fuchsia50 = FromHex("#fdf4ff");
        public static readonly Vector3 Fuchsia100 = FromHex("#fae8ff");
        public static readonly Vector3 Fuchsia200 = FromHex("#f5d0fe");
        public static readonly Vector3 Fuchsia300 = FromHex("#f0abfc");
        public static readonly Vector3 Fuchsia400 = FromHex("#e879f9");
        public static readonly Vector3 Fuchsia500 = FromHex("#d946ef");
        public static readonly Vector3 Fuchsia600 = FromHex("#c026d3");
        public static readonly Vector3 Fuchsia700 = FromHex("#a21caf");
        public static readonly Vector3 Fuchsia800 = FromHex("#86198f");
        public static readonly Vector3 Fuchsia900 = FromHex("#701a75");
        public static readonly Vector3 Fuchsia950 = FromHex("#4a044e");

        // Pink
        public static readonly Vector3 Pink50 = FromHex("#fdf2f8");
        public static readonly Vector3 Pink100 = FromHex("#fce7f3");
        public static readonly Vector3 Pink200 = FromHex("#fbcfe8");
        public static readonly Vector3 Pink300 = FromHex("#f9a8d4");
        public static readonly Vector3 Pink400 = FromHex("#f472b6");
        public static readonly Vector3 Pink500 = FromHex("#ec4899");
        public static readonly Vector3 Pink600 = FromHex("#db2777");
        public static readonly Vector3 Pink700 = FromHex("#be185d");
        public static readonly Vector3 Pink800 = FromHex("#9d174d");
        public static readonly Vector3 Pink900 = FromHex("#831843");
        public static readonly Vector3 Pink950 = FromHex("#500724");

        // Rose
        public static readonly Vector3 Rose50 = FromHex("#fff1f2");
        public static readonly Vector3 Rose100 = FromHex("#ffe4e6");
        public static readonly Vector3 Rose200 = FromHex("#fecdd3");
        public static readonly Vector3 Rose300 = FromHex("#fda4af");
        public static readonly Vector3 Rose400 = FromHex("#fb7185");
        public static readonly Vector3 Rose500 = FromHex("#f43f5e");
        public static readonly Vector3 Rose600 = FromHex("#e11d48");
        public static readonly Vector3 Rose700 = FromHex("#be123c");
        public static readonly Vector3 Rose800 = FromHex("#9f1239");
        public static readonly Vector3 Rose900 = FromHex("#881337");
        public static readonly Vector3 Rose950 = FromHex("#4c0519");

        /// <summary>
        /// Converts a hex color string to a Vector3 with normalized RGB values (0-1).
        /// </summary>
        public static Vector3 FromHex(string hex)
        {
            hex = hex.TrimStart('#');
            var r = int.Parse(hex.AsSpan(0, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture) / 255f;
            var g = int.Parse(hex.AsSpan(2, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture) / 255f;
            var b = int.Parse(hex.AsSpan(4, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture) / 255f;
            return new Vector3(r, g, b);
        }
    }

    public static class ColorExtensions
    {
        public static Vector4 WithAlpha(this Vector3 color, float alpha) =>
            new(color.X, color.Y, color.Z, alpha);
    }
}
