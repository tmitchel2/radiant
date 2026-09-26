using System;
using System.Collections.Generic;
using System.Linq;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Templates;
using Radiant.Text;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Gallery;

/// <summary>The gallery's desktop shell pages, each a whole window shown in a frame.</summary>
internal static class ShellPages
{
    public static Element Workspace() => Frame(new WorkspacePage());

    public static Element Mail() => Frame(new MailPage());

    public static Element NewProject() => Frame(new NewProjectPage(), 560);

    public static Element Preferences() => Frame(new PreferencesPage(), 600);

    public static Element Docking() => Frame(new DockingPage());

    // A window-sized preview: outlined, rounded and clipped, at a fixed height.
    private static Surface Frame(Element shell, float height = 640) => new()
    {
        SurfaceColor = SurfaceName.Surface,
        ShowOutline = true,
        CornerShape = CornerShapeRole.Medium,
        ClipContent = true,
        // Inset by the outline's width, so the shell doesn't paint over it.
        Layout = new LayoutStyle { Height = height, Padding = Edges.All(1) },
        Children = [shell],
    };

    private static readonly string[] s_program =
    [
        "using Radiant.Components;",
        "using Radiant.UI.Core;",
        "",
        "namespace Hello;",
        "",
        "public sealed record Counter : Component",
        "{",
        "    public override Element? Build(BuildContext context)",
        "    {",
        "        var count = context.UseState(0);",
        "        return new SurfaceButton($\"Pressed {count.Value} times\")",
        "        {",
        "            OnPress = () => count.Set(count.Value + 1),",
        "        };",
        "    }",
        "}",
    ];

    private static readonly string[] s_readme =
    [
        "# Hello",
        "",
        "A counter, built with Radiant.",
        "",
        "    dotnet run",
    ];

    private static readonly string[] s_terminal =
    [
        "$ dotnet build",
        "  Hello -> bin/Debug/net10.0/Hello.dll",
        "Build succeeded.",
        "    0 Warning(s)",
        "    0 Error(s)",
        "$ ",
    ];

