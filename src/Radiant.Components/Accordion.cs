using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// Sections under headers that open and close, in an outlined card: one open at a time, or
/// several with <see cref="Multiple"/>. A header's chevron turns as it opens, and the content fades
/// in. With a header focused, Up and Down move between headers and Home and End go to the ends.
/// Which are open is kept by the accordion unless <see cref="Open"/> is set, when the owner keeps
/// it through <see cref="OnOpenChange"/>.
/// </summary>
/// <param name="Items">The sections.</param>
public sealed record Accordion(IReadOnlyList<AccordionItem> Items) : Component
{
    /// <summary>Whether several sections can be open at once.</summary>
    public bool Multiple { get; init; }

    /// <summary>The open sections' indices, when the owner keeps them.</summary>
    public IReadOnlySet<int>? Open { get; init; }

    /// <summary>The sections open at first, when the accordion keeps them.</summary>
    public IReadOnlySet<int>? InitiallyOpen { get; init; }

    /// <summary>Called with the open sections whenever one opens or closes.</summary>
    public Action<IReadOnlySet<int>>? OnOpenChange { get; init; }

    /// <summary>The accordion's size and placement.</summary>
    public LayoutStyle? Layout { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var own = context.UseState(() => (IReadOnlySet<int>)new HashSet<int>(InitiallyOpen ?? new HashSet<int>()));
        var refs = context.UseMemo(() => Enumerable.Range(0, Items.Count).Select(_ => new ElementRef()).ToArray(), Items.Count);
        var latest = context.UseRef(this);
        latest.Value = this;
        var open = Open ?? own.Value;

        void Toggle(int index)
        {
            var props = latest.Value;
            var current = props.Open ?? own.Value;
            var next = props.Multiple ? new HashSet<int>(current) : current.Where(i => i == index).ToHashSet();
            if (!next.Remove(index))
            {
                next.Add(index);
            }
            if (props.Open is null)
            {
                own.Set(next);
            }
            props.OnOpenChange?.Invoke(next);
        }

        void Focus(int index, int step)
        {
            // Skips disabled headers, which can't take focus.
            var items = latest.Value.Items;
            for (var i = index; i >= 0 && i < items.Count; i += step)
            {
                if (!items[i].Disabled)
                {
                    refs[i].Focus();
                    return;
                }
            }
        }

        var sections = new List<Element?>();
        for (var i = 0; i < Items.Count; i++)
        {
            var index = i;
            if (i > 0)
            {
                sections.Add(new Divider());
            }
            sections.Add(new Section(Items[i], open.Contains(i), refs[i], () => Toggle(index))
            {
                OnKeyDown = e =>
                {
                    var count = latest.Value.Items.Count;
                    var handled = true;
                    switch (e.Key)
                    {
                        case KeyCode.Down: Focus(index + 1, 1); break;
                        case KeyCode.Up: Focus(index - 1, -1); break;
                        case KeyCode.Home: Focus(0, 1); break;
                        case KeyCode.End: Focus(count - 1, -1); break;
                        default: handled = false; break;
                    }
                    e.Handled |= handled;
                },
            });
        }
        return new Card([.. sections])
        {
            Variant = CardVariant.Outlined,
            Layout = new LayoutStyle { AlignSelf = Align.Stretch }.Merge(Layout ?? default),
        };
    }

    /// <summary>A header and, when open, its content.</summary>
    private sealed record Section(AccordionItem Item, bool IsOpen, ElementRef Ref, Action Toggle) : Component
    {
        public Action<KeyEventArgs>? OnKeyDown { get; init; }

        public override Element? Build(BuildContext context)
        {
            var theme = context.UseTheme();
            var motion = theme.Theme.Motion;
            var duration = motion.Reduced ? TimeSpan.Zero : motion.ShortDuration;
            var turn = context.UseTransition(IsOpen ? 1f : 0f, duration, motion.Standard);
            var fade = context.UseTransition(IsOpen ? 1f : 0f, duration, motion.Standard, initial: IsOpen ? 1f : 0f);
            return new Box
            {
                Ref = Ref,
                OnKeyDown = OnKeyDown,
                Children =
                [
                    new PressableSurface
                    {
                        Label = Item.Title,
                        Expanded = IsOpen,
                        ShowDisabled = Item.Disabled ? true : null,
                        OnPress = Toggle,
                        Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 16, MinHeight = 56, Padding = Edges.Symmetric(16, 8) },
                        Children =
                        [
                            Item.Icon is null ? null : new SurfaceIcon(Item.Icon) { Legibility = Legibility.Medium },
                            new Box
                            {
                                Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1, RowGap = 2 },
                                Children =
                                [
                                    new SurfaceText(Item.Title) { TextType = TextType.TitleMedium },
                                    Item.Subtitle is null ? null : new SurfaceText(Item.Subtitle) { Legibility = Legibility.Medium },
                                ],
                            },
                            new Box
                            {
                                Transform = Matrix3x2.CreateRotation(MathF.PI * turn),
                                Layout = new LayoutStyle { Width = 24, Height = 24 },
                                Children = [new SurfaceIcon("expand_more") { Legibility = Legibility.Medium }],
                            },
                        ],
                    },
                    IsOpen ? new Box
                    {
                        Opacity = fade,
                        Layout = new LayoutStyle { Padding = new Edges(16, 0, 16, 16) },
                        Children = [Item.Content],
                    } : null,
                ],
            };
        }
    }
}
