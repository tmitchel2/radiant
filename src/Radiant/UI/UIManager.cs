using System.Collections.Generic;
using System.Linq;
using Radiant.Graphics2D;
using Radiant.Input;

namespace Radiant.UI;

/// <summary>
/// Manages a collection of UI elements, handling updates and rendering.
/// </summary>
public class UIManager
{
    private readonly List<UIElement> _elements = [];

    /// <summary>Gets all top-level elements.</summary>
    public IReadOnlyList<UIElement> Elements => _elements;

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

    /// <summary>Updates all elements with current input state.</summary>
    public void Update(InputState input, double deltaTime)
    {
        // Update in reverse order so top elements get input first
        for (var i = _elements.Count - 1; i >= 0; i--)
        {
            if (_elements[i].Visible)
            {
                _elements[i].Update(input, deltaTime);
            }
        }
    }

    /// <summary>Draws all visible elements.</summary>
    public void Draw(Renderer2D renderer)
    {
        foreach (var element in _elements)
        {
            if (element.Visible)
            {
                element.Draw(renderer);
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
