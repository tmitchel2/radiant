using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Gallery;

/// <summary>
/// A design tool's window built only from stock components: a header of stats and actions, a side
/// panel with a prompt and a composer, and a tabbed canvas with floating controls. It's the check
/// that a theme's component styles hold together across a whole app, not just a sheet of parts.
/// </summary>
internal sealed partial record StudioPage : Component
{
    [TestId<IconButton>] public static partial string Undo { get; }
    [TestId<IconButton>] public static partial string Redo { get; }
    [TestId<SurfaceButton>] public static partial string Versions { get; }
    [TestId<SurfaceButton>] public static partial string Save { get; }
    [TestId<SurfaceButton>] public static partial string Download { get; }
    [TestId<Tabs>] public static partial string PanelTabs { get; }
    [TestId<Tabs>] public static partial string ViewTabs { get; }
    [TestId<Chip>] public static partial string Suggestion { get; }
    [TestId<SegmentedButton>] public static partial string Mode { get; }
    [TestId<TextField>] public static partial string Prompt { get; }
    [TestId<IconButton>] public static partial string Attach { get; }
    [TestId<SurfaceButton>] public static partial string Send { get; }
    [TestId<SurfaceButton>] public static partial string Angle { get; }
    [TestId<SurfaceButton>] public static partial string Front { get; }
    [TestId<SurfaceButton>] public static partial string Top { get; }
    [TestId<IconButton>] public static partial string Fullscreen { get; }
    [TestId<ToggleButton>] public static partial string Spin { get; }
    [TestId<ToggleButton>] public static partial string Stop { get; }
    [TestId<IconButton>] public static partial string Previous { get; }
    [TestId<IconButton>] public static partial string PlayPause { get; }
    [TestId<IconButton>] public static partial string Next { get; }
    [TestId<Slider>] public static partial string Timeline { get; }
    [TestId<SurfaceButton>] public static partial string Finished { get; }

    private static readonly string[] s_ideas = ["A red-and-white lighthouse", "A tiny cottage with a garden", "A retro rocket", "A sports car", "A friendly robot", "A pine tree"];

