namespace Radiant.UI.Core;

/// <summary>A mutable box a component keeps across builds; changing it doesn't rebuild anything.</summary>
/// <typeparam name="T">The value's type.</typeparam>
public sealed class Ref<T>
{
    internal Ref(T value) => Value = value;

    /// <summary>The value.</summary>
    public T Value { get; set; }
}
