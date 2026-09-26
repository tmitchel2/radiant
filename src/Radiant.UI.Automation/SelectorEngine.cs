using System.Numerics;
using Radiant.Host.AgentControlProtocol;
using Radiant.UI.Core;

namespace Radiant.UI.Automation;

/// <summary>A node a selector matched: its semantics (null for a node only the render tree has) and its layout.</summary>
internal sealed record Match(SemanticsNode? Semantics, UINode Node, int Depth)
{
    public int Id => Node.Id;

    public string? Role => Semantics is { } semantics ? RoleName(semantics.Role) : null;

    public string? Label => Semantics?.Label;

    public string? TestId => Semantics?.TestId ?? Node.TestId;

    public string? Value => Semantics?.Semantics.Value ?? (Node.Kind == UINodeKind.EditableText ? Node.Text : null) ?? InnerValue;

    /// <summary>
    /// For a container named for tests (no role of its own), the value of the field inside it: what
    /// <c>@name</c> on a text field component means.
    /// </summary>
    public string? InnerValue { get; init; }

    public static string RoleName(SemanticsRole role)
    {
        var name = role.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}

/// <summary>
/// Finds what a <see cref="Selector"/> means in a UI: its tests run over the semantics tree (the tree
/// assistive technology sees), each node joined to its laid-out node for geometry.
/// </summary>
internal sealed class SelectorEngine
{
    private readonly UIRoot _root;
    private readonly List<(Match Match, int Parent)> _nodes = [];
    private readonly Dictionary<int, UINode> _layout = [];
    private readonly Dictionary<int, int> _index = [];

    public SelectorEngine(UIRoot root)
    {
        _root = root;
        Tree = root.GetSemantics();
        if (root.RootNode is { } rootNode)
        {
            // Every laid-out node by id, once: looking each up in the tree would be quadratic.
            var stack = new Stack<UINode>([rootNode]);
            while (stack.TryPop(out var node))
            {
                _layout[node.Id] = node;
                foreach (var child in node.Children)
                {
                    stack.Push(child);
                }
            }
            Add(Tree, rootNode, -1, 0);
        }
    }

    /// <summary>The laid-out node <paramref name="id"/> names, if there is one.</summary>
    public UINode? NodeOf(int id) => _layout.GetValueOrDefault(id);

    /// <summary>The semantics tree it searched.</summary>
    public SemanticsNode Tree { get; }

    /// <summary>Every node, in tree order, the root first.</summary>
    public IEnumerable<Match> All => _nodes.Select(n => n.Match);

    /// <summary>The semantics of the node <paramref name="id"/> names, if it has any.</summary>
    public SemanticsNode? SemanticsOf(int id) => _index.TryGetValue(id, out var index) ? _nodes[index].Match.Semantics : null;

    /// <summary>The nearest node at or above <paramref name="node"/> that has semantics.</summary>
    public Match? NearestSemantic(UINode node)
    {
        for (UINode? at = node; at is not null; at = at.Parent)
        {
            if (_index.TryGetValue(at.Id, out var index))
            {
                return _nodes[index].Match;
            }
        }
        return null;
    }

    /// <summary>The labelled ancestors of a node, outermost first, and it: <c>dialog "Edit" &gt; button "Save"</c>.</summary>
    public string PathOf(int id)
    {
        var index = _index.GetValueOrDefault(id, -1);
        var parts = new List<string>();
        for (var at = index; at > 0; at = _nodes[at].Parent)
        {
            var match = _nodes[at].Match;
            if (at == index || match.Label is not null || match.TestId is not null)
            {
                parts.Add(Describe(match));
            }
        }
        parts.Reverse();
        return string.Join(" > ", parts);
    }

    /// <summary>Every node <paramref name="selector"/> matches, in tree order, before its index is applied.</summary>
    public List<Match> FindAll(Selector selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        HashSet<int>? scopes = null;
        if (selector.Within is { } within)
        {
            scopes = [.. Find(within).Select(m => m.Id)];
        }
        HashSet<int>? inner = null;
        if (selector.Has is { } has)
        {
            inner = [.. Find(has).Select(m => m.Id)];
        }
        var found = new List<Match>();
        for (var i = 1; i < _nodes.Count; i++)
        {
            var (match, _) = _nodes[i];
            if (Passes(selector, match) && (scopes is null || HasAncestorIn(i, scopes)) && (inner is null || Descendants(match).Any(d => inner.Contains(d.Id))))
            {
                found.Add(match);
            }
        }
        // A node's id names it even where the semantics tree leaves it out.
        if (found.Count == 0 && selector.Id is { } id && scopes is null && NodeOf(id) is { } node && Passes(selector with { Id = null }, new Match(null, node, 0)))
        {
            found.Add(new Match(null, node, 0));
        }
        return found;
    }

    /// <summary>What <paramref name="selector"/> means: every match, or the one its index picks.</summary>
    public List<Match> Find(Selector selector)
    {
        var all = FindAll(selector);
        if (selector.Index is not { } index)
        {
            return all;
        }
        var at = index < 0 ? all.Count + index : index;
        return at >= 0 && at < all.Count ? [all[at]] : [];
    }

