namespace Radiant.UI.Core;

/// <summary>
/// An element that describes UI in terms of other elements. Its properties are its inputs (its
/// props); <see cref="Build"/> turns them, and any state it keeps through the hooks on the
/// <see cref="BuildContext"/>, into the element it shows.
/// <para>
/// A component is built again when it's given new props (unequal by value) or when state it
/// uses changes. When its parent rebuilds with props equal to the last, it isn't rebuilt at all.
/// </para>
/// <code>
/// public sealed record Counter(int Start) : Component
/// {
///     public override Element Build(BuildContext context)
///     {
///         var count = context.UseState(Start);
///         return new Box
///         {
///             OnClick = _ => count.Set(count.Value + 1),
///             Children = [new TextBlock($"Clicked {count.Value} times")],
///         };
///     }
/// }
/// </code>
/// </summary>
public abstract record Component : Element
{
    /// <summary>Describes what this component shows, given its props and state.</summary>
    public abstract Element? Build(BuildContext context);
}
