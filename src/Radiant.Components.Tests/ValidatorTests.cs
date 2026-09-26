using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.Components.Tests;

[TestClass]
public class ValidatorTests
{
    [TestMethod]
    public void RequiredFailsOnlyBlankText()
    {
        var required = Validators.Required("Needed");

        Assert.AreEqual("Needed", required("  "));
        Assert.IsNull(required("x"));
    }

    [TestMethod]
    public void OtherChecksLetAnOptionalFieldBeBlank()
    {
        Assert.IsNull(Validators.Email()(""));
        Assert.IsNull(Validators.MinLength(8)(""));
        Assert.IsNull(Validators.Number(0, 10)(""));
        Assert.IsNull(Validators.Pattern("[0-9]+", "Digits")(""));
    }

    [TestMethod]
    public void EachCheckSaysWhatsWrong()
    {
        Assert.IsNull(Validators.Email()("ada@example.com"));
        Assert.IsNotNull(Validators.Email()("ada@example"));
        Assert.AreEqual("Use at least 8 characters", Validators.MinLength(8)("short"));
        Assert.AreEqual("Use at most 3 characters", Validators.MaxLength(3)("four"));
        Assert.AreEqual("Digits", Validators.Pattern("[0-9]+", "Digits")("12a"), "the pattern must match all the text");
        Assert.IsNull(Validators.Pattern("[0-9]+", "Digits")("123"));
        Assert.AreEqual("Enter a number", Validators.Number()("many"));
        Assert.IsNotNull(Validators.Number(0, 10)("11"));
        Assert.IsNull(Validators.Number(0, 10)("7"));
    }
}
