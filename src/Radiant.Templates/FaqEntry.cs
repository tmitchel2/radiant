namespace Radiant.Templates;

/// <summary>A question and its answer, for <see cref="Faq"/>.</summary>
/// <param name="Question">The question.</param>
/// <param name="Answer">The answer.</param>
public sealed record FaqEntry(string Question, string Answer);
