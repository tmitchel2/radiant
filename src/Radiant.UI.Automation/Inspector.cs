using System.Numerics;
using System.Text.Json;
using Radiant.Host.AgentControlProtocol;
using Radiant.UI.Core;

namespace Radiant.UI.Automation;

/// <summary>A field <c>ui.inspect</c> and <c>ui.tree</c> can write for a node.</summary>
[Flags]
internal enum NodeField : long
{
    None = 0,
    Id = 1L << 0,
    Role = 1L << 1,
    Label = 1L << 2,
    TestId = 1L << 3,
    Kind = 1L << 4,
    Bounds = 1L << 5,
    Size = 1L << 6,
    Position = 1L << 7,
    Visible = 1L << 8,
    VisibleRatio = 1L << 9,
    Opacity = 1L << 10,
    OnScreen = 1L << 11,
    Tap = 1L << 12,
    Hittable = 1L << 13,
    ObscuredBy = 1L << 14,
    Scroll = 1L << 15,
    Value = 1L << 16,
    Description = 1L << 17,
    HeadingLevel = 1L << 18,
    Disabled = 1L << 19,
    Checked = 1L << 20,
    Selected = 1L << 21,
    Expanded = 1L << 22,
    Focusable = 1L << 23,
    Focused = 1L << 24,
    Text = 1L << 25,
    Selection = 1L << 26,
    Placeholder = 1L << 27,
    Type = 1L << 28,
    Clips = 1L << 29,
    HitTestVisible = 1L << 30,
    Transformed = 1L << 31,
    ScreenBounds = 1L << 32,
    PixelBounds = 1L << 33,
}

/// <summary>Which fields to write: what was asked for, and which of those were named rather than grouped.</summary>
internal readonly record struct FieldSet(NodeField Fields, NodeField Named)
{
    public const string Default = "basic,geometry,state";

    private static readonly Dictionary<string, NodeField> s_groups = new(StringComparer.OrdinalIgnoreCase)
    {
        ["basic"] = NodeField.Id | NodeField.Role | NodeField.Label | NodeField.TestId | NodeField.Kind,
        ["geometry"] = NodeField.Bounds | NodeField.Size | NodeField.Position,
        ["visibility"] = NodeField.Visible | NodeField.VisibleRatio | NodeField.Opacity | NodeField.OnScreen,
        ["hit"] = NodeField.Tap | NodeField.Hittable | NodeField.ObscuredBy,
        ["scroll"] = NodeField.Scroll,
        ["semantics"] = NodeField.Value | NodeField.Description | NodeField.HeadingLevel,
        ["state"] = NodeField.Disabled | NodeField.Checked | NodeField.Selected | NodeField.Expanded | NodeField.Focusable | NodeField.Focused,
        ["text"] = NodeField.Text | NodeField.Selection | NodeField.Placeholder,
        ["render"] = NodeField.Type | NodeField.Clips | NodeField.HitTestVisible | NodeField.Transformed,
        ["screen"] = NodeField.ScreenBounds | NodeField.PixelBounds,
        ["all"] = (NodeField)((1L << 34) - 1),
    };

    public bool Has(NodeField field) => (Fields & field) != 0;

    // Named fields are written even when false or empty; grouped ones only when they say something.
    public bool Always(NodeField field) => (Named & field) != 0;

    /// <summary>Reads <c>"basic,hit,-label,scroll"</c>; an <c>invalid_params</c> error names what it doesn't know.</summary>
    public static FieldSet Parse(string? spec)
    {
        var fields = NodeField.None;
        var named = NodeField.None;
        foreach (var raw in (string.IsNullOrWhiteSpace(spec) ? Default : spec).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var remove = raw.StartsWith('-');
            var name = remove ? raw[1..] : raw;
            NodeField value;
            var isGroup = s_groups.TryGetValue(name, out value);
            if (!isGroup && !Enum.TryParse(name, ignoreCase: true, out value))
            {
                throw new AgentException(AgentErrorCodes.InvalidParams,
                    $"Unknown field '{name}'. Groups: {string.Join(", ", s_groups.Keys)}; or a field such as tap, bounds, scroll, text.");
            }
            if (remove)
            {
                fields &= ~value;
                named &= ~value;
            }
            else
            {
                fields |= value;
                if (!isGroup)
                {
                    named |= value;
                }
            }
        }
        return new FieldSet(fields, named);
    }
}

/// <summary>
/// Writes nodes as JSON for <c>ui.tree</c> and <c>ui.inspect</c>: a document
/// <c>{frame, tree, coords, pixelScale, window, nodes:[…]}</c>, each node with the fields asked for and
/// its <c>children</c> to the depth asked for (<c>childCount</c> where it stops).
/// </summary>
internal sealed class Inspector(UIAppSession session, SelectorEngine engine, FieldSet fields, bool renderTree, bool visibleOnly)
{
    private UIRoot Root => session.Root;

