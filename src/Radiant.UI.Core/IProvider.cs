namespace Radiant.UI.Core;

/// <summary>What the runtime needs of any <see cref="Provider{T}"/>, whatever its value type.</summary>
internal interface IProvider
{
    /// <summary>The context provided.</summary>
    object Context { get; }

    /// <summary>The one child.</summary>
    Element? Child { get; }

    /// <summary>Whether another provider supplies an equal value.</summary>
    bool ProvidesSameValueAs(IProvider other);
}
