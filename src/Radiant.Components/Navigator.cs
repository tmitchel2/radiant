using System;
using System.Collections.Generic;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// The pages of a <see cref="StackNavigator"/>, from its root to the one showing: pages push the
/// next one and pop back through it (<see cref="NavigatorHooks.UseNavigator"/>).
/// </summary>
public sealed class Navigator
{
    private readonly Action<IReadOnlyList<NavigatorPage>> _set;
    private readonly Func<IReadOnlyList<NavigatorPage>> _get;

    internal Navigator(Func<IReadOnlyList<NavigatorPage>> get, Action<IReadOnlyList<NavigatorPage>> set)
    {
        _get = get;
        _set = set;
    }

    /// <summary>The pages, root first.</summary>
    public IReadOnlyList<NavigatorPage> Pages => _get();

    /// <summary>Whether there's a page to go back to.</summary>
    public bool CanGoBack => Pages.Count > 1;

    /// <summary>Shows <paramref name="page"/> over the current one, titled <paramref name="title"/>.</summary>
    public void Push(string title, Element page) => _set([.. Pages, new NavigatorPage(title, page)]);

    /// <summary>Goes back a page (nothing at the root).</summary>
    public void Pop()
    {
        var pages = Pages;
        if (pages.Count > 1)
        {
            _set([.. System.Linq.Enumerable.Take(pages, pages.Count - 1)]);
        }
    }

    /// <summary>Goes back to the root page.</summary>
    public void PopToRoot()
    {
        var pages = Pages;
        if (pages.Count > 1)
        {
            _set([pages[0]]);
        }
    }
}
