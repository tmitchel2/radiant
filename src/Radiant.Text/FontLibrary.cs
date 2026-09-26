using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Radiant.Text;

/// <summary>
/// The font families text can be set in, by name, with a fallback order for characters a family
/// lacks. <see cref="Default"/> holds Radiant's embedded fonts: Inter and JetBrains Mono, both
/// variable (any weight) and both with italics.
/// </summary>
public sealed class FontLibrary : IDisposable
{
    /// <summary>Inter: Radiant's UI typeface.</summary>
    public const string Inter = "Inter";

    /// <summary>JetBrains Mono: Radiant's fixed-width typeface.</summary>
    public const string JetBrainsMono = "JetBrains Mono";

    private static readonly Lazy<FontLibrary> s_default = new(CreateDefault);

    private readonly Dictionary<string, (FontFace? Upright, FontFace? Italic)> _families = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FontFace> _fallbacks = [];
    private readonly object _gate = new();

    /// <summary>A library with Radiant's embedded fonts: Inter and JetBrains Mono.</summary>
    public static FontLibrary Default => s_default.Value;

    /// <summary>The registered family names.</summary>
    public IReadOnlyList<string> Families
    {
        get
        {
            lock (_gate)
            {
                return [.. _families.Keys];
            }
        }
    }

    /// <summary>
    /// Adds a face under its family name (upright or italic, as the face says). A face added
    /// with <paramref name="fallback"/> is also tried, in the order added, for characters the
    /// requested family lacks.
    /// </summary>
    public void Register(FontFace face, bool fallback = true)
    {
        ArgumentNullException.ThrowIfNull(face);
        lock (_gate)
        {
            _families.TryGetValue(face.FamilyName, out var entry);
            _families[face.FamilyName] = face.IsItalic ? (entry.Upright, face) : (face, entry.Italic);
            if (fallback && !face.IsItalic)
            {
                _fallbacks.Add(face);
            }
        }
    }

    /// <summary>
    /// The face for a family: its italic when asked for and it has one, its upright otherwise.
    /// Null if the family isn't registered.
    /// </summary>
    public FontFace? FindFace(string family, bool italic = false)
    {
        lock (_gate)
        {
            if (!_families.TryGetValue(family, out var entry))
            {
                return null;
            }
            return italic ? entry.Italic ?? entry.Upright : entry.Upright ?? entry.Italic;
        }
    }

    /// <summary>
    /// The instance of a family to set text in at a weight and size: <c>wght</c> set to the
    /// weight and, where the font has an optical-size axis, <c>opsz</c> to the size, so small text
    /// gets the design tuned for small sizes. An unknown family falls back to the first registered.
    /// </summary>
    public FontInstance Resolve(string family, float weight, bool italic, float size)
    {
        var face = ResolveFace(family, italic);
        return face.Instance(
            new FontVariation(FontVariation.Weight, weight),
            new FontVariation(FontVariation.OpticalSize, size));
    }

    /// <summary>
    /// The face to draw a code point with: <paramref name="preferred"/> if it has the glyph,
    /// otherwise the first fallback that does, otherwise <paramref name="preferred"/> (which
    /// draws its missing-glyph box).
    /// </summary>
    public FontFace FaceFor(int codePoint, FontFace preferred)
    {
        ArgumentNullException.ThrowIfNull(preferred);
        if (preferred.HasGlyph(codePoint))
        {
            return preferred;
        }
        lock (_gate)
        {
            foreach (var fallback in _fallbacks)
            {
                if (!ReferenceEquals(fallback, preferred) && fallback.HasGlyph(codePoint))
                {
                    return fallback;
                }
            }
        }
        return preferred;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var (upright, italic) in _families.Values)
            {
                upright?.Dispose();
                italic?.Dispose();
            }
            _families.Clear();
            _fallbacks.Clear();
        }
    }

    /// <summary>The face for a family, or the first registered family's if it isn't registered.</summary>
    internal FontFace ResolveFace(string family, bool italic) =>
        FindFace(family, italic) ?? FirstFace()
            ?? throw new InvalidOperationException("The font library has no fonts.");

    private FontFace? FirstFace()
    {
        lock (_gate)
        {
            return _families.Values.Select(f => f.Upright ?? f.Italic).FirstOrDefault(f => f is not null);
        }
    }

    private static FontLibrary CreateDefault()
    {
        var library = new FontLibrary();
        library.Register(Embedded("InterVariable.ttf", Inter, italic: false));
        library.Register(Embedded("InterVariable-Italic.ttf", Inter, italic: true));
        library.Register(Embedded("JetBrainsMonoVariable.ttf", JetBrainsMono, italic: false));
        library.Register(Embedded("JetBrainsMonoVariable-Italic.ttf", JetBrainsMono, italic: true));
        return library;
    }

    private static FontFace Embedded(string file, string family, bool italic)
    {
        using var stream = typeof(FontLibrary).Assembly.GetManifestResourceStream("Radiant.Text.Fonts." + file)
            ?? throw new FileNotFoundException($"Embedded font not found: {file}");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return FontFace.FromBytes(memory.ToArray(), family, italic);
    }
}