    public override Element? Build(BuildContext context)
    {
        var panel = context.UseState(0);
        var view = context.UseState(0);
        var mode = context.UseState((IReadOnlySet<int>)new HashSet<int> { 1 });
        var prompt = context.UseState(Radiant.UI.Core.TextEditState.From(""));
        var spin = context.UseState(true);
        var playing = context.UseState(true);
        var step = context.UseState(1f);
        return new Surface
        {
            SurfaceColor = SurfaceName.SurfaceBright,
            ShowOutline = true,
            OutlineVariant = true,
            CornerShape = CornerShapeRole.Large,
            ClipContent = true,
            Layout = new LayoutStyle { Height = 720, Padding = Edges.All(1) },
            Children =
            [
                Header(),
                new Divider(),
                new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, FlexGrow = 1, FlexShrink = 1 },
                    Children =
                    [
                        SidePanel(panel.Value, panel.Set, mode.Value, mode.Set, prompt.Value, prompt.Set),
                        new Divider { Vertical = true },
                        Canvas(view.Value, view.Set, spin.Value, spin.Set, playing.Value, () => playing.Set(!playing.Value), step.Value, step.Set),
                    ],
                },
            ],
        };
    }

    private static Box Header() => new()
    {
        Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, Height = 56, Padding = Edges.Symmetric(16, 0), ColumnGap = 8 },
        Children =
        [
            new SurfaceText("Alpine Chalet") { TextType = TextType.TitleMedium, Layout = new LayoutStyle { Margin = new Edges(0, 0, 8, 0) } },
            new Tag("611 pieces"),
            new Tag("118 steps"),
            new Tag("32×32 studs"),
            new Box { Layout = new LayoutStyle { FlexGrow = 1 } },
            new IconButton("undo", "Undo") { TestId = Undo },
            new IconButton("redo", "Redo") { TestId = Redo },
            new Badge(new SurfaceButton("Versions", ButtonVariant.Text) { TestId = Versions, Icon = "history" }) { Inline = true, Count = 1 },
            new SurfaceButton("Save", ButtonVariant.Text) { TestId = Save, Icon = "cloud_upload" },
            new SurfaceButton("Download") { TestId = Download, Icon = "download" },
        ],
    };

    private static Box SidePanel(int panel, System.Action<int> choose, IReadOnlySet<int> mode, System.Action<IReadOnlySet<int>> setMode,
        TextEditState prompt, System.Action<TextEditState> setPrompt) => new()
        {
            Layout = new LayoutStyle { Width = 360, Padding = Edges.All(16), RowGap = 12 },
            Children =
        [
            new ScrollArea
            {
                Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 },
                ContentLayout = new LayoutStyle { RowGap = 12 },
                Children =
                [
            new Tabs([new Tab("Chat") { Icon = "chat" }, new Tab("Library") { Icon = "library_books" }], panel, choose) { TestId = PanelTabs },
            new SurfaceText("What should we build?") { TextType = TextType.HeadlineSmall, HeadingLevel = 1, Layout = new LayoutStyle { Margin = new Edges(0, 12, 0, 0) } },
            new SurfaceText("Describe a model or drop in a photo. We design it in bricks, check that every part holds, and write the building manual.") { Legibility = Legibility.Medium },
            new SurfaceText("Try one") { TextType = TextType.Overline, Legibility = Legibility.Medium },
            new Row([.. s_ideas.Select(idea => (Element?)new Chip(idea) { TestId = Suggestion, OnPress = () => setPrompt(TextEditState.From(idea)) })]),
            new SurfaceText("Or") { TextType = TextType.Overline, Legibility = Legibility.Medium },
            new Row(
                new Chip("Turn a photo into bricks") { TestId = Suggestion, Icon = "image" },
                new Chip("Browse examples") { TestId = Suggestion, Icon = "library_books" }),
                ],
            },
            // The composer: a framed card holding a mode switch, the prompt and its actions.
            new Card(
                new SegmentedButton([new Segment("Change this build"), new Segment("Start a new build")], mode, setMode) { TestId = Mode },
                new TextField("Prompt")
                {
                    TestId = Prompt,
                    Variant = TextFieldVariant.Plain,
                    Placeholder = "Describe what to build, or how to change it…",
                    Multiline = true,
                    Value = prompt,
                    OnChange = setPrompt,
                    Layout = new LayoutStyle { MinHeight = 44 },
                },
                new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, JustifyContent = Justify.SpaceBetween },
                    Children =
                    [
                        new IconButton("image", "Attach a photo") { TestId = Attach },
                        new SurfaceButton("Send") { TestId = Send, Icon = "send", ShowDisabled = prompt.Text.Length == 0 ? true : null },
                    ],
                })
            {
                Variant = CardVariant.Outlined,
                Layout = new LayoutStyle { Padding = Edges.All(12), RowGap = 8 },
            },
        ],
        };

    private static Box Canvas(int view, System.Action<int> choose, bool spin, System.Action<bool> setSpin, bool playing, System.Action playPause,
        float step, System.Action<float> setStep) => new()
        {
            Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1, Padding = Edges.All(16), RowGap = 12 },
            Children =
        [
            new Tabs([new Tab("Model") { Icon = "view_in_ar" }, new Tab("Manual") { Icon = "menu_book" }, new Tab("Parts") { Icon = "category" }, new Tab("Design") { Icon = "code" }], view, choose) { TestId = ViewTabs },
            new Surface
            {
                SurfaceColor = SurfaceName.SurfaceContainerLow,
                ShowOutline = true,
                OutlineVariant = true,
                CornerShape = CornerShapeRole.Medium,
                ClipContent = true,
                Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1, AlignItems = Align.Center, JustifyContent = Justify.Center },
                Children =
                [
                    Plate(),
                    // Floating over the canvas: the view toolbar, the stop button, the player and a note.
                    Floating(new Edges(12, 12, Dimension.Undefined, Dimension.Undefined), new Box
                    {
                        Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 8 },
                        Children =
                        [
                            Raised(CornerShapeRole.Medium, Edges.All(4),
                                new SurfaceButton("3/4", ButtonVariant.Text) { TestId = Angle, Icon = "view_in_ar" },
                                new SurfaceButton("Front", ButtonVariant.Text) { TestId = Front, Icon = "crop_free" },
                                new SurfaceButton("Top", ButtonVariant.Text) { TestId = Top, Icon = "grid_view" },
                                new IconButton("fullscreen", "Full screen") { TestId = Fullscreen }),
                            new ToggleButton("sync", "Spin", spin, setSpin) { TestId = Spin, ShowLabel = true },
                        ],
                    }),
                    Floating(new Edges(Dimension.Undefined, 12, 12, Dimension.Undefined), new ToggleButton("stop", "Stop", true, null) { TestId = Stop, ShowLabel = true }),
                    Floating(new Edges(Dimension.Undefined, Dimension.Undefined, Dimension.Undefined, 16), new Box
                    {
                        Layout = new LayoutStyle { AlignSelf = Align.Center },
                        Children =
                        [
                            Raised(CornerShapeRole.Full, Edges.Symmetric(8, 4),
                                new IconButton("skip_previous", "Previous step") { TestId = Previous },
                                new IconButton(playing ? "pause" : "play_arrow", playing ? "Pause" : "Play", IconButtonVariant.Filled) { TestId = PlayPause, CornerShape = CornerShapeRole.Full, OnPress = playPause },
                                new IconButton("skip_next", "Next step") { TestId = Next },
                                new SurfaceText($"{step:0} / 60") { TextType = TextType.LabelLarge, Layout = new LayoutStyle { Margin = Edges.Symmetric(4, 0) } },
                                new Box { Layout = new LayoutStyle { Width = 160 }, Children = [new LinearProgress { Value = step / 60f, Label = "Showcase progress" }] },
                                new SurfaceText("4×") { TextType = TextType.LabelLarge, Legibility = Legibility.Medium, Layout = new LayoutStyle { Margin = Edges.Symmetric(8, 0) } }),
                        ],
                    }),
                ],
            },
            new Card(new Box
            {
                Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 16 },
                Children =
                [
                    new Box
                    {
                        Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 8 },
                        Children =
                        [
                            new SurfaceText($"Showcase {step:0} / 60") { TextType = TextType.TitleSmall },
                            new SurfaceText("Main build · steps 1–3 of 75") { TextType = TextType.BodySmall, Legibility = Legibility.Medium },
                        ],
                    },
                    new Box
                    {
                        Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 },
                        Children = [new Slider(step, setStep) { TestId = Timeline, Min = 1, Max = 60, Step = 1, Label = "Showcase step" }],
                    },
                    new SurfaceButton("Finished", ButtonVariant.Outlined) { TestId = Finished },
                ],
            })
            {
                Variant = CardVariant.Outlined,
                Layout = new LayoutStyle { Padding = Edges.Symmetric(16, 8) },
            },
        ],
        };

    // The model on the canvas: a green baseplate in perspective, studded.
    private static Box Plate() => new()
    {
        Layout = new LayoutStyle { Width = 380, Height = 220 },
        Transform = Matrix3x2.CreateSkew(-0.35f, 0.08f),
        TransformOrigin = new Vector2(0.5f, 0.5f),
        Background = Radiant.Graphics2D.Color.Parse("#3c8a50"),
        CornerRadii = Radiant.Graphics2D.CornerRadii.All(4),
        Shadows = [new BoxShadow(new Vector2(0, 12), 24, -6, new Vector4(0, 0, 0, 0.25f))],
    };

    private static Box Floating(Edges inset, Element content) => new()
    {
        Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = inset },
        Children = [content],
    };

    // A raised bar of controls floating over the canvas.
    private static Surface Raised(CornerShapeRole shape, Edges padding, params Element?[] children) => new()
    {
        SurfaceColor = SurfaceName.SurfaceBright,
        ShowOutline = true,
        OutlineVariant = true,
        Elevation = ElevationLevel.Level2,
        CornerShape = shape,
        Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 2, Padding = padding },
        Children = children,
    };
}
