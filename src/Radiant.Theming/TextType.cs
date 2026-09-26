namespace Radiant.Theming;

/// <summary>A step on the type scale: Material 3's fifteen styles plus Destash's in-betweens.</summary>
public enum TextType
{
    /// <summary>The largest text: hero numbers and short headings.</summary>
    DisplayLarge,

    /// <summary>Display, medium.</summary>
    DisplayMedium,

    /// <summary>Display, small.</summary>
    DisplaySmall,

    /// <summary>Page headings.</summary>
    HeadlineLarge,

    /// <summary>Headline, medium.</summary>
    HeadlineMedium,

    /// <summary>Headline, small.</summary>
    HeadlineSmall,

    /// <summary>Headline, extra small.</summary>
    HeadlineExtraSmall,

    /// <summary>Section and dialog titles.</summary>
    TitleLarge,

    /// <summary>Title, between large and medium.</summary>
    TitleSemiLarge,

    /// <summary>Title, medium: list headings, app bar titles.</summary>
    TitleMedium,

    /// <summary>Title, small.</summary>
    TitleSmall,

    /// <summary>Button text.</summary>
    LabelLarge,

    /// <summary>Label, medium: chips, tabs.</summary>
    LabelMedium,

    /// <summary>Label, small: badges, captions on controls.</summary>
    LabelSmall,

    /// <summary>Body text, extra large.</summary>
    BodyExtraLarge,

    /// <summary>Body text, large.</summary>
    BodyLarge,

    /// <summary>Body text: the default.</summary>
    BodyMedium,

    /// <summary>Body text, small: supporting text.</summary>
    BodySmall,

    /// <summary>Body text, extra small.</summary>
    BodyExtraSmall,

    /// <summary>Code and figures that must line up, in the monospace family.</summary>
    Code,
}
