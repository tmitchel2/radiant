using System.Text.Json;

namespace Radiant.Host.AgentControlProtocol.Tests;

[TestClass]
public sealed class ActionResultSerializationTests
{
    // Regression: app.exit previously returned a private nested record not registered in
    // AgentJsonContext, so SerializeToElement (how ActionRegistry.Execute embeds a result in the
    // response) threw under AOT source-gen and the response came back as an error. ExitResult is now a
    // registered shared result type — this guards that it serializes the same way the dispatcher does.
    [TestMethod]
    public void ExitResultSerializesViaAgentContext()
    {
        var element = JsonSerializer.SerializeToElement(new ExitResult("requested"), AgentJsonContext.Default.Options);
        Assert.AreEqual("requested", element.GetProperty("status").GetString());
    }

    [TestMethod]
    public void BoolResultSerializesViaAgentContext()
    {
        var element = JsonSerializer.SerializeToElement(new BoolResult(true), AgentJsonContext.Default.Options);
        Assert.IsTrue(element.GetProperty("ok").GetBoolean());
    }
}
