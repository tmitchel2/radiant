namespace Radiant.Generators;

/// <summary>A facet property to generate on a record.</summary>
/// <param name="Name">The property's name.</param>
/// <param name="Type">Its type, fully qualified with nullability.</param>
/// <param name="Facet">The facet interface it comes from, fully qualified (for its documentation).</param>
internal sealed record FacetProperty(string Name, string Type, string Facet);

/// <summary>One property copied by a forwarder.</summary>
/// <param name="Name">The property.</param>
/// <param name="KeepTargetWhenUnset">Whether it's nullable, so the target keeps its own value where the source's is null.</param>
internal sealed record ForwardedProperty(string Name, bool KeepTargetWhenUnset);

/// <summary>A <c>Forward{Name}</c> method to generate.</summary>
/// <param name="Name">The part's name.</param>
/// <param name="Target">The target type, fully qualified.</param>
/// <param name="Properties">What is copied.</param>
internal sealed record Forwarder(string Name, string Target, EquatableArray<ForwardedProperty> Properties);

/// <summary>A type enclosing the record, which the generated partial must also be nested in.</summary>
/// <param name="Keyword">class, struct, record, record struct or interface.</param>
/// <param name="Name">Its name, with type parameters.</param>
internal sealed record ContainingType(string Keyword, string Name);

/// <summary>A diagnostic to report, with its location kept in comparable form.</summary>
internal sealed record DiagnosticInfo(string Id, string FilePath, int Start, int Length, EquatableArray<string> Arguments);

/// <summary>Everything the generator needs about one record.</summary>
internal sealed record RecordModel(
    string FullName,
    string? Namespace,
    EquatableArray<ContainingType> Containers,
    string Keyword,
    string Name,
    EquatableArray<FacetProperty> Properties,
    EquatableArray<Forwarder> Forwarders,
    EquatableArray<DiagnosticInfo> Diagnostics);
