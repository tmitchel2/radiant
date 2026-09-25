using System.Text.Json;
using Radiant.Host.AgentControlProtocol;

namespace Radiant.Host;

/// <summary>
/// Headless smoke test for the host's <c>tab.*</c> control actions: drives a <see cref="TabController"/>
/// (with its real registry-backed scan) directly, without opening a window. Verifies that
/// <c>tab.list</c> discovers the currently attached renderers and — when an instance is named — that
/// <c>tab.activate</c> selects it. This is the no-display path for confirming the control wiring; the
/// unit tests in <c>Radiant.Host.Tests</c> cover the dispatch logic with injected seams.
/// </summary>
internal static class ControlTest
{
    public static int Run(string? instance)
    {
        var controller = new TabController(TabOwnership.PrimaryHostName);

        var list = controller.Handle(new AgentCommand { Id = "t1", Action = "tab.list" });
        Console.WriteLine("tab.list -> " + JsonSerializer.Serialize(list, AgentJsonContext.Default.AgentResponse));
        if (list.Status != "ok")
        {
            Console.Error.WriteLine("CONTROL-TEST FAIL (tab.list)");
            return 1;
        }

        if (instance is not null)
        {
            var p = JsonDocument.Parse($"{{\"name\":{JsonSerializer.Serialize(instance)}}}").RootElement;
            var act = controller.Handle(new AgentCommand { Id = "t2", Action = "tab.activate", Params = p });
            Console.WriteLine("tab.activate -> " + JsonSerializer.Serialize(act, AgentJsonContext.Default.AgentResponse));
            if (act.Status != "ok")
            {
                Console.Error.WriteLine($"CONTROL-TEST FAIL (tab.activate '{instance}' — is it attached?)");
                return 1;
            }
        }

        Console.WriteLine("CONTROL-TEST PASS");
        return 0;
    }
}