    /// <summary>
    /// The one node <paramref name="selector"/> means, for an action: null if none; if several, the one
    /// that can be seen when only one can, the innermost when they're all one inside another, or else an
    /// <c>ambiguous</c> error listing them.
    /// </summary>
    public Match? FindOne(Selector selector)
    {
        var matches = Find(selector);
        if (matches.Count <= 1)
        {
            return matches.FirstOrDefault();
        }
        var visible = matches.Where(m => m.Node.VisibleBounds is not null).ToList();
        if (visible.Count == 1)
        {
            return visible[0];
        }
        var candidates = visible.Count > 0 ? visible : matches;
        var deepest = candidates.MaxBy(m => m.Depth)!;
        if (candidates.All(m => m.Id == deepest.Id || m.Node.IsAncestorOf(deepest.Node)))
        {
            return deepest;
        }
        throw new AgentException(AgentErrorCodes.Ambiguous,
            $"{candidates.Count} elements match {selector}: {string.Join("; ", candidates.Take(5).Select(Describe))}. Add [n] to pick one, or narrow it.",
            Json.Refs(candidates.Take(10).Select(m => Refs.Of(m, _root))));
    }

    /// <summary>
    /// Nodes that nearly match, for a <c>no_match</c> error: of the role asked for, if one was, with a test
    /// ID, label or value that contains what was asked for (or it them), has its letters in order, or is
    /// a typo or two from it.
    /// </summary>
    public List<Match> Suggest(Selector selector)
    {
        var candidates = All.Skip(1).Where(m => selector.Role is null || m.Role is not null && Normalize(m.Role) == Normalize(selector.Role)).ToList();
        var wanted = new[] { selector.TestId, selector.Label?.Value, selector.Text?.Value, selector.Value?.Value }
            .Where(s => !string.IsNullOrEmpty(s)).Select(s => s!.ToLowerInvariant()).ToList();
        if (wanted.Count == 0)
        {
            return selector.Role is not null ? [.. candidates.Take(5)] : [];
        }
        return [.. candidates
            .Select(m => (Match: m, Score: wanted.Min(w => Score(w, m))))
            .Where(s => s.Score < int.MaxValue)
            .OrderBy(s => s.Score)
            .Select(s => s.Match)
            .Take(5)];

        static int Score(string wanted, Match match)
        {
            var best = int.MaxValue;
            foreach (var candidate in new[] { match.TestId, match.Label, match.Value })
            {
                if (string.IsNullOrEmpty(candidate))
                {
                    continue;
                }
                var lower = candidate.ToLowerInvariant();
                var score = lower.Contains(wanted, StringComparison.Ordinal) || lower.Length >= Math.Max(3, wanted.Length / 2) && wanted.Contains(lower, StringComparison.Ordinal) ? 0
                    : wanted.Length >= 3 && InOrder(wanted, lower) ? 1
                    : Levenshtein(wanted, lower) is var distance && distance <= Math.Max(1, wanted.Length / 4) ? 1 + distance
                    : int.MaxValue;
                best = Math.Min(best, score);
            }
            return best;
        }

        // Whether every letter of what was asked for appears in the candidate, in order: an abbreviation.
        static bool InOrder(string wanted, string candidate)
        {
            var at = 0;
            foreach (var c in candidate)
            {
                if (at < wanted.Length && c == wanted[at])
                {
                    at++;
                }
            }
            return at == wanted.Length;
        }
    }

    /// <summary>
    /// What takes focus for <paramref name="match"/>: itself if it can, else the first thing inside it that
    /// can (a text field component's input, named on its outer box).
    /// </summary>
    public Match? Focusable(Match match)
    {
        if (match.Node.IsFocusable)
        {
            return match;
        }
        return Descendants(match).FirstOrDefault(m => m.Node.IsFocusable);
    }

    /// <summary>What's inside <paramref name="match"/> in the semantics tree, in tree order.</summary>
    public IEnumerable<Match> Descendants(Match match)
    {
        if (!_index.TryGetValue(match.Id, out var index))
        {
            yield break;
        }
        var depth = _nodes[index].Match.Depth;
        for (var i = index + 1; i < _nodes.Count && _nodes[i].Match.Depth > depth; i++)
        {
            yield return _nodes[i].Match;
        }
    }

    /// <summary>A node in the log's words: <c>button "Save" @save #412</c>.</summary>
    public static string Describe(Match match)
    {
        var text = match.Role ?? match.Node.Kind.ToString().ToLowerInvariant();
        if (match.Label is { } label)
        {
            text += $" \"{(label.Length > 40 ? label[..39] + "…" : label)}\"";
        }
        if (match.TestId is { } testId)
        {
            text += " @" + testId;
        }
        return text + " #" + match.Id;
    }