    public JsonElement Document(IEnumerable<Match> roots, int depth) => Json.Write(writer =>
    {
        writer.WriteStartObject();
        writer.WriteNumber("frame", session.Frame);
        writer.WriteString("tree", renderTree ? "render" : "semantics");
        writer.WriteString("coords", "logical");
        writer.WriteNumber("pixelScale", session.PixelScale);
        writer.WritePropertyName("window");
        WriteRect(writer, new System.Drawing.RectangleF(session.WindowPosition.X, session.WindowPosition.Y, session.Size.X, session.Size.Y));
        writer.WriteStartArray("nodes");
        foreach (var root in roots)
        {
            WriteNode(writer, root, depth);
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    private void WriteNode(Utf8JsonWriter writer, Match match, int depth)
    {
        var node = match.Node;
        var semantics = match.Semantics;
        writer.WriteStartObject();
        if (fields.Has(NodeField.Id))
        {
            writer.WriteNumber("id", node.Id);
        }
        String(NodeField.Role, "role", match.Role);
        String(NodeField.Label, "label", match.Label);
        String(NodeField.TestId, "testId", match.TestId);
        if (fields.Has(NodeField.Kind))
        {
            writer.WriteString("kind", node.Kind.ToString().ToLowerInvariant());
        }

        var bounds = node.Bounds;
        if (fields.Has(NodeField.Bounds))
        {
            writer.WritePropertyName("bounds");
            WriteRect(writer, bounds);
        }
        if (fields.Has(NodeField.Size))
        {
            writer.WritePropertyName("size");
            writer.WriteStartObject();
            writer.WriteNumber("w", Geometry.Round(bounds.Width));
            writer.WriteNumber("h", Geometry.Round(bounds.Height));
            writer.WriteEndObject();
        }
        if (fields.Has(NodeField.Position) && node.Parent is { } parent)
        {
            var local = parent.ToLocal(new Vector2(bounds.X, bounds.Y));
            writer.WritePropertyName("position");
            WritePoint(writer, local);
        }

        var visible = fields.Has(NodeField.Visible | NodeField.VisibleRatio | NodeField.OnScreen | NodeField.Tap | NodeField.Hittable | NodeField.ObscuredBy)
            ? node.VisibleBounds
            : null;
        if (fields.Has(NodeField.Visible))
        {
            writer.WritePropertyName("visible");
            if (visible is { } rect)
            {
                WriteRect(writer, rect);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
        if (fields.Has(NodeField.VisibleRatio))
        {
            writer.WriteNumber("visibleRatio", MathF.Round(node.VisibleRatio, 3));
        }
        if (fields.Has(NodeField.Opacity) && (node.EffectiveOpacity < 1f || fields.Always(NodeField.Opacity)))
        {
            writer.WriteNumber("opacity", MathF.Round(node.EffectiveOpacity, 3));
        }
        if (fields.Has(NodeField.OnScreen))
        {
            writer.WriteBoolean("onScreen", visible is not null);
        }
        if (fields.Has(NodeField.Tap | NodeField.Hittable | NodeField.ObscuredBy))
        {
            var (tap, obscuredBy) = Geometry.TapPoint(Root, node);
            if (fields.Has(NodeField.Tap) && (tap is not null || fields.Always(NodeField.Tap)))
            {
                writer.WritePropertyName("tap");
                if (tap is { } point)
                {
                    WritePoint(writer, point);
                }
                else
                {
                    writer.WriteNullValue();
                }
            }
            if (fields.Has(NodeField.Hittable))
            {
                writer.WriteBoolean("hittable", tap is not null);
            }
            if (fields.Has(NodeField.ObscuredBy) && obscuredBy is not null && engine.NearestSemantic(obscuredBy) is { } cover && cover.Id != node.Id)
            {
                writer.WritePropertyName("obscuredBy");
                JsonSerializer.Serialize(writer, Refs.Of(cover, Root), AutomationJsonContext.Default.NodeRef);
            }
        }

        if (fields.Has(NodeField.Scroll) && node.Scroll is { } scroll)
        {
            writer.WritePropertyName("scroll");
            writer.WriteStartObject();
            writer.WritePropertyName("offset");
            WritePoint(writer, scroll.Offset);
            writer.WritePropertyName("max");
            WritePoint(writer, scroll.MaxOffset);
            writer.WritePropertyName("content");
            WriteSize(writer, scroll.ContentSize);
            writer.WritePropertyName("viewport");
            WriteSize(writer, scroll.ViewportSize);
            writer.WritePropertyName("canScroll");
            writer.WriteStartObject();
            writer.WriteBoolean("x", scroll.CanScrollHorizontal);
            writer.WriteBoolean("y", scroll.CanScrollVertical);
            writer.WriteEndObject();
            writer.WriteBoolean("animating", scroll.IsAnimating);
            writer.WriteEndObject();
        }

        var state = semantics?.Semantics ?? node.Semantics;
        String(NodeField.Value, "value", match.Value);
        String(NodeField.Description, "description", state?.Description);
        if (fields.Has(NodeField.HeadingLevel) && (state?.HeadingLevel is > 0 || fields.Always(NodeField.HeadingLevel)))
        {
            writer.WriteNumber("headingLevel", state?.HeadingLevel ?? 0);
        }
        Flag(NodeField.Disabled, "disabled", state?.Disabled == true);
        if (fields.Has(NodeField.Checked) && (state?.Checked is not null || fields.Always(NodeField.Checked)))
        {
            if (state?.Checked is { } isChecked)
            {
                writer.WriteBoolean("checked", isChecked);
            }
            else
            {
                writer.WriteNull("checked");
            }
        }
        Flag(NodeField.Selected, "selected", state?.Selected == true);
        if (fields.Has(NodeField.Expanded) && (state?.Expanded is not null || fields.Always(NodeField.Expanded)))
        {
            if (state?.Expanded is { } expanded)
            {
                writer.WriteBoolean("expanded", expanded);
            }
            else
            {
                writer.WriteNull("expanded");
            }
        }
        Flag(NodeField.Focusable, "focusable", semantics?.IsFocusable ?? node.IsFocusable);
        Flag(NodeField.Focused, "focused", node.IsFocused);

        String(NodeField.Text, "text", node.Text);
        if (fields.Has(NodeField.Selection) && node.Selection is { } selection)
        {
            writer.WritePropertyName("selection");
            writer.WriteStartObject();
            writer.WriteNumber("start", selection.Start);
            writer.WriteNumber("end", selection.End);
            writer.WriteEndObject();
        }
        String(NodeField.Placeholder, "placeholder", node.Placeholder);

        if (fields.Has(NodeField.Type))
        {
            writer.WriteString("type", node.Kind.ToString());
        }
        Flag(NodeField.Clips, "clips", node.ClipsChildren);
        Flag(NodeField.HitTestVisible, "hitTestVisible", node.IsHitTestVisible);
        Flag(NodeField.Transformed, "transformed", node.HasTransform);
        if (fields.Has(NodeField.ScreenBounds))
        {
            writer.WritePropertyName("screenBounds");
            WriteRect(writer, new System.Drawing.RectangleF(bounds.X + session.WindowPosition.X, bounds.Y + session.WindowPosition.Y, bounds.Width, bounds.Height));
        }
        if (fields.Has(NodeField.PixelBounds))
        {
            var scale = session.PixelScale;
            writer.WritePropertyName("pixelBounds");
            WriteRect(writer, new System.Drawing.RectangleF(bounds.X * scale, bounds.Y * scale, bounds.Width * scale, bounds.Height * scale));
        }

        var children = Children(match).Where(c => !visibleOnly || c.Node.VisibleBounds is not null).ToList();
        if (children.Count > 0)
        {
            if (depth == 0)
            {
                writer.WriteNumber("childCount", children.Count);
            }
            else
            {
                writer.WriteStartArray("children");
                foreach (var child in children)
                {
                    WriteNode(writer, child, depth - 1);
                }
                writer.WriteEndArray();
            }
        }
        writer.WriteEndObject();

        void String(NodeField field, string name, string? value)
        {
            if (!fields.Has(field))
            {
                return;
            }
            if (value is not null)
            {
                writer.WriteString(name, value);
            }
            else if (fields.Always(field))
            {
                writer.WriteNull(name);
            }
        }

        void Flag(NodeField field, string name, bool value)
        {
            if (fields.Has(field) && (value || fields.Always(field)))
            {
                writer.WriteBoolean(name, value);
            }
        }
    }

    private IEnumerable<Match> Children(Match match)
    {
        if (renderTree || match.Semantics is null)
        {
            foreach (var child in match.Node.Children)
            {
                yield return new Match(engine.SemanticsOf(child.Id), child, match.Depth + 1);
            }
            yield break;
        }
        foreach (var child in match.Semantics.Children)
        {
            if (engine.NodeOf(child.Id) is { } node)
            {
                yield return new Match(child, node, match.Depth + 1);
            }
        }
    }

    private static void WriteRect(Utf8JsonWriter writer, System.Drawing.RectangleF rect)
    {
        writer.WriteStartObject();
        writer.WriteNumber("x", Geometry.Round(rect.X));
        writer.WriteNumber("y", Geometry.Round(rect.Y));
        writer.WriteNumber("w", Geometry.Round(rect.Width));
        writer.WriteNumber("h", Geometry.Round(rect.Height));
        writer.WriteEndObject();
    }

    private static void WritePoint(Utf8JsonWriter writer, Vector2 point)
    {
        writer.WriteStartObject();
        writer.WriteNumber("x", Geometry.Round(point.X));
        writer.WriteNumber("y", Geometry.Round(point.Y));
        writer.WriteEndObject();
    }

    private static void WriteSize(Utf8JsonWriter writer, Vector2 size)
    {
        writer.WriteStartObject();
        writer.WriteNumber("w", Geometry.Round(size.X));
        writer.WriteNumber("h", Geometry.Round(size.Y));
        writer.WriteEndObject();
    }
}