    /// <summary>Monospaced lines with line numbers.</summary>
    private sealed record CodeView(IReadOnlyList<string> Lines, bool Numbers = true) : Component
    {
        public override Element? Build(BuildContext context)
        {
            var theme = context.UseTheme();
            var surface = context.UseSurface();
            var style = theme.Text(TextType.BodyMedium) with { FontFamily = FontLibrary.JetBrainsMono, Size = 13, LineHeight = 20 };
            var text = style with { Color = theme.ContentColor(surface) };
            var faint = style with { Color = theme.ContentColor(surface with { Content = surface.Content with { Opacity = Legibility.Low } }) };
            var rows = Lines.Select((line, i) => (Element?)new Box
            {
                Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, ColumnGap = 16 },
                Children =
                [
                    Numbers ? new TextBlock((i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))
                    {
                        Style = faint,
                        Alignment = TextAlignment.End,
                        IsDecorative = true,
                        Layout = new LayoutStyle { Width = 28, FlexShrink = 0 },
                    } : null,
                    new TextBlock(line.Length == 0 ? " " : line) { Style = text, Wrap = false },
                ],
            }).ToArray();
            return new ScrollArea
            {
                Behaviour = new Radiant.Scrolling.ScrollBehaviour { Axes = Radiant.Scrolling.ScrollAxes.Both },
                // Code reads left to right in any language.
                Layout = new LayoutStyle { FlexGrow = 1, Direction = TextDirection.LeftToRight },
                ContentLayout = new LayoutStyle { Padding = new Edges(Numbers ? 8 : 16, 8, 16, 8) },
                Children = rows,
            };
        }
    }

    private sealed record WorkspacePage : Component
    {
        public override Element? Build(BuildContext context)
        {
            var activity = context.UseState(0);
            var open = context.UseState<IReadOnlyList<DocumentTab>>(
            [
                new DocumentTab("Counter.cs") { Icon = "code" },
                new DocumentTab("README.md") { Icon = "description", Modified = true },
            ]);
            var selected = context.UseState(0);
            var visible = context.UseState(true);
            var opacity = context.UseState(1f);
            var corner = context.UseState(0);

            void Open(string name, string icon)
            {
                var index = open.Value.ToList().FindIndex(t => t.Label == name);
                if (index < 0)
                {
                    open.Set([.. open.Value, new DocumentTab(name) { Icon = icon }]);
                    index = open.Value.Count;
                }
                selected.Set(index);
            }

            void Close(int index)
            {
                var tabs = open.Value.Where((_, i) => i != index).ToArray();
                open.Set(tabs);
                selected.Set(Math.Clamp(selected.Value > index ? selected.Value - 1 : selected.Value, 0, Math.Max(0, tabs.Length - 1)));
            }

            var current = open.Value.Count == 0 ? null : open.Value[Math.Min(selected.Value, open.Value.Count - 1)];
            TreeNode[] files =
            [
                new("hello", "Hello")
                {
                    Icon = "folder",
                    ExpandedIcon = "folder_open",
                    Children =
                    [
                        new("Counter.cs", "Counter.cs") { Icon = "code" },
                        new("Program.cs", "Program.cs") { Icon = "code" },
                        new("tests", "Tests") { Icon = "folder", ExpandedIcon = "folder_open", Children = [new("CounterTests.cs", "CounterTests.cs") { Icon = "code" }] },
                        new("README.md", "README.md") { Icon = "description" },
                        new("Hello.csproj", "Hello.csproj") { Icon = "description" },
                    ],
                },
            ];
            var explorer = new TreeView(files)
            {
                Label = "Explorer",
                InitialExpanded = new HashSet<string> { "hello" },
                Selected = current?.Label,
                // A single press opens a file, as editors' explorers do.
                OnSelect = n =>
                {
                    if (n.Children.Count == 0)
                    {
                        Open(n.Label, n.Icon ?? "description");
                    }
                },
            };

            var editor = new Box
            {
                Layout = new LayoutStyle { FlexGrow = 1 },
                Children =
                [
                    new DocumentTabs(open.Value, selected.Value, selected.Set)
                    {
                        OnClose = Close,
                        Actions = [new IconButton("more_horiz", "More actions")],
                    },
                    current is null
                        ? new Box { Layout = new LayoutStyle { FlexGrow = 1, JustifyContent = Justify.Center }, Children = [new EmptyState("code", "No open editors")] }
                        : new CodeView(current.Label == "README.md" ? s_readme : s_program) { Key = current.Label },
                ],
            };

            var inspector = new ScrollArea
            {
                Layout = new LayoutStyle { FlexGrow = 1 },
                Children =
                [
                    new SurfaceText("Button") { TextType = TextType.TitleSmall, HeadingLevel = 2, Layout = new LayoutStyle { Padding = new Edges(16, 12, 16, 8) } },
                    new Divider(),
                    new InspectorSection("Appearance",
                    [
                        new PropertyRow("Visible", new Switch(visible.Value, visible.Set)),
                        new PropertyRow("Opacity", new Slider(opacity.Value, opacity.Set) { Label = "Opacity" }),
                        new PropertyRow("Corners", new SegmentedButton([new Segment("Round"), new Segment("Square")], new HashSet<int> { corner.Value }, s => corner.Set(s.First()))),
                    ]),
                    new InspectorSection("Text",
                    [
                        new PropertyRow("Font", new SurfaceText("Inter")),
                        new PropertyRow("Size", new SurfaceText("14 px")),
                        new PropertyRow("Weight", new Chip("Medium")),
                    ]),
                    new InspectorSection("Events", [new PropertyRow("OnPress", new SurfaceText("count.Set(…)"))]) { Open = false },
                ],
            };

            return new WorkspaceLayout(
                [new NavItem("folder", "Explorer"), new NavItem("search", "Search"), new NavItem("bug_report", "Run and debug"), new NavItem("extension", "Extensions")],
                activity.Value,
                i => activity.Set(activity.Value == i ? -1 : i),
                editor)
            {
                Sidebar = activity.Value == 0 ? explorer : new Box
                {
                    Layout = new LayoutStyle { Padding = Edges.All(16) },
                    Children = [new SurfaceText("Nothing here yet.") { Legibility = Legibility.Medium }],
                },
                SidebarActions = [new IconButton("add", "New file")],
                Panel = new CodeView(s_terminal, Numbers: false),
                PanelTitle = "Terminal",
                Inspector = inspector,
                StatusBar = new StatusBar
                {
                    Leading = [new StatusItem("main") { Icon = "sync", OnPress = () => { } }, new StatusItem("0") { Icon = "error", Label = "No problems" }],
                    Trailing = [new StatusItem("Ln 10, Col 9"), new StatusItem("UTF-8"), new StatusItem("C#")],
                },
            };
        }
    }

    private sealed record MailPage : Component
    {
        private static readonly ListEntry[] s_messages =
        [
            new("Ada Lovelace", "Notes on the Analytical Engine") { Meta = "9:41" },
            new("Grace Hopper", "Re: the compiler, and a moth") { Meta = "8:15" },
            new("Alan Turing", "Can machines think?") { Meta = "Yesterday" },
            new("Katherine Johnson", "Trajectory checks for Friday") { Meta = "Mon" },
            new("Edsger Dijkstra", "Go to considered harmful") { Meta = "Sun" },
            new("Barbara Liskov", "Substitution, again") { Meta = "Sat" },
        ];

        public override Element? Build(BuildContext context)
        {
            var selected = context.UseState(0);
            var message = selected.Value >= 0 ? s_messages[selected.Value] : null;
            return new MasterDetail(s_messages, selected.Value, selected.Set, message is null ? null : new Box
            {
                Layout = new LayoutStyle { FlexGrow = 1, Padding = new Edges(28, 20, 28, 20), RowGap = 16 },
                Children =
                [
                    new Box
                    {
                        Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 4 },
                        Children =
                        [
                            new SurfaceText(message.Subtitle) { TextType = TextType.HeadlineSmall, HeadingLevel = 1, Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 } },
                            new IconButton("archive", "Archive"),
                            new IconButton("delete", "Delete"),
                            new IconButton("more_vert", "More"),
                        ],
                    },
                    new Box
                    {
                        Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 12 },
                        Children =
                        [
                            new Avatar(message.Title),
                            new Box { Children = [new SurfaceText(message.Title) { TextType = TextType.TitleSmall }, new SurfaceText("to me") { Legibility = Legibility.Medium }] },
                            new Box { Layout = new LayoutStyle { FlexGrow = 1 } },
                            new SurfaceText(message.Meta) { Legibility = Legibility.Medium },
                        ],
                    },
                    new SurfaceText("Thank you for the notes. I've read them twice, and I think the engine could do far more than arithmetic: " +
                        "anything whose rules can be written down, music included. Shall we meet on Thursday to go through the tables?")
                    {
                        TextType = TextType.BodyLarge,
                    },
                    new Box
                    {
                        Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, ColumnGap = 8 },
                        Children = [new SurfaceButton("Reply", ButtonVariant.Tonal) { Icon = "send" }, new SurfaceButton("Forward", ButtonVariant.Outlined)],
                    },
                ],
            })
            {
                Title = "Inbox",
                Actions = [new IconButton("edit", "Compose")],
            };
        }
    }

    private sealed record NewProjectPage : Component
    {
        public override Element? Build(BuildContext context)
        {
            var step = context.UseState(0);
            var template = context.UseState(0);
            var name = context.UseState(TextEditState.From("Hello"));
            var tests = context.UseState(true);
            var git = context.UseState(true);
            string[] templates = ["Empty app", "Sidebar app", "Document editor"];
            WizardStep[] steps =
            [
                new("Choose a template", new Box
                {
                    Layout = new LayoutStyle { RowGap = 4 },
                    Children = [.. templates.Select((t, i) => (Element?)new Radio(template.Value == i, () => template.Set(i)) { Label = t })],
                })
                {
                    Description = "What the new app starts from.",
                },
                new("Name it", new TextField("Project name") { Value = name.Value, OnChange = name.Set, Variant = TextFieldVariant.Outlined, Layout = new LayoutStyle { MaxWidth = 360 } })
                {
                    Description = "Used for the folder and the namespace.",
                    CanContinue = name.Value.Text.Trim().Length > 0,
                },
                new("Options", new Box
                {
                    Layout = new LayoutStyle { RowGap = 8, MaxWidth = 420 },
                    Children =
                    [
                        new SettingsRow("Add a test project", new Switch(tests.Value, tests.Set)),
                        new SettingsRow("Create a Git repository", new Switch(git.Value, git.Set)),
                    ],
                }),
                new("Review", new Box
                {
                    Layout = new LayoutStyle { RowGap = 6 },
                    Children =
                    [
                        new PropertyRow("Template", new SurfaceText(templates[template.Value])),
                        new PropertyRow("Name", new SurfaceText(name.Value.Text)),
                        new PropertyRow("Tests", new SurfaceText(tests.Value ? "Yes" : "No")),
                        new PropertyRow("Git", new SurfaceText(git.Value ? "Yes" : "No")),
                    ],
                })
                {
                    Description = "Check everything before the project is made.",
                },
            ];
            return new Wizard(steps, step.Value, step.Set)
            {
                Title = "New project",
                FinishText = "Create",
                OnFinish = () => step.Set(0),
                OnCancel = () => step.Set(0),
            };
        }
    }

    private sealed record PreferencesPage : Component
    {
        public override Element? Build(BuildContext context)
        {
            var category = context.UseState(0);
            var launch = context.UseState(true);
            var updates = context.UseState(true);
            var sounds = context.UseState(false);
            NavItem[] categories =
            [
                new("settings", "General"),
                new("palette", "Appearance"),
                new("keyboard", "Keyboard"),
                new("notifications", "Notifications"),
                new("person", "Account") { Section = "You" },
                new("shield", "Privacy"),
            ];
            Element content = category.Value switch
            {
                0 => new Box
                {
                    Layout = new LayoutStyle { RowGap = 32 },
                    Children =
                    [
                        new SettingsSection("Startup",
                        [
                            new SettingsRow("Open at login", new Switch(launch.Value, launch.Set)),
                            new SettingsRow("Check for updates", new Switch(updates.Value, updates.Set)) { Description = "Once a day, in the background" },
                        ]),
                        new SettingsSection("Sound", [new SettingsRow("Play sounds", new Switch(sounds.Value, sounds.Set))]),
                    ],
                },
                _ => new EmptyState(categories[category.Value].Icon, $"{categories[category.Value].Label} settings") { Description = "Nothing to set here yet." },
            };
            return new PreferencesLayout(categories, category.Value, category.Set, content);
        }
    }

    /// <summary>A docked workspace: drag a tab to another area, or move it with its context menu.</summary>
    private sealed record DockingPage : Component
    {
        private static readonly DockLayout s_initial = new()
        {
            Left = new DockGroup(["files", "outline"]),
            Center = new DockGroup(["program", "readme"]),
            Bottom = new DockGroup(["terminal", "problems"], Size: 180),
            Right = new DockGroup(["properties"], Size: 240),
        };

        public override Element? Build(BuildContext context)
        {
            var layout = context.UseState(s_initial);
            static Element Lines(params string[] lines) => new Box
            {
                Layout = new LayoutStyle { Padding = Edges.All(12), RowGap = 6 },
                Children = [.. lines.Select(l => (Element?)new SurfaceText(l) { TextType = TextType.BodyMedium })],
            };
            DockItem[] items =
            [
                new("files", "Files", Lines("Program.cs", "Counter.cs", "README.md", "Hello.csproj")) { Icon = "folder", Closable = false },
                new("outline", "Outline", Lines("Counter", "  Build(BuildContext)")) { Icon = "account_tree" },
                new("program", "Counter.cs", new CodeView(s_program)) { Icon = "code" },
                new("readme", "README.md", Lines("# Hello", "A counter, built with Radiant.")) { Icon = "description" },
                new("terminal", "Terminal", new CodeView(s_terminal, Numbers: false)) { Icon = "terminal" },
                new("problems", "Problems", Lines("No problems.")) { Icon = "error" },
                new("properties", "Properties", Lines("Name: Counter", "Kind: record", "Base: Component")) { Icon = "tune" },
            ];
            return new DockPanel(layout.Value, layout.Set, items);
        }
    }
}
