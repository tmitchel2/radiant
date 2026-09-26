using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using HarfBuzzSharp;

namespace Radiant.Text;

/// <summary>
/// A font file, loaded once and shared: its glyphs, its variation axes, and its metrics. Get
/// drawable, shapeable instances of it with <see cref="Instance"/>.
/// </summary>
public sealed class FontFace : IDisposable
{
    private readonly Blob _blob;
    private readonly Face _face;
    private readonly HarfBuzzSharp.Font _probe;
    private readonly Dictionary<string, FontInstance> _instances = [];
    private readonly object _gate = new();

    private FontFace(byte[] data, string familyName, bool italic)
    {
        // HarfBuzz reads the font in place for the face's lifetime, so it gets a native copy that
        // the blob frees when HarfBuzz lets go of it. (Blob.FromStream hands HarfBuzz managed
        // memory that a compacting garbage collection moves, after which every glyph is .notdef.)
        var copy = Marshal.AllocHGlobal(data.Length);
        Marshal.Copy(data, 0, copy, data.Length);
        _blob = new Blob(copy, data.Length, MemoryMode.ReadOnly, () => Marshal.FreeHGlobal(copy));
        _face = new Face(_blob, 0);
        _face.MakeImmutable();
        _probe = new HarfBuzzSharp.Font(_face);
        FamilyName = familyName;
        IsItalic = italic;
        UnitsPerEm = _face.UnitsPerEm;
        Axes = _face.VariationAxisInfos
            .Select(a => new FontAxis(TagName(a.Tag), a.MinValue, a.DefaultValue, a.MaxValue))
            .ToArray();
    }

    /// <summary>The family this face belongs to, as it was registered.</summary>
    public string FamilyName { get; }

    /// <summary>Whether this is the family's italic.</summary>
    public bool IsItalic { get; }

    /// <summary>The font's design units per em.</summary>
    public int UnitsPerEm { get; }

    /// <summary>The variation axes, empty for a static font.</summary>
    public IReadOnlyList<FontAxis> Axes { get; }

    internal Face Face => _face;

    /// <summary>A face from a font file's bytes (TrueType or OpenType).</summary>
    public static FontFace FromBytes(byte[] data, string familyName, bool italic = false)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new FontFace(data, familyName, italic);
    }

    /// <summary>A face from a font file on disk.</summary>
    public static FontFace FromFile(string path, string familyName, bool italic = false) =>
        FromBytes(File.ReadAllBytes(path), familyName, italic);

    // An OpenType tag is four ASCII bytes packed big-endian into an integer.
    private static string TagName(uint tag) => string.Create(4, tag, static (chars, t) =>
    {
        chars[0] = (char)((t >> 24) & 0xFF);
        chars[1] = (char)((t >> 16) & 0xFF);
        chars[2] = (char)((t >> 8) & 0xFF);
        chars[3] = (char)(t & 0xFF);
    });

    /// <summary>The axis with a tag, if the font has it.</summary>
    public FontAxis? FindAxis(string tag)
    {
        foreach (var axis in Axes)
        {
            if (axis.Tag == tag)
            {
                return axis;
            }
        }
        return null;
    }

    /// <summary>Whether the font has a glyph for a Unicode code point.</summary>
    public bool HasGlyph(int codePoint) => _probe.TryGetGlyph((uint)codePoint, out _);

    /// <summary>
    /// The face at particular variation values (for a variable font: weight, optical size, …).
    /// Values are clamped to each axis's range; axes the font lacks are ignored. Instances are
    /// cached, so asking again for the same values returns the same instance.
    /// </summary>
    public FontInstance Instance(params FontVariation[] variations)
    {
        ArgumentNullException.ThrowIfNull(variations);
        var applied = variations
            .Where(v => FindAxis(v.Tag) is not null)
            .Select(v => v with { Value = FindAxis(v.Tag)!.Value.Clamp(v.Value) })
            .OrderBy(v => v.Tag, StringComparer.Ordinal)
            .ToArray();
        var key = string.Join(';', applied.Select(v => $"{v.Tag}={v.Value:R}"));
        lock (_gate)
        {
            if (!_instances.TryGetValue(key, out var instance))
            {
                instance = new FontInstance(this, applied);
                _instances[key] = instance;
            }
            return instance;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var instance in _instances.Values)
            {
                instance.Dispose();
            }
            _instances.Clear();
        }
        _probe.Dispose();
        _face.Dispose();
        _blob.Dispose();
    }

    /// <summary>For example <c>Inter Italic (wght 100–900, opsz 14–32)</c>.</summary>
    public override string ToString() =>
        $"{FamilyName}{(IsItalic ? " Italic" : "")}" +
        (Axes.Count == 0 ? "" : $" ({string.Join(", ", Axes.Select(a => $"{a.Tag} {a.Min:0.##}–{a.Max:0.##}"))})");
}