    private static bool Passes(Selector selector, Match match)
    {
        var semantics = match.Semantics;
        if (selector.TestId is { } testId && match.TestId != testId)
        {
            return false;
        }
        if (selector.Id is { } id && match.Id != id)
        {
            return false;
        }
        if (selector.Role is { } role && (match.Role is null || !Normalize(match.Role).Equals(Normalize(role), StringComparison.Ordinal)))
        {
            return false;
        }
        if (selector.Label is { } label && !label.Matches(match.Label))
        {
            return false;
        }
        if (selector.Text is { } text && !text.Matches(match.Label) && !text.Matches(match.Value) && !text.Matches(match.Node.Text))
        {
            return false;
        }
        if (selector.Value is { } value && !value.Matches(match.Value))
        {
            return false;
        }
        if (selector.Visible is { } visible && (match.Node.VisibleBounds is not null) != visible)
        {
            return false;
        }
        if (selector.Enabled is { } enabled && (semantics?.Semantics.Disabled != true) != enabled)
        {
            return false;
        }
        if (selector.Focused is { } focused && match.Node.IsFocused != focused)
        {
            return false;
        }
        if (selector.Checked is { } isChecked && (semantics?.Semantics.Checked == true) != isChecked)
        {
            return false;
        }
        return selector.Selected is not { } selected || (semantics?.Semantics.Selected == true) == selected;
    }

    private static string Normalize(string role) => role.Replace("-", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal).ToLowerInvariant();

    private bool HasAncestorIn(int index, HashSet<int> scopes)
    {
        for (var at = _nodes[index].Parent; at >= 0; at = _nodes[at].Parent)
        {
            if (scopes.Contains(_nodes[at].Match.Id))
            {
                return true;
            }
        }
        return false;
    }

    private void Add(SemanticsNode semantics, UINode node, int parent, int depth)
    {
        var index = _nodes.Count;
        _nodes.Add((new Match(semantics, node, depth) { InnerValue = InnerValueOf(semantics) }, parent));
        _index[node.Id] = index;
        foreach (var child in semantics.Children)
        {
            if (NodeOf(child.Id) is { } childNode)
            {
                Add(child, childNode, index, depth + 1);
            }
        }
    }

    // A roleless container's field's value: the first text field, slider or the like inside it.
    private static string? InnerValueOf(SemanticsNode semantics)
    {
        if (semantics.Role is not (SemanticsRole.None or SemanticsRole.Group) || semantics.Semantics.Value is not null)
        {
            return null;
        }
        var stack = new Stack<SemanticsNode>(semantics.Children.Reverse());
        while (stack.TryPop(out var node))
        {
            if (node.Role is SemanticsRole.TextField or SemanticsRole.Slider or SemanticsRole.ProgressIndicator && node.Semantics.Value is { } value)
            {
                return value;
            }
            foreach (var child in node.Children.Reverse())
            {
                stack.Push(child);
            }
        }
        return null;
    }

    private static int Levenshtein(string a, string b)
    {
        if (Math.Abs(a.Length - b.Length) > 3)
        {
            return 4;
        }
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }
        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            }
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }
}

/// <summary>Where a node can be tapped.</summary>
internal static class Geometry
{
    /// <summary>
    /// A point where a press lands on <paramref name="node"/> (or something in it): the centre of what can
    /// be seen of it, else the first of a 3×3 then a 5×5 grid over it that does. If none does, what's on top
    /// at its centre.
    /// </summary>
    public static (Vector2? Point, UINode? ObscuredBy) TapPoint(UIRoot root, UINode node)
    {
        if (node.VisibleBounds is not { } visible)
        {
            return (null, null);
        }
        var centre = new Vector2(visible.X + visible.Width / 2, visible.Y + visible.Height / 2);
        if (Lands(root, node, centre))
        {
            return (centre, null);
        }
        foreach (var steps in new[] { 3, 5 })
        {
            for (var row = 0; row < steps; row++)
            {
                for (var column = 0; column < steps; column++)
                {
                    var point = new Vector2(visible.X + visible.Width * (column + 0.5f) / steps, visible.Y + visible.Height * (row + 0.5f) / steps);
                    if (Lands(root, node, point))
                    {
                        return (point, null);
                    }
                }
            }
        }
        var hits = root.HitTest(centre);
        return (null, hits.Count > 0 ? hits[0] : null);
    }

    private static bool Lands(UIRoot root, UINode node, Vector2 point) => root.HitTest(point).Contains(node);

    public static RectValue Rect(System.Drawing.RectangleF rect) => new(Round(rect.X), Round(rect.Y), Round(rect.Width), Round(rect.Height));

    public static PointValue Point(Vector2 point) => new(Round(point.X), Round(point.Y));

    public static float Round(float value) => MathF.Round(value, 2);
}

/// <summary>Brief descriptions of nodes, for results and errors.</summary>
internal static class Refs
{
    public static NodeRef Of(Match match, UIRoot root, bool withTap = false)
    {
        var visible = match.Node.VisibleBounds;
        return new NodeRef
        {
            Id = match.Id,
            Role = match.Role,
            Label = match.Label,
            TestId = match.TestId,
            Bounds = Geometry.Rect(match.Node.Bounds),
            Visible = visible is not null,
            Tap = withTap && Geometry.TapPoint(root, match.Node).Point is { } tap ? Geometry.Point(tap) : null,
        };
    }
}
