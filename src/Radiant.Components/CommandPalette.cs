using System;
using System.Collections.Generic;
using System.Linq;
using Radiant.Components.Primitives;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A searchable list of commands over the app (⌘K, Ctrl+Shift+P): typing ranks the commands by
/// <see cref="FuzzyMatch"/> on their titles and keywords; with nothing typed they're listed by
/// group. Up and Down move the highlight, Enter or a press runs the highlighted command and
/// closes, and Escape or a press outside closes. The search starts empty each time it opens.
/// </summary>
/// <param name="Open">Whether it's showing.</param>
/// <param name="OnClose">Called when it should close.</param>
/// <param name="Commands">The commands: the UI's registered ones (<c>context.UseCommands()</c>), or a list of its own.</param>
public sealed partial record CommandPalette(bool Open, Action OnClose, IReadOnlyList<Command> Commands) : Component
{
    [TestId] public static partial string Search { get; }
    [TestId] public static partial string Result { get; }

    /// <summary>The search field's placeholder.</summary>
    public string Placeholder { get; init; } = "Type a command or search";

    /// <summary>What running a command does; by default its <see cref="Command.Run"/>.</summary>
    public Action<Command>? OnRun { get; init; }

    /// <summary>What shows when nothing matches.</summary>
    public string EmptyText { get; init; } = "No matching commands";

