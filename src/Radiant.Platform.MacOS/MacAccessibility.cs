using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Radiant.Platform.MacOS;

/// <summary>
/// VoiceOver reads the UI through the window's GLFW content view: its <c>accessibilityChildren</c>
/// and <c>accessibilityFocusedUIElement</c> are replaced (on GLFW's view class, passing through for
/// views without an attached UI, as the text input methods are) to answer with
/// <c>NSAccessibilityElement</c>s built from the UI's tree.
/// <list type="bullet">
/// <item>The tree is read from the <see cref="IAccessibilityProvider"/> only when VoiceOver asks, so an
/// app nobody is reading pays nothing. Elements are kept by node id, so VoiceOver's references
/// stay good while their nodes live.</item>
/// <item>Pressable roles are a subclass whose <c>accessibilityPerformPress</c> presses the node; any
/// element's <c>setAccessibilityFocused:</c> moves the UI's focus there.</item>
/// <item>When the UI says the tree changed, VoiceOver is told the layout changed, once until it reads
/// again; a focus move is announced as the focused element changing.</item>
/// </list>
/// </summary>
internal sealed unsafe class MacAccessibility : IAccessibility
{
    private const string ElementClass = "RadiantAccessibilityElement";
    private const string PressableClass = "RadiantAccessibilityPressable";

    private static readonly Dictionary<nint, MacAccessibility> s_views = [];
    private static readonly Dictionary<nint, (MacAccessibility Owner, int Id)> s_elements = [];
    private static readonly Dictionary<nint, Originals> s_classes = [];

    private readonly nint _view;
    private readonly Dictionary<int, Entry> _entries = [];
    private IAccessibilityProvider? _provider;
    private nint _roots;
    private bool _asked;
    private bool _told;

    public MacAccessibility(nint view)
    {
        _view = view;
        if (view == 0)
        {
            return;
        }
        using var pool = ObjC.Pool();
        var cls = ObjC.Send(view, "class");
        if (!s_classes.ContainsKey(cls))
        {
            s_classes[cls] = new Originals(
                Children: ObjC.ReplaceMethod(cls, "accessibilityChildren", (nint)(delegate* unmanaged<nint, nint, nint>)&ViewChildren, "@@:"),
                Focused: ObjC.ReplaceMethod(cls, "accessibilityFocusedUIElement", (nint)(delegate* unmanaged<nint, nint, nint>)&ViewFocused, "@@:"));
        }
        s_views[view] = this;
    }

    /// <inheritdoc/>
    public bool IsSupported => _view != 0;

    /// <inheritdoc/>
    public void Attach(IAccessibilityProvider? provider)
    {
        _provider = provider;
        if (provider is null)
        {
            Clear();
        }
    }

    /// <inheritdoc/>
    public void Invalidate()
    {
        // Told once: VoiceOver reads the tree again, and until it has, telling it again adds nothing.
        if (!_asked || _told || _view == 0)
        {
            return;
        }
        _told = true;
        using var pool = ObjC.Pool();
        ObjC.PostAccessibilityNotification(_view, "NSAccessibilityLayoutChangedNotification");
    }

    /// <inheritdoc/>
    public void FocusChanged(int id)
    {
        if (!_asked || _view == 0 || !_entries.TryGetValue(id, out var entry))
        {
            return;
        }
        using var pool = ObjC.Pool();
        ObjC.PostAccessibilityNotification(entry.Element, "NSAccessibilityFocusedUIElementChangedNotification");
    }

    /// <summary>The top-level elements, from the tree as it is now: what the view answers VoiceOver with.</summary>
    internal nint Children()
    {
        _asked = true;
        _told = false;
        if (_provider is null)
        {
            return 0;
        }
        var root = _provider.Root();
        var seen = new HashSet<int>();
        var top = new List<nint>();
        foreach (var child in root.Children)
        {
            top.Add(Sync(child, _view, root.Frame, seen));
        }
        // Nodes that went: VoiceOver lets go of their elements too.
        foreach (var id in new List<int>(_entries.Keys))
        {
            if (!seen.Contains(id))
            {
                Release(id);
            }
        }
        if (_roots != 0)
        {
            ObjC.Send(_roots, "release");
        }
        _roots = ObjC.Send(ObjC.NSArray([.. top]), "retain");
        return _roots;
    }

    /// <summary>The element with keyboard focus, or zero: asked of the UI, as focus can move between notifications.</summary>
    internal nint FocusedElement() =>
        _provider is { } provider && _entries.TryGetValue(provider.FocusedId, out var entry) ? entry.Element : 0;

