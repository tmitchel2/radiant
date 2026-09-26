namespace Radiant.Theming;

/// <summary>What colour hover, focus and press are shown in, laid over a control.</summary>
public enum StateLayerLook
{
    /// <summary>The control's content colour: a filled button lightens, a plain one darkens.</summary>
    Content,

    /// <summary>A shade: black on a light theme, white on a dark one, so every control darkens (or, dark, lightens).</summary>
    Shade,
}
