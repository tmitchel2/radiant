using System;
using System.Drawing;
using Radiant.Platform;

namespace Radiant.UI.Core;

/// <summary>
/// Connects a <see cref="UIRoot"/> to its window's <see cref="IPlatform"/>: the root's cursor
/// is shown through the platform's cursors, its text input client is focused in the platform's
/// text input, and a moving caret moves the input method's candidate window. It's also where the
/// platform's assistive technology reads the semantics tree from and presses and focuses nodes.
/// Owns the platform once attached, and disposes it.
/// </summary>
internal sealed class PlatformBinding(UIRoot root) : IDisposable, IAccessibilityProvider
{
    private IPlatform? _platform;
    private RectangleF _caret;
    private int _focused;

    /// <summary>The platform attached, if any.</summary>
    public IPlatform? Platform => _platform;

    /// <summary>Starts forwarding to <paramref name="platform"/>, bringing it up to date with the root.</summary>
    public void Attach(IPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(platform);
        if (_platform is not null)
        {
            throw new InvalidOperationException("A platform is already attached.");
        }
        _platform = platform;
        root.CursorChanged += OnCursorChanged;
        root.TextInputClientChanged += OnClientChanged;
        platform.Cursors.Show(root.Cursor);
        OnClientChanged(root.TextInputClient);
        platform.Accessibility.Attach(this);
    }

    /// <summary>
    /// After each update: if the client's caret moved (typing, scrolling, layout), tells the
    /// platform, so an open candidate window follows it.
    /// </summary>
    public void AfterUpdate()
    {
        if (_platform is null)
        {
            return;
        }
        // The frame may have changed the tree; assistive technology reads it again when it next asks.
        _platform.Accessibility.Invalidate();
        if (root.FocusedId != _focused)
        {
            _focused = root.FocusedId;
            _platform.Accessibility.FocusChanged(_focused);
        }
        if (root.TextInputClient is not { } client)
        {
            return;
        }
        var caret = client.CaretRect;
        if (caret != _caret)
        {
            _caret = caret;
            _platform.TextInput.InvalidateCaret();
        }
    }

    /// <inheritdoc/>
    public AccessibilityNode Root() => Convert(root.GetSemantics());

    /// <inheritdoc/>
    public bool Press(int id) => root.Press(id);

    /// <inheritdoc/>
    public bool Focus(int id) => root.FocusNode(id);

    /// <inheritdoc/>
    public int FocusedId => root.FocusedId;

    private static AccessibilityNode Convert(SemanticsNode node) =>
        new(node.Id, Role(node.Role), node.Label, node.Bounds, [.. System.Linq.Enumerable.Select(node.Children, Convert)])
        {
            Value = node.Semantics.Value,
            Description = node.Semantics.Description,
            Checked = node.Semantics.Checked,
            Selected = node.Semantics.Selected,
            Disabled = node.Semantics.Disabled,
            Focusable = node.IsFocusable,
            Focused = node.IsFocused,
            Expanded = node.Semantics.Expanded,
            HeadingLevel = node.Semantics.HeadingLevel,
        };

    // The platform's roles are the UI's, by name.
    private static AccessibilityRole Role(SemanticsRole role) =>
        Enum.TryParse<AccessibilityRole>(role.ToString(), out var mapped) ? mapped : AccessibilityRole.Group;

    private void OnCursorChanged(CursorShape shape) => _platform?.Cursors.Show(shape);

    private void OnClientChanged(ITextInputClient? client)
    {
        _caret = client?.CaretRect ?? default;
        _platform?.TextInput.Focus(client);
    }

    /// <summary>Stops forwarding and disposes the platform.</summary>
    public void Dispose()
    {
        if (_platform is not { } platform)
        {
            return;
        }
        root.CursorChanged -= OnCursorChanged;
        root.TextInputClientChanged -= OnClientChanged;
        platform.TextInput.Focus(null);
        platform.Accessibility.Attach(null);
        _platform = null;
        platform.Dispose();
    }
}