    // Brings the element for a node (and its children's) up to date, making it if it's new.
    private nint Sync(AccessibilityNode node, nint parent, System.Drawing.RectangleF parentFrame, HashSet<int> seen)
    {
        seen.Add(node.Id);
        var pressable = IsPressable(node.Role);
        if (_entries.TryGetValue(node.Id, out var entry) && entry.Pressable != pressable)
        {
            Release(node.Id);
            entry = null;
        }
        if (entry is null)
        {
            var cls = pressable ? PressableElementClass() : PlainElementClass();
            var made = ObjC.Send(ObjC.Send(cls, "alloc"), "init");
            entry = new Entry(made, pressable);
            _entries[node.Id] = entry;
            s_elements[made] = (this, node.Id);
        }
        var element = entry.Element;
        var (role, subrole) = RoleOf(node);
        ObjC.Send(element, "setAccessibilityRole:", ObjC.String(role));
        ObjC.Send(element, "setAccessibilitySubrole:", subrole is null ? 0 : ObjC.String(subrole));
        ObjC.Send(element, "setAccessibilityLabel:", node.Label is null ? 0 : ObjC.String(node.Label));
        ObjC.Send(element, "setAccessibilityValue:", ValueOf(node));
        ObjC.Send(element, "setAccessibilityHelp:", node.Description is null ? 0 : ObjC.String(node.Description));
        ObjC.SendBool(element, "setAccessibilityEnabled:", !node.Disabled);
        ObjC.SendBool(element, "setAccessibilitySelected:", node.Selected);
        if (node.Expanded is { } expanded)
        {
            ObjC.SendBool(element, "setAccessibilityExpanded:", expanded);
        }
        ObjC.Send(element, "setAccessibilityParent:", parent);
        ObjC.SendRect(element, "setAccessibilityFrameInParentSpace:", InParent(node.Frame, parentFrame, parent == _view));
        var children = new List<nint>(node.Children.Count);
        foreach (var child in node.Children)
        {
            children.Add(Sync(child, element, node.Frame, seen));
        }
        ObjC.Send(element, "setAccessibilityChildren:", children.Count == 0 ? 0 : ObjC.NSArray([.. children]));
        return element;
    }

    // A frame in its parent's space, which runs up from the bottom: the view's own (unless it's
    // flipped), or the parent element's frame.
    private NSRect InParent(System.Drawing.RectangleF frame, System.Drawing.RectangleF parent, bool parentIsView)
    {
        if (parentIsView)
        {
            var height = ObjC.GetRect(_view, "bounds").Height;
            var flipped = ObjC.GetBool(_view, "isFlipped");
            return new NSRect(frame.X, flipped ? frame.Y : height - frame.Y - frame.Height, frame.Width, frame.Height);
        }
        return new NSRect(frame.X - parent.X, parent.Y + parent.Height - (frame.Y + frame.Height), frame.Width, frame.Height);
    }

    private static nint ValueOf(AccessibilityNode node)
    {
        if (node.Checked is { } isChecked)
        {
            return ObjC.Send(ObjC.Class("NSNumber"), "numberWithInt:", isChecked ? 1 : 0);
        }
        if (node.Role == AccessibilityRole.Heading)
        {
            return ObjC.Send(ObjC.Class("NSNumber"), "numberWithInt:", node.HeadingLevel);
        }
        // Static text is read by its value.
        var text = node.Role == AccessibilityRole.Text ? node.Label : node.Value;
        return text is null ? 0 : ObjC.String(text);
    }

    /// <summary>AppKit's role (and subrole) for a UI role.</summary>
    internal static (string Role, string? Subrole) RoleOf(AccessibilityNode node) => node.Role switch
    {
        AccessibilityRole.Button => ("AXButton", null),
        AccessibilityRole.Link => ("AXLink", null),
        AccessibilityRole.CheckBox => ("AXCheckBox", null),
        AccessibilityRole.Switch => ("AXCheckBox", "AXSwitch"),
        AccessibilityRole.RadioButton => ("AXRadioButton", null),
        AccessibilityRole.Slider => ("AXSlider", null),
        AccessibilityRole.TextField => ("AXTextField", null),
        AccessibilityRole.Text => ("AXStaticText", null),
        AccessibilityRole.Heading => ("AXHeading", null),
        AccessibilityRole.Image => ("AXImage", null),
        AccessibilityRole.List => ("AXList", null),
        AccessibilityRole.Tab => ("AXRadioButton", "AXTabButton"),
        AccessibilityRole.TabList => ("AXTabGroup", null),
        AccessibilityRole.Menu => ("AXMenu", null),
        AccessibilityRole.MenuItem => ("AXMenuItem", null),
        AccessibilityRole.Dialog => ("AXGroup", "AXApplicationDialog"),
        AccessibilityRole.Alert => ("AXGroup", "AXApplicationAlert"),
        AccessibilityRole.ProgressIndicator => ("AXProgressIndicator", null),
        AccessibilityRole.ScrollArea => ("AXScrollArea", null),
        AccessibilityRole.Tooltip => ("AXHelpTag", null),
        AccessibilityRole.Separator => ("AXSplitter", null),
        AccessibilityRole.Table => ("AXTable", null),
        AccessibilityRole.Row => ("AXRow", null),
        AccessibilityRole.ColumnHeader => ("AXCell", null),
        AccessibilityRole.Cell => ("AXCell", null),
        AccessibilityRole.Tree => ("AXOutline", null),
        AccessibilityRole.TreeItem => ("AXRow", "AXOutlineRow"),
        _ => ("AXGroup", null),
    };

