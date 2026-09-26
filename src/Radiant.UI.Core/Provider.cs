using System.Collections.Generic;

namespace Radiant.UI.Core;

/// <summary>Supplies a <see cref="Context{T}"/>'s value to its child and everything below it.</summary>
/// <typeparam name="T">The value's type.</typeparam>
/// <param name="Context">The context provided.</param>
/// <param name="Value">The value.</param>
/// <param name="Child">What sees the value.</param>
public sealed record Provider<T>(Context<T> Context, T Value, Element? Child) : Element, IProvider
{
    object IProvider.Context => Context;

    bool IProvider.ProvidesSameValueAs(IProvider other) =>
        other is Provider<T> provider && EqualityComparer<T>.Default.Equals(Value, provider.Value);
}
