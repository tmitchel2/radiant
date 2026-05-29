namespace Radiant.Layout;

/// <summary>
/// A layout length: a value plus its <see cref="DimensionUnit"/>. The
/// <see cref="DimensionUnit.Undefined"/> default means "leave unset" so a
/// <c>default</c> <see cref="LayoutStyle"/> applies no overrides at all.
/// </summary>
/// <param name="Value">The numeric value; meaningless when <paramref name="Unit"/> is Undefined/Auto.</param>
/// <param name="Unit">How to interpret <paramref name="Value"/>.</param>
public readonly record struct Dimension(float Value, DimensionUnit Unit)
{
    /// <summary>Unset — no override is applied.</summary>
    public static Dimension Undefined => default;

    /// <summary>Sized automatically (Yoga <c>auto</c>).</summary>
    public static Dimension Auto => new(float.NaN, DimensionUnit.Auto);

    /// <summary>An absolute length in points.</summary>
    public static Dimension Points(float value) => new(value, DimensionUnit.Point);

    /// <summary>A percentage (0–100) of the parent's corresponding dimension.</summary>
    public static Dimension Percent(float value) => new(value, DimensionUnit.Percent);

    /// <summary>Whether this dimension carries a value the engine should apply.</summary>
    public bool IsSet => Unit != DimensionUnit.Undefined;

    /// <summary>A bare float is treated as an absolute point length.</summary>
    public static implicit operator Dimension(float points) => Points(points);
}
