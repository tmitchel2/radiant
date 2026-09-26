using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>A cart: each line with its picture, name, quantity and amount, then subtotal, shipping, total and checkout.</summary>
/// <param name="Lines">What's in the cart.</param>
public sealed partial record CartSummary(IReadOnlyList<CartLine> Lines) : Component
{
    [TestId<IconButton>] public static partial string Fewer { get; }
    [TestId<IconButton>] public static partial string More { get; }
    [TestId<SurfaceButton>] public static partial string CheckOut { get; }

    /// <summary>Shipping, added to the total.</summary>
    public decimal Shipping { get; init; }

    /// <summary>The currency format, e.g. "$".</summary>
    public string Currency { get; init; } = "$";

    /// <summary>Called with a line's index and its new quantity (0 removes it).</summary>
    public Action<int, int>? OnQuantityChange { get; init; }

    /// <summary>What the checkout button does.</summary>
    public Action? OnCheckout { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        string Money(decimal amount) => Currency + amount.ToString("0.00", CultureInfo.InvariantCulture);
        var subtotal = Lines.Sum(l => l.UnitPrice * l.Quantity);
        var rows = new List<Element?> { new SurfaceText("Your cart") { TextType = TextType.TitleLarge, HeadingLevel = 2 } };
        for (var i = 0; i < Lines.Count; i++)
        {
            var index = i;
            var line = Lines[i];
            var change = OnQuantityChange;
            rows.Add(new Box
            {
                Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 12 },
                Children =
                [
                    new Image(line.Product.Picture) { Fit = ImageFit.Cover, CornerRadii = Radiant.Graphics2D.CornerRadii.All(8), Layout = new LayoutStyle { Width = 56, Height = 56 } },
                    new Box
                    {
                        Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 },
                        Children =
                        [
                            new SurfaceText(line.Product.Name) { TextType = TextType.BodyLarge, MaxLines = 1 },
                            new SurfaceText(Money(line.UnitPrice)) { Legibility = Legibility.Medium },
                        ],
                    },
                    new IconButton("remove", "Fewer") { TestId = Fewer, OnPress = () => change?.Invoke(index, line.Quantity - 1) },
                    new SurfaceText(line.Quantity.ToString(CultureInfo.InvariantCulture)) { TextType = TextType.LabelLarge },
                    new IconButton("add", "More") { TestId = More, OnPress = () => change?.Invoke(index, line.Quantity + 1) },
                    new SurfaceText(Money(line.UnitPrice * line.Quantity)) { TextType = TextType.LabelLarge, Layout = new LayoutStyle { MinWidth = 64 }, Alignment = Radiant.Text.TextAlignment.End },
                ],
            });
        }
        rows.Add(new Divider());
        rows.Add(Total("Subtotal", Money(subtotal), false));
        rows.Add(Total("Shipping", Shipping == 0 ? "Free" : Money(Shipping), false));
        rows.Add(Total("Total", Money(subtotal + Shipping), true));
        rows.Add(new SurfaceButton("Check out") { TestId = CheckOut, Icon = "lock", OnPress = OnCheckout, Layout = new LayoutStyle { AlignSelf = Align.Stretch } });
        return new Card(rows.ToArray()) { Variant = CardVariant.Outlined, Layout = new LayoutStyle { MaxWidth = 480, Padding = Edges.All(20), RowGap = 12 } };

        static Element Total(string label, string value, bool strong) => new Box
        {
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, JustifyContent = Justify.SpaceBetween },
            Children =
            [
                new SurfaceText(label) { TextType = strong ? TextType.TitleMedium : TextType.BodyMedium, Legibility = strong ? null : Legibility.Medium },
                new SurfaceText(value) { TextType = strong ? TextType.TitleMedium : TextType.BodyMedium },
            ],
        };
    }
}
