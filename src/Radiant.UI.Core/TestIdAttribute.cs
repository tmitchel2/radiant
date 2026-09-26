using System;

namespace Radiant.UI.Core;

/// <summary>
/// Declares a part of a component that tests and agents can find: put it on a
/// <c>public static partial string</c> property with only a getter, and the generator supplies its
/// value, <c>"&lt;Type&gt;.&lt;Property&gt;"</c> unless one is given here. Use the property as the part's
/// <see cref="Element.TestId"/> in <c>Build</c>; a test project referencing the generator gets a typed
/// locator for it (<c>Driver.SignInForm().Email()</c>). The component's own root is named too, by the
/// type's name (see <see cref="TestIdsAttribute"/>).
/// <code>
/// [TestId] public static partial string Submit { get; }
/// … new SurfaceButton("Sign in") { TestId = Submit } …
/// </code>
/// </summary>
/// <param name="value">The ID, if it isn't to be made from the type's and the property's names.</param>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
#pragma warning disable CA1813 // Unsealed: the generic form, which names the part's type, derives from it.
public class TestIdAttribute(string? value = null) : Attribute
#pragma warning restore CA1813
{
    /// <summary>The ID, or null to make it from the names.</summary>
    public string? Value { get; } = value;
}

/// <summary>
/// A <see cref="TestIdAttribute"/> that says what the part is: its locator is then that component's own
/// (<c>Driver.SignInForm().Email()</c> is a <c>TextField</c>'s, with its <c>Input()</c>).
/// </summary>
/// <typeparam name="TPart">The element the part is.</typeparam>
/// <param name="value">The ID, if it isn't to be made from the names.</param>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class TestIdAttribute<TPart>(string? value = null) : TestIdAttribute(value) where TPart : Element;

/// <summary>
/// Names a component's root, which is otherwise named by the type (<c>"SignInForm"</c>) once it
/// declares any <see cref="TestIdAttribute">part</see>.
/// </summary>
/// <param name="name">The root's ID.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class TestIdsAttribute(string? name = null) : Attribute
{
    /// <summary>The root's ID, or null for the type's name.</summary>
    public string? Name { get; } = name;
}

/// <summary>
/// The components in an assembly that declare parts: written by the generator, and read by the one that
/// makes locators in test projects.
/// </summary>
/// <param name="components">The components.</param>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class TestIdCatalogAttribute(params Type[] components) : Attribute
{
    /// <summary>The components.</summary>
    public Type[] Components { get; } = components;
}

/// <summary>
/// Marks a control (a button, a field, a switch…) that must be given a <see cref="Element.TestId"/> where it's
/// made, from a declared part (<see cref="TestIdAttribute"/>) or one passed on: the analyzer reports one
/// made without (RAD030) or given a string (RAD031). Inherited by derived types.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class RequiresTestIdAttribute : Attribute;
