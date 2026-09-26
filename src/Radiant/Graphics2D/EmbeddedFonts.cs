namespace Radiant.Graphics2D;

/// <summary>
/// Names of the MSDF fonts embedded in Radiant, for <see cref="MsdfFont.LoadEmbedded"/>.
/// <para>
/// Inter (UI text) and JetBrains Mono (code, numbers that must line up) are both SIL Open Font
/// License fonts, so they can ship inside any application. The atlases include engineering symbols
/// such as ⌀ and ⊥ baked from Noto fallbacks, so annotation text draws in a single font. They are
/// rebaked with <c>tools/bake-fonts.sh</c>.
/// </para>
/// </summary>
public static class EmbeddedFonts
{
    /// <summary>Inter, weight 400: body text.</summary>
    public const string InterRegular = "inter-regular";

    /// <summary>Inter, weight 500: labels and titles.</summary>
    public const string InterMedium = "inter-medium";

    /// <summary>Inter, weight 600: headings and emphasis.</summary>
    public const string InterSemiBold = "inter-semibold";

    /// <summary>JetBrains Mono, weight 400: code and tabular figures.</summary>
    public const string JetBrainsMonoRegular = "jetbrains-mono-regular";

    /// <summary>The font UI text uses unless told otherwise.</summary>
    public const string Default = InterRegular;

    /// <summary>The fixed-width font.</summary>
    public const string Monospace = JetBrainsMonoRegular;

    /// <summary>GD&amp;T frame symbols (Noto Sans Symbols), the first of the drafting fallback chain.</summary>
    public const string Drafting = "drafting";

    /// <summary>GD&amp;T math-operator symbols (Noto Sans Math), second in the drafting chain.</summary>
    public const string DraftingMath = "drafting-math";

    /// <summary>GD&amp;T geometric-shape symbols (Noto Sans Symbols 2), third in the drafting chain.</summary>
    public const string DraftingShapes = "drafting-shapes";

    // The names callers used before the fonts were named for their faces. "default" was Arial.
    internal static string Resolve(string name) => name switch
    {
        "default" => Default,
        "monospace" => Monospace,
        _ => name,
    };
}
