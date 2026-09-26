namespace Radiant.Theming;

/// <summary>How a disabled control looks.</summary>
public enum DisabledLook
{
    /// <summary>Recoloured: a faint container and faint content (<see cref="StateLayerOpacities.DisabledContainer"/> and <see cref="StateLayerOpacities.DisabledContent"/>).</summary>
    Recolor,

    /// <summary>Its usual colours, the whole control faded to <see cref="InteractionStyle.DisabledOpacity"/>.</summary>
    Fade,
}
