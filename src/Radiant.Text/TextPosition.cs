namespace Radiant.Text;

/// <summary>A caret position: a UTF-16 index between characters and the side it belongs to.</summary>
/// <param name="Index">The index, 0 to the text's length.</param>
/// <param name="Affinity">Which character the caret goes with where the index has two places.</param>
public readonly record struct TextPosition(int Index, TextAffinity Affinity = TextAffinity.Downstream);
