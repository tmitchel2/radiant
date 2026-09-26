namespace Radiant.UI.Core;

/// <summary>
/// A value passed down the tree without threading it through every component's props: a
/// <see cref="Provider{T}"/> supplies it to everything below, and <see cref="BuildContext.Use{T}"/>
/// reads the nearest one. A component that reads it is rebuilt when the provided value changes.
/// </summary>
/// <typeparam name="T">The value's type.</typeparam>
public sealed class Context<T>
{
    /// <summary>A context whose value is <paramref name="defaultValue"/> where nothing provides one.</summary>
    public Context(T defaultValue) => DefaultValue = defaultValue;

    /// <summary>The value read where no provider is above.</summary>
    public T DefaultValue { get; }

    /// <summary>A provider of this context's value to <paramref name="child"/> and everything below it.</summary>
    public Provider<T> Provide(T value, Element? child) => new(this, value, child);
}
