using System.Text.Json;

namespace Radiant.Host.AgentControlProtocol.Tests;

[TestClass]
public sealed class SelectorSyntaxTests
{
    [TestMethod]
    public void ReadsTestIdsAndIds()
    {
        Assert.AreEqual(new Selector { TestId = "save" }, Selector.Parse("@save"));
        Assert.AreEqual(new Selector { TestId = "save" }, Selector.Parse("testId=save"));
        Assert.AreEqual(new Selector { Id = 412 }, Selector.Parse("#412"));
        Assert.AreEqual(new Selector { Id = 412 }, Selector.Parse("id=412"));
    }

    [TestMethod]
    public void ReadsRoleAndLabelTogether()
    {
        var selector = Selector.Parse("role=button label=\"Save all\"");

        Assert.AreEqual("button", selector.Role);
        Assert.AreEqual(TextMatch.Exact("Save all"), selector.Label);
    }

    [TestMethod]
    public void ReadsEachKindOfTextMatch()
    {
        Assert.AreEqual(new TextMatch("sav", TextMatchMode.Contains, true), Selector.Parse("text~=sav").Text);
        Assert.AreEqual(new TextMatch("Sav", TextMatchMode.Contains), Selector.Parse("text*=Sav").Text);
        Assert.AreEqual(new TextMatch("save", TextMatchMode.Exact, true), Selector.Parse("text^=save").Text);
        Assert.AreEqual(new TextMatch("^Save (all)?$", TextMatchMode.Regex, true), Selector.Parse("label=/^Save (all)?$/i").Label);
        Assert.AreEqual(new TextMatch("a/b", TextMatchMode.Regex), Selector.Parse("label=/a\\/b/").Label);
    }

    [TestMethod]
    public void ABareOrQuotedWordIsText()
    {
        Assert.AreEqual(new Selector { Text = TextMatch.Exact("Buttons") }, Selector.Parse("Buttons"));
        Assert.AreEqual(new Selector { Text = TextMatch.Exact("Save \"all\"") }, Selector.Parse("\"Save \\\"all\\\"\""));
    }

    [TestMethod]
    public void ReadsFlagsAndIndexes()
    {
        var selector = Selector.Parse("role=checkBox checked=false visible [2]");

        Assert.AreEqual(false, selector.Checked);
        Assert.AreEqual(true, selector.Visible);
        Assert.AreEqual(2, selector.Index);
        Assert.AreEqual(-1, Selector.Parse("role=row[-1]").Index);
    }

    [TestMethod]
    public void WithinNestsOutermostFirst()
    {
        var selector = Selector.Parse("@page >> @orders >> role=button label=Delete");

        Assert.AreEqual("button", selector.Role);
        Assert.AreEqual("orders", selector.Within!.TestId);
        Assert.AreEqual("page", selector.Within.Within!.TestId);
        Assert.IsNull(selector.Within.Within.Within);
    }

    [TestMethod]
    [DataRow("@save")]
    [DataRow("#7")]
    [DataRow("role=button label=\"Save all\" enabled [1]")]
    [DataRow("@orders >> text~=\"delete me\" checked=false")]
    [DataRow("label=/^Sa ve$/i value^=X")]
    [DataRow("testId=\"has space\" text*=b")]
    [DataRow("\"visible\"")]
    [DataRow("@Dialog has(role=button label=\"OK (now)\") [0]")]
    [DataRow("@a has(@b has(text~=c))")]
    public void FormatsWhatItReads(string text)
    {
        var selector = Selector.Parse(text);

        Assert.AreEqual(selector, Selector.Parse(selector.ToString()), selector.ToString());
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(">> @a")]
    [DataRow("@a >>")]
    [DataRow("colour=red")]
    [DataRow("label=\"open")]
    [DataRow("label=/open")]
    [DataRow("#abc")]
    [DataRow("[x]")]
    [DataRow("checked=maybe")]
    [DataRow("role~=button")]
    [DataRow("@a has(text=b")]
    public void RejectsWhatIsNotASelector(string text)
    {
        Assert.ThrowsExactly<FormatException>(() => Selector.Parse(text));
    }

    [TestMethod]
    public void HasNestsASelector()
    {
        var selector = Selector.Parse("@Dialog has(text=Confirm)");

        Assert.AreEqual("Dialog", selector.TestId);
        Assert.AreEqual(new Selector { Text = TextMatch.Exact("Confirm") }, selector.Has);
        Assert.AreEqual(selector, System.Text.Json.JsonSerializer.Deserialize("""{"testId":"Dialog","has":{"text":"Confirm"}}""", AgentJsonContext.Default.Selector));
    }

    [TestMethod]
    public void JsonTakesAStringOrAnObject()
    {
        var fromString = JsonSerializer.Deserialize("\"@save [1]\"", AgentJsonContext.Default.Selector);
        var fromObject = JsonSerializer.Deserialize("""{"testId":"save","index":1,"label":{"contains":"sa","ignoreCase":true},"within":"@dialog"}""", AgentJsonContext.Default.Selector);

        Assert.AreEqual(new Selector { TestId = "save", Index = 1 }, fromString);
        Assert.AreEqual(new Selector { TestId = "save", Index = 1, Label = TextMatch.Contains("sa"), Within = new Selector { TestId = "dialog" } }, fromObject);
    }

    [TestMethod]
    public void JsonRoundTrips()
    {
        var selector = Selector.Parse("@list >> role=button label=/^Del/i text=Delete checked [0]");

        var json = JsonSerializer.Serialize(selector, AgentJsonContext.Compact.Selector);

        Assert.AreEqual(selector, JsonSerializer.Deserialize(json, AgentJsonContext.Compact.Selector));
        StringAssert.Contains(json, "\"text\":\"Delete\"");
    }

    [TestMethod]
    public void MatchesText()
    {
        Assert.IsTrue(TextMatch.Exact("Save").Matches("Save"));
        Assert.IsFalse(TextMatch.Exact("Save").Matches("save"));
        Assert.IsTrue(TextMatch.Contains("AV").Matches("Save"));
        Assert.IsTrue(new TextMatch("^S.v", TextMatchMode.Regex).Matches("Save"));
        Assert.IsFalse(TextMatch.Contains("a").Matches(null));
    }
}