    /// <summary>The commands a query matches, best first; with no query, every command, grouped in order.</summary>
    public static IReadOnlyList<Command> Rank(IReadOnlyList<Command> commands, string query)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(query);
        query = query.Trim();
        if (query.Length == 0)
        {
            // Groups in the order they first appear, commands in order within each.
            var groups = new List<string?>();
            foreach (var command in commands)
            {
                if (!groups.Contains(command.Group))
                {
                    groups.Add(command.Group);
                }
            }
            return [.. groups.SelectMany(g => commands.Where(c => c.Group == g))];
        }
        return
        [
            .. commands
                .Select((command, order) =>
                {
                    var title = FuzzyMatch.Score(query, command.Title);
                    // Keywords count, but less than the title.
                    var keywords = command.Keywords is null ? null : FuzzyMatch.Score(query, command.Keywords) - 10;
                    var best = title is null ? keywords : keywords is null ? title : Math.Max(title.Value, keywords.Value);
                    return (command, order, score: best);
                })
                .Where(r => r.score is not null)
                .OrderByDescending(r => r.score)
                .ThenBy(r => r.order)
                .Select(r => r.command),
        ];
    }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var query = context.UseState(TextEditState.Empty);
        var active = context.UseState(0);
        var scroll = context.UseRef(new Radiant.Scrolling.ScrollController(new Radiant.Scrolling.ScrollBehaviour())).Value;
        var list = context.UseRef(new ElementRef()).Value;
        var refs = context.UseRef(new Dictionary<string, ElementRef>()).Value;
        var open = Open;

        // Each opening starts from an empty search.
        context.UseEffect(() =>
        {
            if (open)
            {
                query.Set(TextEditState.Empty);
                active.Set(0);
            }
            return null;
        }, open);

        // What can't run now isn't offered.
        var shown = Rank([.. Commands.Where(c => c.Enabled)], query.Value.Text);
        var grouped = query.Value.Text.Trim().Length == 0;
        var highlighted = shown.Count == 0 ? -1 : Math.Clamp(active.Value, 0, shown.Count - 1);
        var (close, onRun) = (OnClose, OnRun);

        void Run(Command command)
        {
            close();
            if (onRun is not null)
            {
                onRun(command);
            }
            else
            {
                command.Run?.Invoke();
            }
        }

        void Move(int to)
        {
            if (shown.Count == 0)
            {
                return;
            }
            var next = Math.Clamp(to, 0, shown.Count - 1);
            active.Set(next);
            // Rows and group headings differ in height, so the row is found by where it's laid out.
            if (refs.TryGetValue(shown[next].Id, out var row) && row.IsMounted && list.IsMounted)
            {
                scroll.ScrollIntoView(row.Bounds.Y - list.Bounds.Y, row.Bounds.Height);
            }
        }

        var surface = context.UseSurface();
        var accent = theme.Get(SurfaceName.Primary);
        var faded = surface with { Content = surface.Content with { Opacity = Legibility.Medium } };
        var rows = new List<Element?>();
        string? group = null;
        for (var i = 0; i < shown.Count; i++)
        {
            var command = shown[i];
            if (grouped && command.Group is { } heading && heading != group)
            {
                rows.Add(new SurfaceText(heading)
                {
                    TextType = TextType.LabelMedium,
                    Legibility = Legibility.Medium,
                    Layout = new LayoutStyle { Padding = new Edges(12, rows.Count == 0 ? 4 : 12, 12, 4) },
                });
            }
            group = command.Group;
            var index = i;
            if (!refs.TryGetValue(command.Id, out var reference))
            {
                refs[command.Id] = reference = new ElementRef();
            }
            rows.Add(new Item(command, i == highlighted, reference, () => active.Set(index), () => Run(command)) { Key = command.Id });
        }

        return new Presence(Open, progress => new Portal(new Box
        {
            Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = Edges.All(0), AlignItems = Align.Center, Padding = new Edges(24, 96, 24, 24) },
            Background = theme.Scrim with { A = 0.2f * progress },
            Children =
            [
                new DismissableLayer(new FocusScope(new Surface
                {
                    SurfaceColor = SurfaceName.SurfaceContainerHigh,
                    CornerShape = CornerShapeRole.Large,
                    Elevation = ElevationLevel.Level3,
                    ClipContent = true,
                    Semantics = new Semantics { Role = SemanticsRole.Dialog, Label = "Command palette" },
                    Layout = new LayoutStyle { Width = 560, MaxHeight = 440 },
                    Children =
                    [
                        new Box
                        {
                            Opacity = progress,
                            Layout = new LayoutStyle { FlexShrink = 1 },
                            OnKeyDownCapture = e =>
                            {
                                switch (e.Key)
                                {
                                    case KeyCode.Down: Move(highlighted + 1); e.Handled = true; break;
                                    case KeyCode.Up: Move(highlighted - 1); e.Handled = true; break;
                                    case KeyCode.Enter when highlighted >= 0: Run(shown[highlighted]); e.Handled = true; break;
                                    default: break;
                                }
                            },
                            Children =
                            [
                                new Box
                                {
                                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 12, Height = 52, Padding = Edges.Symmetric(16, 0) },
                                    Children =
                                    [
                                        new SurfaceIcon("search") { Legibility = Legibility.Medium },
                                        new TextInput(query.Value, next =>
                                        {
                                            query.Set(next);
                                            active.Set(0);
                                        })
                                        {
                                            TestId = Search,
                                            // The palette's own search isn't one of the commands it lists.
                                            OffersEditCommands = false,
                                            Label = "Search commands",
                                            Placeholder = Placeholder,
                                            Style = theme.Text(TextType.BodyLarge) with { Color = theme.ContentColor(surface) },
                                            PlaceholderColor = theme.ContentColor(faded),
                                            CaretColor = accent,
                                            SelectionColor = accent with { A = 0.3f },
                                            Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 },
                                        },
                                    ],
                                },
                                new Divider(),
                                shown.Count == 0
                                    ? new SurfaceText(EmptyText) { Legibility = Legibility.Medium, Layout = new LayoutStyle { Padding = Edges.All(20) } }
                                    : new ScrollArea
                                    {
                                        Controller = scroll,
                                        Layout = new LayoutStyle { FlexShrink = 1 },
                                        ContentLayout = new LayoutStyle { Padding = Edges.All(8) },
                                        Children = [new Box { Ref = list, Semantics = new Semantics { Role = SemanticsRole.List, Label = "Commands" }, Children = rows }],
                                    },
                            ],
                        },
                    ],
                }), close),
            ],
        }));
    }

    private static float RowHeight(ResolvedTheme theme) => 40f + theme.DensityOffset;

    /// <summary>A command's row: icon, title and shortcut, filled while highlighted.</summary>
    private sealed record Item(Command Command, bool Highlighted, ElementRef Ref, Action Highlight, Action Run) : Component
    {
        public override Element? Build(BuildContext context)
        {
            var theme = context.UseTheme();
            var state = context.UseSurface().With(new SurfaceChange
            {
                Surface = Highlighted ? SurfaceName.Secondary : null,
                ToggleSurfaceContainer = Highlighted,
            });
            var (highlight, run) = (Highlight, Run);
            return ThemeContexts.Surface.Provide(state, new Box
            {
                TestId = Result,
                Ref = Ref,
                Semantics = new Semantics { Role = SemanticsRole.ListItem, Label = Command.Title, Selected = Highlighted },
                Background = Highlighted ? theme.SurfaceColor(state) : null,
                CornerRadii = theme.Corners(CornerShapeRole.Small),
                Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 12, Height = RowHeight(theme), Padding = Edges.Symmetric(12, 0) },
                OnPointerMove = _ =>
                {
                    if (!Highlighted)
                    {
                        highlight();
                    }
                },
                OnClick = e =>
                {
                    run();
                    e.Handled = true;
                },
                Children =
                [
                    new Box
                    {
                        Layout = new LayoutStyle { Width = 20, AlignItems = Align.Center },
                        Children = [Command.Icon is null ? null : new SurfaceIcon(Command.Icon) { IconSize = 20, Legibility = Highlighted ? null : Legibility.Medium }],
                    },
                    new SurfaceText(Command.Title) { MaxLines = 1, Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 } },
                    Command.Shortcut is null ? null : new SurfaceText(Command.Shortcut.ToString()) { TextType = TextType.LabelMedium, Legibility = Legibility.Medium },
                ],
            });
        }
    }
}
