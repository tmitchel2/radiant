using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Radiant.Text;

/// <summary>
/// The outline icon drawn for each of Radiant's icon names: a character in
/// <see cref="FontLibrary.OutlineIconFont"/>, or null where the outline set has no match (draw the
/// name in <see cref="FontLibrary.Icons"/> then).
/// </summary>
public static class OutlineIcons
{
    private static readonly Lazy<Dictionary<string, string>> s_glyphs = new(Load);

    /// <summary>The character to set in <see cref="FontLibrary.OutlineIconFont"/> for the icon called <paramref name="name"/>, or null.</summary>
    public static string? Glyph(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return s_glyphs.Value.TryGetValue(name, out var glyph) ? glyph : null;
    }

    private static Dictionary<string, string> Load()
    {
        using var stream = typeof(OutlineIcons).Assembly.GetManifestResourceStream("Radiant.Text.Fonts.OutlineIcons.map")
            ?? throw new FileNotFoundException("Embedded outline icon map not found.");
        using var reader = new StreamReader(stream);
        var glyphs = new Dictionary<string, string>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }
            var space = line.IndexOf(' ', StringComparison.Ordinal);
            var codePoint = int.Parse(line.AsSpan(space + 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            glyphs[line[..space]] = char.ConvertFromUtf32(codePoint);
        }
        return glyphs;
    }
}
