using System;

namespace Radiant.UI.Core;

/// <summary>An element's identity among its siblings: any value with meaningful equality, such as an id.</summary>
/// <param name="Value">The identifying value.</param>
public readonly record struct Key(object Value)
{
    /// <summary>A key from a string.</summary>
    public static implicit operator Key(string value) => new(value);

    /// <summary>A key from a number.</summary>
    public static implicit operator Key(int value) => new(value);

    /// <summary>A key from a number.</summary>
    public static implicit operator Key(long value) => new(value);

    /// <summary>A key from a GUID.</summary>
    public static implicit operator Key(Guid value) => new(value);

    /// <summary>A key from a string (the named form of the implicit conversion).</summary>
    public static Key FromString(string value) => new(value);

    /// <summary>A key from a number (the named form of the implicit conversion).</summary>
    public static Key FromInt32(int value) => new(value);

    /// <summary>A key from a number (the named form of the implicit conversion).</summary>
    public static Key FromInt64(long value) => new(value);

    /// <summary>A key from a GUID (the named form of the implicit conversion).</summary>
    public static Key FromGuid(Guid value) => new(value);

    /// <summary>The value, for debugging.</summary>
    public override string ToString() => Value.ToString() ?? "";
}
