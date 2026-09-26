using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>
/// Past orders, newest first: each a card with its number, date, total and status, what was in
/// it, and actions to view it or buy it again.
/// </summary>
/// <param name="Orders">The orders.</param>
public sealed partial record OrderHistory(IReadOnlyList<Order> Orders) : Component
{
    [TestId<SurfaceButton>] public static partial string ViewOrder { get; }
    [TestId<SurfaceButton>] public static partial string BuyAgain { get; }

    /// <summary>Called with an order to view it.</summary>
    public Action<Order>? OnView { get; init; }

    /// <summary>Called with an order to buy its items again.</summary>
    public Action<Order>? OnBuyAgain { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var (view, again) = (OnView, OnBuyAgain);
        return new Box
        {
            Semantics = new Semantics { Role = SemanticsRole.List, Label = "Orders" },
            Layout = new LayoutStyle { RowGap = 16, AlignSelf = Align.Stretch },
            Children = [.. Orders.Select(order => (Element?)new Box
            {
                Semantics = new Semantics { Role = SemanticsRole.ListItem, Label = $"Order {order.Number}, {order.Status}" },
                Children = [new Card([.. Parts(order)]) { Layout = new LayoutStyle { RowGap = 12 } }],
            })],
        };

        IEnumerable<Element?> Parts(Order order)
        {
            yield return new Box
            {
                Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, FlexWrap = FlexWrap.Wrap, AlignItems = Align.Center, ColumnGap = 24, RowGap = 8 },
                Children =
                [
                    Detail("Order", order.Number),
                    Detail("Placed", order.Date),
                    Detail("Total", order.Total),
                    new Box { Layout = new LayoutStyle { FlexGrow = 1 } },
                    new Tag(order.Status) { Icon = order.Complete ? "check_circle" : "local_shipping", Color = order.Complete ? SurfaceName.Success : SurfaceName.Secondary },
                ],
            };
            yield return new Divider();
            foreach (var line in order.Lines)
            {
                yield return new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 12 },
                    Children =
                    [
                        new SurfaceText(line.Name) { Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 }, MaxLines = 1 },
                        new SurfaceText($"× {line.Quantity.ToString(CultureInfo.InvariantCulture)}") { Legibility = Legibility.Medium },
                        new SurfaceText(line.Price) { Layout = new LayoutStyle { MinWidth = 64 }, Alignment = Radiant.Text.TextAlignment.End },
                    ],
                };
            }
            if (view is not null || again is not null)
            {
                yield return new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, JustifyContent = Justify.FlexEnd, ColumnGap = 8, Margin = new Edges(0, 4, 0, 0) },
                    Children =
                    [
                        view is null ? null : new SurfaceButton("View order", ButtonVariant.Text) { TestId = ViewOrder, OnPress = () => view(order) },
                        again is null ? null : new SurfaceButton("Buy again", ButtonVariant.Tonal) { TestId = BuyAgain, Icon = "replay", OnPress = () => again(order) },
                    ],
                };
            }
        }

        static Element Detail(string label, string value) => new Box
        {
            Children =
            [
                new SurfaceText(label) { TextType = TextType.LabelMedium, Legibility = Legibility.Medium },
                new SurfaceText(value) { TextType = TextType.TitleSmall },
            ],
        };
    }
}
