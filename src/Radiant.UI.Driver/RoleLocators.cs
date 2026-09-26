using Radiant.Host.AgentControlProtocol;
using Radiant.UI.Core;

namespace Radiant.UI.Driver;

/// <summary>
/// Elements by their semantics role, a method for each role (generated from <see cref="SemanticsRole"/>):
/// <c>Driver.Role.Button("Save")</c> matches buttons labelled "Save" exactly; <c>Driver.Role.Button()</c>
/// every button; <c>scope.Role.CheckBox(TextMatch.Contains("remember"))</c> within a scope.
/// </summary>
public readonly partial struct RoleLocators
{
    private readonly AppDriver _driver;
    private readonly Selector? _scope;

    internal RoleLocators(AppDriver driver, Selector? scope)
    {
        _driver = driver;
        _scope = scope;
    }

    /// <summary>Elements of <paramref name="role"/>, labelled as <paramref name="label"/> says if given.</summary>
    public Locator Of(SemanticsRole role, TextMatch? label = null) => Make(NameOf(role), label);

    /// <summary>The name a selector gives <paramref name="role"/>: <c>textField</c> for <see cref="SemanticsRole.TextField"/>.</summary>
    public static string NameOf(SemanticsRole role)
    {
        var name = role.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private Locator Make(string role, TextMatch? label) =>
        new(_driver, new Selector { Role = role, Label = label, Within = _scope });
}
