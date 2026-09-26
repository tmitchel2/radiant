using System;
using System.Collections.Generic;
using System.Linq;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>
/// Notifications, newest first: a header with how many are unread and a way to mark them all read,
/// then each notification with its icon, text and time, the unread ones marked with a dot and
/// read out as unread.
/// </summary>
/// <param name="Entries">The notifications.</param>
public sealed partial record NotificationFeed(IReadOnlyList<NotificationEntry> Entries) : Component
{
    [TestId<SurfaceButton>] public static partial string MarkAllRead { get; }

    /// <summary>Called to mark them all read; with none, there's no button for it.</summary>
    public Action? OnMarkAllRead { get; init; }

    /// <summary>Called with a notification when it's opened.</summary>
    public Action<NotificationEntry>? OnOpen { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var unread = Entries.Count(e => e.Unread);
        var open = OnOpen;
        return new Box
        {
            Layout = new LayoutStyle { AlignSelf = Align.Stretch, RowGap = 4 },
            Children =
            [
                new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 8, Padding = new Edges(16, 0, 8, 0) },
                    Children =
                    [
                        new SurfaceText("Notifications") { TextType = TextType.TitleLarge, HeadingLevel = 2 },
                        unread == 0 ? null : new Tag($"{unread} new"),
                        new Box { Layout = new LayoutStyle { FlexGrow = 1 } },
                        OnMarkAllRead is null || unread == 0 ? null : new SurfaceButton("Mark all as read", ButtonVariant.Text) { TestId = MarkAllRead, OnPress = OnMarkAllRead },
                    ],
                },
                new Box
                {
                    Semantics = new Semantics { Role = SemanticsRole.List, Label = "Notifications" },
                    Children = [.. Entries.Select(entry => (Element?)new ListItem(entry.Text)
                    {
                        LeadingIcon = entry.Icon,
                        SupportingText = entry.Time,
                        OnPress = open is null ? null : () => open(entry),
                        Trailing = entry.Unread ? new Box
                        {
                            Semantics = new Semantics { Role = SemanticsRole.Text, Label = "Unread" },
                            Layout = new LayoutStyle { Width = 8, Height = 8 },
                            Background = theme.Get(SurfaceName.Primary),
                            CornerRadii = Radiant.Graphics2D.CornerRadii.All(4),
                        } : null,
                    })],
                },
            ],
        };
    }
}
