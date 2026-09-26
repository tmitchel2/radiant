using System;
using System.Collections.Generic;
using System.Linq;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>Frequently asked questions: a heading, and each question opening onto its answer.</summary>
/// <param name="Title">The heading.</param>
/// <param name="Entries">The questions and answers.</param>
public sealed record Faq(string Title, IReadOnlyList<FaqEntry> Entries) : Component
{
    /// <summary>A line under the heading ("Can't find what you need? Contact us.").</summary>
    public string? Subtitle { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new Box
        {
            Layout = new LayoutStyle { RowGap = 16, Padding = Edges.Symmetric(0, 16), MaxWidth = 760, AlignSelf = Align.Stretch },
            Children =
            [
                new SurfaceText(Title) { TextType = TextType.HeadlineMedium, HeadingLevel = 2 },
                Subtitle is null ? null : new SurfaceText(Subtitle) { Legibility = Legibility.Medium },
                new Accordion([.. Entries.Select(e => new AccordionItem(e.Question, new SurfaceText(e.Answer) { Legibility = Legibility.Medium }))]),
            ],
        };
    }
}
