using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Radiant.Graphics2D;
using Radiant.Input;
using Radiant.Layout;
using Radiant.Styling;

namespace Radiant.UI;

/// <summary>
/// Manages a collection of UI elements, handling updates and rendering.
/// </summary>
public class UIManager
{
    private readonly List<UIElement> _elements = [];

    /// <summary>Gets all top-level elements.</summary>
    public IReadOnlyList<UIElement> Elements => _elements;

    /// <summary>
    /// Optional stylesheet applied to the whole tree each frame by <see cref="Draw"/>. When null,
    /// only each element's inline <see cref="UIElement.Style"/> is resolved.
    /// </summary>
    public Stylesheet? Stylesheet { get; set; }

    /// <summary>Whether any UI element is capturing input.</summary>
    public bool IsCapturingInput => _elements.Any(e => e.Visible && e.IsCapturingInput);

    /// <summary>Adds an element to the manager.</summary>
    public T Add<T>(T element) where T : UIElement
    {
        _elements.Add(element);
        return element;
    }

    /// <summary>Removes an element from the manager.</summary>
    public bool Remove(UIElement element) => _elements.Remove(element);

    /// <summary>Clears all elements.</summary>
    public void Clear() => _elements.Clear();

    /// <summary>
    /// Runs the flexbox layout pass over all opted-in roots. Call once per frame <em>before</em>
    /// <see cref="Update"/> so elements have current positions for hit-testing and drawing.
    /// </summary>
    public void Layout(Vector2 viewport) => YogaLayoutEngine.Calculate(_elements, viewport);

    /// <summary>Updates all elements with current input state.</summary>
    public void Update(InputState input, double deltaTime)
    {
        // If an element is capturing input (e.g. open dropdown), only update that element
        for (var i = _elements.Count - 1; i >= 0; i--)
        {
            if (_elements[i].Visible && _elements[i].IsCapturingInput)
            {
                _elements[i].Update(input, deltaTime);
                return;
            }
        }

        // Update in reverse order so top elements get input first
        for (var i = _elements.Count - 1; i >= 0; i--)
        {
            if (_elements[i].Visible)
            {
                _elements[i].Update(input, deltaTime);
            }
        }
    }

    /// <summary>
    /// Resolves <see cref="UIElement.ResolvedStyle"/> for every element in the tree (stylesheet rules
    /// + inline style). Runs after <see cref="Update"/> so pseudo-state selectors see this frame's
    /// hover/press; invoked automatically at the start of <see cref="Draw"/>.
    /// </summary>
    public void ApplyStyles()
    {
        foreach (var element in _elements)
        {
            ApplyStyleRecursive(element);
        }
    }

    private void ApplyStyleRecursive(UIElement element)
    {
        element.ResolvedStyle = StyleResolver.Resolve(element, Stylesheet);
        if (element is IUiContainer container)
        {
            foreach (var child in container.Children)
            {
                ApplyStyleRecursive(child);
            }
        }
    }

    /// <summary>Draws all visible elements.</summary>
    public void Draw(Renderer2D renderer)
    {
        ApplyStyles();

        foreach (var element in _elements)
        {
            if (element.Visible)
            {
                element.Draw(renderer);
            }
        }

        // Draw overlays on top of all elements
        foreach (var element in _elements)
        {
            if (element.Visible)
            {
                element.DrawOverlay(renderer);
            }
        }
    }

    /// <summary>Finds an element by tag.</summary>
    public UIElement? FindByTag(object tag)
    {
        foreach (var element in _elements)
        {
            if (Equals(element.Tag, tag))
                return element;

            if (element is Panel panel)
            {
                var found = FindByTagInPanel(panel, tag);
                if (found != null) return found;
            }
        }
        return null;
    }

    private static UIElement? FindByTagInPanel(Panel panel, object tag)
    {
        foreach (var child in panel.Children)
        {
            if (Equals(child.Tag, tag))
                return child;

            if (child is Panel childPanel)
            {
                var found = FindByTagInPanel(childPanel, tag);
                if (found != null) return found;
            }
        }
        return null;
    }
}
