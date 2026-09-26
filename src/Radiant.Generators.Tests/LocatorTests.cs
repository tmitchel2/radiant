using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.Generators.Tests;

[TestClass]
public class LocatorTests
{
    private static readonly MetadataReference[] s_driver =
    [
        MetadataReference.CreateFromFile(typeof(Radiant.UI.Driver.AppDriver).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Radiant.Host.AgentControlProtocol.Selector).Assembly.Location),
    ];

    private const string Components = """
        using Radiant.UI.Core;

        namespace Lib;

        public sealed partial record Field : Component
        {
            [TestId] public static partial string Input { get; }
            public override Element? Build(BuildContext context) => null;
        }

        public sealed partial record SignInForm : Component
        {
            [TestId<Field>] public static partial string Email { get; }
            [TestId] public static partial string Submit { get; }
            public override Element? Build(BuildContext context) => null;
        }

        internal sealed partial record Secret : Component
        {
            [TestId] public static partial string Hidden { get; }
            public override Element? Build(BuildContext context) => null;
        }
        """;

    // The components' assembly, as a test project references it.
    private static MetadataReference Library(string source = Components, string name = "Lib")
    {
        var (_, _, errors, output) = GeneratorHarness.Compile(source, name, [new TestIdGenerator()]);
        Assert.AreEqual(0, errors.Length, string.Join("\n", errors.Select(e => e.ToString())));
        return output.ToMetadataReference();
    }

    [TestMethod]
    public void EachCataloguedComponentGetsALocator()
    {
        var (generated, diagnostics, errors) = GeneratorHarness.Run("""
            using Radiant.UI.Driver;

            static class Uses
            {
                static void Use(AppDriver driver) => _ = driver.SignInForm().Email().Input();
            }
            """, new LocatorGenerator(), [Library(), .. s_driver]);

        Assert.AreEqual(0, errors.Length, string.Join("\n", errors.Select(e => e.ToString())));
        Assert.AreEqual(0, diagnostics.Length);
        var form = generated.Single(g => g.Contains("class SignInFormLocator"));
        StringAssert.Contains(form, "public FieldLocator Email() => Part(global::Lib.SignInForm.Email");
        StringAssert.Contains(form, "public global::Radiant.UI.Driver.Locator Submit() => Part(global::Lib.SignInForm.Submit);");
        StringAssert.Contains(generated.Single(g => g.Contains("class RadiantLocators")), "TestId = \"SignInForm\"");
        Assert.IsFalse(generated.Any(g => g.Contains("SecretLocator")), "an internal component the tests can't see has none");
    }

    [TestMethod]
    public void TheProjectsOwnComponentsGetLocatorsToo()
    {
        var (generated, _, errors) = GeneratorHarness.Run("""
            using Radiant.UI.Core;
            using Radiant.UI.Driver;

            namespace Tests;

            public sealed partial record Page : Component
            {
                [TestId] public static partial string Go { get; }
                public override Element? Build(BuildContext context) => null;
            }

            static class Uses
            {
                static void Use(AppDriver driver) => _ = driver.Page().Go();
            }
            """, new LocatorGenerator(), s_driver);

        // The test ID generator isn't in this run, so Go has no body: that's the only error expected.
        Assert.IsTrue(errors.All(e => e.Id == "CS9248"), string.Join("\n", errors.Select(e => e.ToString())));
        Assert.IsTrue(generated.Any(g => g.Contains("class PageLocator")));
    }

    [TestMethod]
    public void ComponentsSharingANameAreTold()
    {
        var other = Library(Components.Replace("namespace Lib;", "namespace Other;"), "Other");

        var (generated, diagnostics, errors) = GeneratorHarness.Run("", new LocatorGenerator(), [Library(), other, .. s_driver]);

        Assert.AreEqual(0, errors.Length, string.Join("\n", errors.Select(e => e.ToString())));
        Assert.IsTrue(diagnostics.Any(d => d.Id == "RAD020"));
        Assert.IsTrue(generated.Any(g => g.Contains("class Other_SignInFormLocator")));
    }

    [TestMethod]
    public void EveryRoleHasItsMethods()
    {
        var methods = typeof(Radiant.UI.Driver.RoleLocators).GetMethods().Select(m => m.Name).ToHashSet();

        foreach (var role in System.Enum.GetNames<Radiant.UI.Core.SemanticsRole>().Where(r => r != "None"))
        {
            Assert.IsTrue(methods.Contains(role), role);
        }
        Assert.AreEqual(3, typeof(Radiant.UI.Driver.RoleLocators).GetMethods().Count(m => m.Name == "Button"), "any, exactly, or matching");
    }

    [TestMethod]
    public void NothingIsMadeWithoutTheDriver()
    {
        var (generated, _, _) = GeneratorHarness.Run("", new LocatorGenerator(), [Library()]);

        Assert.AreEqual(0, generated.Length);
    }
}
