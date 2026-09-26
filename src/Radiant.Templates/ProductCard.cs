using System;
using System.Globalization;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>A product in a store's grid: its picture, badge, name, detail, rating, price and an add-to-cart button.</summary>
/// <param name="Product">The product.</param>
public sealed record ProductCard(Product Product) : Component
{
    /// <summary>What the add-to-cart button does.</summary>
    public Action? OnAddToCart { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        var product = Product;
        return new Card(
            new Box
            {
                Children =
                [
                    new Image(product.Picture)
                    {
                        Fit = ImageFit.Cover,
                        AltText = product.Name,
                        CornerRadii = Radiant.Graphics2D.CornerRadii.All(8),
                        Layout = new LayoutStyle { Height = 180, AlignSelf = Align.Stretch },
                    },
                    product.Badge is null ? null : new Box
                    {
                        Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = new Edges(8, 8, Dimension.Undefined, Dimension.Undefined) },
                        Children = [new Chip(product.Badge) { Selected = true }],
                    },
                ],
            },
            new SurfaceText(product.Name) { TextType = TextType.TitleMedium, MaxLines = 1 },
            product.Detail is null ? null : new SurfaceText(product.Detail) { Legibility = Legibility.Medium, MaxLines = 1 },
            product.Rating is not { } rating ? null : new Box
            {
                Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 2 },
                Children =
                [
                    new Surface { ContentColor = SurfaceName.Warning, Children = [new SurfaceIcon("star") { IconFilled = true, IconSize = 18 }] },
                    new SurfaceText(rating.ToString("0.0", CultureInfo.InvariantCulture)) { TextType = TextType.LabelLarge },
                ],
            },
            new Box
            {
                Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, JustifyContent = Justify.SpaceBetween, Margin = new Edges(0, 4, 0, 0) },
                Children =
                [
                    new SurfaceText(product.Price) { TextType = TextType.TitleLarge },
                    new IconButton("shopping_cart", $"Add {product.Name} to cart", IconButtonVariant.Tonal) { OnPress = OnAddToCart },
                ],
            })
        {
            Variant = CardVariant.Outlined,
            Layout = new LayoutStyle { Padding = Edges.All(12), RowGap = 6 },
        };
    }
}