    /// <summary>Whether VoiceOver can press an element of this role.</summary>
    internal static bool IsPressable(AccessibilityRole role) => role is AccessibilityRole.Button or AccessibilityRole.Link
        or AccessibilityRole.CheckBox or AccessibilityRole.Switch or AccessibilityRole.RadioButton or AccessibilityRole.Tab
        or AccessibilityRole.MenuItem or AccessibilityRole.ListItem or AccessibilityRole.Row or AccessibilityRole.TreeItem
        or AccessibilityRole.ColumnHeader;

    private void Release(int id)
    {
        if (_entries.Remove(id, out var entry))
        {
            s_elements.Remove(entry.Element);
            ObjC.Send(entry.Element, "release");
        }
    }

    private void Clear()
    {
        foreach (var id in new List<int>(_entries.Keys))
        {
            Release(id);
        }
        if (_roots != 0)
        {
            ObjC.Send(_roots, "release");
            _roots = 0;
        }
    }

    private static nint PlainElementClass() => ObjC.DefineSubclass(ElementClass, "NSAccessibilityElement",
        ("setAccessibilityFocused:", (nint)(delegate* unmanaged<nint, nint, byte, void>)&SetFocused, "v@:c"),
        ("isAccessibilityFocused", (nint)(delegate* unmanaged<nint, nint, byte>)&IsFocused, "c@:"));

    private static nint PressableElementClass() => ObjC.DefineSubclass(PressableClass, "NSAccessibilityElement",
        ("setAccessibilityFocused:", (nint)(delegate* unmanaged<nint, nint, byte, void>)&SetFocused, "v@:c"),
        ("isAccessibilityFocused", (nint)(delegate* unmanaged<nint, nint, byte>)&IsFocused, "c@:"),
        ("accessibilityPerformPress", (nint)(delegate* unmanaged<nint, nint, byte>)&PerformPress, "c@:"));

    // ------------------------------------------------------------------ the replacements

#pragma warning disable CA1031 // An exception must not unwind into AppKit.
    [UnmanagedCallersOnly]
    private static nint ViewChildren(nint self, nint cmd)
    {
        try
        {
            if (s_views.TryGetValue(self, out var owner) && owner._provider is not null)
            {
                return owner.Children();
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[platform] accessibility failed: {e}");
        }
        return OriginalsOf(self) is { Children: not 0 } originals ? ((delegate* unmanaged<nint, nint, nint>)originals.Children)(self, cmd) : 0;
    }

    [UnmanagedCallersOnly]
    private static nint ViewFocused(nint self, nint cmd)
    {
        if (s_views.TryGetValue(self, out var owner) && owner.FocusedElement() is var element and not 0)
        {
            return element;
        }
        return OriginalsOf(self) is { Focused: not 0 } originals ? ((delegate* unmanaged<nint, nint, nint>)originals.Focused)(self, cmd) : self;
    }

    [UnmanagedCallersOnly]
    private static byte PerformPress(nint self, nint cmd)
    {
        try
        {
            return s_elements.TryGetValue(self, out var at) && at.Owner._provider?.Press(at.Id) == true ? (byte)1 : (byte)0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[platform] accessibility press failed: {e}");
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static void SetFocused(nint self, nint cmd, byte focused)
    {
        try
        {
            if (focused != 0 && s_elements.TryGetValue(self, out var at))
            {
                at.Owner._provider?.Focus(at.Id);
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[platform] accessibility focus failed: {e}");
        }
    }

    [UnmanagedCallersOnly]
    private static byte IsFocused(nint self, nint cmd) =>
        s_elements.TryGetValue(self, out var at) && at.Owner._provider?.FocusedId == at.Id ? (byte)1 : (byte)0;
#pragma warning restore CA1031

    private static Originals? OriginalsOf(nint view) =>
        s_classes.TryGetValue(ObjC.Send(view, "class"), out var originals) ? originals : null;

    private sealed record Entry(nint Element, bool Pressable);

    private sealed record Originals(nint Children, nint Focused);
}
