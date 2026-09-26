using Radiant.Host.AgentControlProtocol;

namespace Radiant.UI.Driver;

/// <summary>
/// A locator for a component that declares parts (<c>[TestId]</c>), and for the parts inside it: what the
/// generator makes for each such component a test project can see (<c>Driver.SignInForm().Email()</c>).
/// It's a <see cref="Locator"/> for the component's root, so it can be tapped, inspected and expected of
/// like any other.
/// <para>
/// A part's test ID names it uniquely (<c>SignInForm.Email</c>), so from <c>Driver.SignInForm()</c> parts are
/// found by ID alone, whether or not the component has a single root to name. Once narrowed to one
/// instance (<see cref="Nth"/>, <see cref="Containing(string)"/>, <see cref="Within"/>, or as a part of
/// another), its parts are found inside it.
/// </para>
/// </summary>
/// <typeparam name="TSelf">The generated locator, which its filters return.</typeparam>
public abstract class ComponentLocator<TSelf> : Locator where TSelf : ComponentLocator<TSelf>
{
    /// <summary>The component <paramref name="selector"/> means; its parts are looked for within <paramref name="partScope"/>, or anywhere if null.</summary>
    protected ComponentLocator(AppDriver driver, Selector selector, Selector? partScope) : base(driver, selector) => PartScope = partScope;

    /// <summary>Where its parts are looked for: null for anywhere (their IDs name them).</summary>
    protected Selector? PartScope { get; }

    /// <summary>The same kind of locator for another selector and part scope.</summary>
    protected abstract TSelf Create(Selector selector, Selector? partScope);

    /// <summary>The part with test ID <paramref name="testId"/>.</summary>
    protected Locator Part(string testId) => new(Driver, new Selector { TestId = testId, Within = PartScope });

    /// <summary>The part with test ID <paramref name="testId"/>, a component itself, as the locator <paramref name="create"/> makes; its parts are found inside it.</summary>
    protected TPart Part<TPart>(string testId, Func<AppDriver, Selector, Selector?, TPart> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        var selector = new Selector { TestId = testId, Within = PartScope };
        return create(Driver, selector, selector);
    }

    /// <summary>The <paramref name="index"/>th of these, from 0; negative counts from the end.</summary>
    public new TSelf Nth(int index) => Narrow(Selector.WithIndex(index));

    /// <summary>The first of these.</summary>
    public new TSelf First() => Nth(0);

    /// <summary>The last of these.</summary>
    public new TSelf Last() => Nth(-1);

    /// <summary>The one with <paramref name="text"/> (a label, a value, some text) somewhere inside it.</summary>
    public new TSelf Containing(string text) => Containing(TextMatch.Exact(text));

    /// <summary>The one with something inside it whose label, value or text passes <paramref name="text"/>.</summary>
    public new TSelf Containing(TextMatch text) => Narrow(base.Containing(text).Selector);

    /// <summary>Only inside <paramref name="scope"/>.</summary>
    public new TSelf Within(Locator scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return Narrow(base.Within(scope).Selector);
    }

    // Narrowed to particular instances, its parts are those inside them.
    private TSelf Narrow(Selector selector) => Create(selector, selector);
}
