using System.Text.Json;

namespace Radiant.Host.AgentControlProtocol.Tests;

[TestClass]
public sealed class WireTests
{
    [TestMethod]
    public void CompactMessagesAreOneLine()
    {
        using var result = JsonDocument.Parse("""
            {
              "nested": { "a": [1, 2] },
              "text": "two\nlines"
            }
            """);
        var response = AgentResponse.Ok("c1", result.RootElement.Clone(), 3);
        response.Type = "res";

        var line = JsonSerializer.Serialize(response, AgentJsonContext.Compact.AgentResponse);

        Assert.IsFalse(line.Contains('\n', StringComparison.Ordinal), line);
        StringAssert.Contains(line, "\"type\":\"res\"");
        var back = JsonSerializer.Deserialize(line, AgentJsonContext.Compact.AgentResponse)!;
        Assert.AreEqual("two\nlines", back.Result!.Value.GetProperty("text").GetString());
    }

    [TestMethod]
    public void TheFileFormatOmitsTheNewFields()
    {
        var command = new AgentCommand { Id = "a", Action = "tab.list" };

        var json = JsonSerializer.Serialize(command, AgentJsonContext.Default.AgentCommand);

        Assert.IsFalse(json.Contains("type", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("timeoutMs", StringComparison.Ordinal));
    }

    [TestMethod]
    public void AcceptedCarriesItsMessage()
    {
        var response = AgentResponse.Accepted("a", "later \"quoted\"");

        Assert.AreEqual("accepted", response.Status);
        Assert.AreEqual("later \"quoted\"", response.Result!.Value.GetProperty("message").GetString());
    }

    [TestMethod]
    public void ErrorsCarryDetails()
    {
        using var details = JsonDocument.Parse("""{"busy":["ticker"]}""");
        var response = AgentResponse.Err("a", AgentErrorCodes.Busy, "not idle", details.RootElement.Clone());

        var back = JsonSerializer.Deserialize(JsonSerializer.Serialize(response, AgentJsonContext.Compact.AgentResponse), AgentJsonContext.Compact.AgentResponse)!;

        Assert.AreEqual("ticker", back.Error!.Details!.Value.GetProperty("busy")[0].GetString());
    }

    [TestMethod]
    public void LogLinesRoundTripAndRead()
    {
        var entry = new LogEntry
        {
            Seq = 3,
            T = "2026-09-26T10:00:01.2340000Z",
            Frame = 882,
            Src = LogSources.Human,
            Kind = LogKinds.Input,
            Input = new LogInput { Type = "tap", X = 412, Y = 300 },
            Target = new LogTarget { Id = 41, Role = "button", Label = "Save", TestId = "save" },
        };

        var line = LogFormatter.ToJsonLine(entry);
        var back = LogFormatter.FromJsonLine(line)!;
        var text = LogFormatter.Format(back);

        Assert.IsFalse(line.Contains('\n', StringComparison.Ordinal));
        StringAssert.Contains(text, "human");
        StringAssert.Contains(text, "tap");
        StringAssert.Contains(text, "button \"Save\" @save #41 (412,300)");
        Assert.IsNull(LogFormatter.FromJsonLine("not json"));
    }

    [TestMethod]
    public void ResultLinesSayHowItWent()
    {
        var entry = new LogEntry
        {
            Kind = LogKinds.Result,
            Src = LogSources.Agent,
            Action = new LogAction { Id = "c1", Name = "ui.tap", Selector = "@save" },
            Result = new LogResult { Status = "error", Ms = 26, Frames = 3, Error = new AgentError { Code = "no_match", Message = "nothing" } },
        };

        var text = LogFormatter.Format(entry);

        StringAssert.Contains(text, "ui.tap");
        StringAssert.Contains(text, "→ no_match 26ms 3f  nothing");
    }
}
