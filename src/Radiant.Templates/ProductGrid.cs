using System;
using System.Collections.Generic;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>Products in a wrapping grid of cards.</summary>
/// <param name="Products">The products.</param>
/// <param name="OnAddToCart">Called with a product's index when it's added to the cart.</param>
public sealed record ProductGrid(IReadOnlyList<Product> Products, Action<int>? OnAddToCart) : Component
{
    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        var cards = new List<Element?>();
        for (var i = 0; i < Products.Count; i++)
        {
            var index = i;
            var add = OnAddToCart;
            cards.Add(new ProductCard(Products[i]) { OnAddToCart = () => add?.Invoke(index), Key = i });
        }
        return new Grid { MinColumnWidth = 220, MaxColumns = 4, ColumnGap = 16, RowGap = 16, Children = cards };
    }
}
