using System;

namespace Radiant.UI.Core;

/// <summary>
/// Marks an interface as a style facet: a group of related props (corner shape, outline,
/// background colour…) that components choose to expose. A partial record implementing the
/// interface gets its properties generated, so every component spells a facet the same way.
/// Facet properties should be nullable: null means "not set here", which lets forwarding keep a
/// child's own value.
/// <code>
/// [StyleFacet]
/// public partial interface IHasCornerShape
/// {
///     CornerShapeRole? CornerShape { get; init; }
/// }
///
/// public sealed partial record Card : Component, IHasCornerShape { … } // CornerShape is generated
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Interface, Inherited = false)]
public sealed class StyleFacetAttribute : Attribute;
