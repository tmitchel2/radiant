using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.Generators.Tests;

[TestClass]
public class TestIdTests
{
    private const string Controls = """
        using Radiant.UI.Core;

        namespace App;

        [RequiresTestId]
        public sealed record Button(string Label) : Component
        {
            public override Element? Build(BuildContext context) => null;
        }

        public record Plain : Component
        {
            public override Element? Build(BuildContext context) => null;
        }

        [RequiresTestId]
        public record Field : Component
        {
            public override Element? Build(BuildContext context) => null;
        }

        public sealed record PasswordField : Field;

        """;

    private static string Normalize(string text) => text.Replace("\r\n", "\n").Trim();

    [TestMethod]
    public void DeclaredPartsGetTheirIdsAndTheRootItsName()
    {
        var (generated, diagnostics, errors) = GeneratorHarness.Run(Controls + """
            public sealed partial record SignInForm : Component
            {
                [TestId] public static partial string Email { get; }
                [TestId<Button>("sign-in")] public static partial string Submit { get; }

                public override Element? Build(BuildContext context) => new Button("Go") { TestId = Submit };
            }
            """, new TestIdGenerator());

        Assert.AreEqual(0, diagnostics.Length, string.Join("\n", diagnostics.Select(d => d.ToString())));
        Assert.AreEqual(0, errors.Length, string.Join("\n", errors.Select(d => d.ToString())));
        var parts = generated.Single(g => g.Contains("partial record SignInForm"));
        StringAssert.Contains(parts, """public static partial string Email => "SignInForm.Email";""");
        StringAssert.Contains(parts, """public static partial string Submit => "sign-in";""");
        StringAssert.Contains(parts, """protected override string? DefaultTestId => "SignInForm";""");
        StringAssert.Contains(generated.Single(g => g.Contains("TestIdCatalog")), "typeof(global::App.SignInForm)");
    }

    [TestMethod]
    public void TheRootCanBeRenamed()
    {
        var (generated, _, errors) = GeneratorHarness.Run(Controls + """
            [TestIds("Login")]
            public sealed partial record SignInForm : Component
            {
                [TestId] public static partial string Email { get; }
                public override Element? Build(BuildContext context) => null;
            }
            """, new TestIdGenerator());

        Assert.AreEqual(0, errors.Length);
        StringAssert.Contains(generated[0], "\"Login.Email\"");
        StringAssert.Contains(generated[0], "DefaultTestId => \"Login\"");
    }

    [TestMethod]
    public void NestedComponentsAreGeneratedInTheirContainers()
    {
        var (generated, _, errors) = GeneratorHarness.Run(Controls + """
            public static partial class Pages
            {
                public sealed partial record Home : Component
                {
                    [TestId] public static partial string Start { get; }
                    public override Element? Build(BuildContext context) => null;
                }
            }
            """, new TestIdGenerator());

        Assert.AreEqual(0, errors.Length, string.Join("\n", errors.Select(d => d.ToString())));
        StringAssert.Contains(generated[0], "partial class Pages");
        StringAssert.Contains(generated[0], "\"Home.Start\"");
    }

    [TestMethod]
    [DataRow("public static string Email { get; set; }", "RAD010")]
    [DataRow("public partial string Email { get; }", "RAD010")]
    [DataRow("public static partial int Email { get; }", "RAD010")]
    public void AMisshapenPartIsReported(string declaration, string id)
    {
        var (_, diagnostics, _) = GeneratorHarness.Run(Controls + $$"""
            public sealed partial record Form : Component
            {
                [TestId] {{declaration}}
                public override Element? Build(BuildContext context) => null;
            }
            """, new TestIdGenerator());

        Assert.AreEqual(id, diagnostics.Single().Id);
    }

    [TestMethod]
    public void ATypeThatIsNotPartialIsReported()
    {
        var (generated, diagnostics, _) = GeneratorHarness.Run(Controls + """
            public sealed record Form : Component
            {
                [TestId] public static partial string Email { get; }
                public override Element? Build(BuildContext context) => null;
            }
            """, new TestIdGenerator());

        CollectionAssert.Contains(diagnostics.Select(d => d.Id).ToList(), "RAD011");
        Assert.IsFalse(generated.Any(g => g.Contains("partial record Form")));
    }

    [TestMethod]
    public void PartsSharingAnIdAreReported()
    {
        var (_, diagnostics, _) = GeneratorHarness.Run(Controls + """
            public sealed partial record Form : Component
            {
                [TestId("x")] public static partial string A { get; }
                [TestId("x")] public static partial string B { get; }
                public override Element? Build(BuildContext context) => null;
            }
            """, new TestIdGenerator());

        Assert.AreEqual("RAD012", diagnostics.Single().Id);
    }

    [TestMethod]
    public void PartsAreDeclaredOnComponents()
    {
        var (_, diagnostics, _) = GeneratorHarness.Run(Controls + """
            public static partial class Ids
            {
                [TestId] public static partial string Email { get; }
            }
            """, new TestIdGenerator());

        Assert.AreEqual("RAD013", diagnostics.Single().Id);
    }

    [TestMethod]
    [DataRow("new Button(\"Go\")", "RAD030")]
    [DataRow("new Button(\"Go\") { TestId = \"go\" }", "RAD031")]
    [DataRow("new Button(\"Go\") { TestId = $\"go{1}\" }", "RAD031")]
    [DataRow("new Button(\"Go\") { TestId = Constant }", "RAD031")]
    [DataRow("new Button(\"Go\") { TestId = null }", "RAD031")]
    [DataRow("new PasswordField()", "RAD030")]
    [DataRow("new Button(\"Go\") with { Label = \"Stop\" }", "RAD030")]
    public void ControlsWithoutADeclaredIdAreReported(string creation, string id)
    {
        var diagnostics = GeneratorHarness.Analyze(Controls + $$"""
            public sealed partial record Form(string Given) : Component
            {
                private const string Constant = "go";
                [TestId] public static partial string Go { get; }
                public override Element? Build(BuildContext context) => {{creation}};
            }
            """, new TestIdAnalyzer());

        Assert.AreEqual(id, diagnostics.Single().Id, string.Join("\n", diagnostics.Select(d => d.ToString())));
    }

    [TestMethod]
    [DataRow("new Button(\"Go\") { TestId = Go }")]
    [DataRow("new Button(\"Go\") { TestId = Other.Next }")]
    [DataRow("new Button(\"Go\") { TestId = TestId }")]
    [DataRow("new Button(\"Go\") { TestId = Given }")]
    [DataRow("new Button(\"Go\") { TestId = context is null ? Go : Other.Next }")]
    [DataRow("new Button(\"Go\") { TestId = TestId ?? Go }")]
    [DataRow("new Button(\"Go\") with { TestId = Go }")]
    [DataRow("new Plain()")]
    public void ControlsWithADeclaredOrPassedOnIdAreNot(string creation)
    {
        var diagnostics = GeneratorHarness.Analyze(Controls + $$"""
            public sealed partial record Other : Component
            {
                [TestId] public static partial string Next { get; }
                public override Element? Build(BuildContext context) => null;
            }

            public sealed partial record Form(string Given) : Component
            {
                [TestId] public static partial string Go { get; }
                public override Element? Build(BuildContext context) => {{creation}};
            }
            """, new TestIdAnalyzer());

        Assert.AreEqual(0, diagnostics.Length, string.Join("\n", diagnostics.Select(d => d.ToString())));
    }
}
