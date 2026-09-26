using System;
using System.Collections.Generic;

namespace Radiant.UI.Core;

/// <summary>
/// Generates a method that passes some of a component's style facets down to one of its parts,
/// as a Destash component chooses which of its props feed each sub-component. For
/// <c>[ForwardFacets(typeof(Surface), "Container", typeof(IHasCornerShape))]</c> the record gets
/// <c>Surface ForwardContainer(Surface target)</c>, returning the target with the
/// component's corner shape where the component sets one. Anything set with <c>with</c>
/// afterwards overrides what was forwarded.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class ForwardFacetsAttribute : Attribute
{
    /// <summary>Forwards <paramref name="facets"/> to <paramref name="target"/> through <c>Forward{name}</c>.</summary>
    /// <param name="target">The part's element type, which must implement every facet forwarded.</param>
    /// <param name="name">The part's name: the method is <c>Forward</c> followed by it.</param>
    /// <param name="facets">The facet interfaces to pass on.</param>
    public ForwardFacetsAttribute(Type target, string name, params Type[] facets)
    {
        Target = target;
        Name = name;
        Facets = facets;
    }

    /// <summary>The part's element type.</summary>
    public Type Target { get; }

    /// <summary>The part's name.</summary>
    public string Name { get; }

    /// <summary>The facets forwarded.</summary>
    public IReadOnlyList<Type> Facets { get; }
}
