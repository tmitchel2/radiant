using System.Collections.Concurrent;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;

namespace Radiant.UI.Automation;

/// <summary>
/// The JSON Schema of each action's params, for <c>actions.list</c>: made from the params records
/// themselves, so it can't drift from what the actions read. A selector's schema is open, as it takes a
/// string in the compact syntax or an object.
/// </summary>
internal static class Schemas
{
    private static readonly ConcurrentDictionary<string, string?> s_schemas = new(StringComparer.Ordinal);

    public static string? For(string action) => s_schemas.GetOrAdd(action, name => TypeFor(name) is { } type
        ? type.Options.GetJsonSchemaAsNode(type.Type, new JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true }).ToJsonString()
        : null);

    private static JsonTypeInfo? TypeFor(string action) => action switch
    {
        "app.idle" => AutomationJsonContext.Default.IdleParams,
        "app.step" => AutomationJsonContext.Default.StepParams,
        "ui.tree" => AutomationJsonContext.Default.TreeParams,
        "ui.inspect" => AutomationJsonContext.Default.InspectParams,
        "ui.query" => AutomationJsonContext.Default.QueryParams,
        "ui.waitFor" => AutomationJsonContext.Default.WaitForParams,
        "ui.screenshot" => AutomationJsonContext.Default.ScreenshotParams,
        "ui.tap" or "ui.press" or "ui.longPress" or "ui.hover" or "ui.focus" or "ui.type" => AutomationJsonContext.Default.ElementActionParams,
        "ui.tapAt" => AutomationJsonContext.Default.TapAtParams,
        "ui.key" => AutomationJsonContext.Default.KeyParams,
        "ui.scroll" => AutomationJsonContext.Default.ScrollParams,
        "ui.scrollTo" => AutomationJsonContext.Default.ScrollToParams,
        "ui.swipe" or "ui.drag" => AutomationJsonContext.Default.DragParams,
        "log.subscribe" => AutomationJsonContext.Default.LogSubscribeParams,
        "log.unsubscribe" => AutomationJsonContext.Default.LogUnsubscribeParams,
        "log.note" or "log.tail" => AutomationJsonContext.Default.LogNoteParams,
        _ => null,
    };
}
