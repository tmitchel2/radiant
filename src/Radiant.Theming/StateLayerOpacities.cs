namespace Radiant.Theming;

/// <summary>
/// How strongly the content colour is laid over a component to show its state (Material's state
/// layers), and how faded disabled components are.
/// </summary>
public sealed record StateLayerOpacities
{
    /// <summary>Under the pointer.</summary>
    public float Hover { get; init; } = 0.08f;

    /// <summary>With keyboard focus.</summary>
    public float Focus { get; init; } = 0.10f;

    /// <summary>While pressed.</summary>
    public float Pressed { get; init; } = 0.10f;

    /// <summary>While dragged.</summary>
    public float Dragged { get; init; } = 0.16f;

    /// <summary>A disabled component's container.</summary>
    public float DisabledContainer { get; init; } = 0.12f;

    /// <summary>A disabled component's content.</summary>
    public float DisabledContent { get; init; } = 0.38f;
}
