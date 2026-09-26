using System.Collections.Generic;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Gallery.ThemeLab;

/// <summary>
/// A specimen of the theme beside the editor: type, every kind of button, chips, fields, selection
/// controls, tabs, cards, a list, overlays and the colour families. Everything works, so states
/// (hover, pressed, chosen, open) can be tried as well as seen.
/// </summary>
internal sealed partial record ThemePreview : Component
{
    [TestId<SurfaceButton>] public static partial string Filled { get; }
    [TestId<SurfaceButton>] public static partial string Tonal { get; }
    [TestId<SurfaceButton>] public static partial string Outlined { get; }
    [TestId<SurfaceButton>] public static partial string TextButton { get; }
    [TestId<SurfaceButton>] public static partial string Elevated { get; }
    [TestId<SurfaceButton>] public static partial string Disabled { get; }
    [TestId<IconButton>] public static partial string IconFilled { get; }
    [TestId<IconButton>] public static partial string IconTonal { get; }
    [TestId<IconButton>] public static partial string IconOutlined { get; }
    [TestId<IconButton>] public static partial string IconStandard { get; }
    [TestId<TextField>] public static partial string Name { get; }
    [TestId<TextField>] public static partial string Email { get; }
    [TestId<Checkbox>] public static partial string Agree { get; }
    [TestId<Switch>] public static partial string Notify { get; }
    [TestId<RadioGroup>] public static partial string Plan { get; }
    [TestId<Slider>] public static partial string Level { get; }
    [TestId<Tabs>] public static partial string ViewTabs { get; }
    [TestId<SegmentedButton>] public static partial string Mode { get; }
    [TestId<SurfaceButton>] public static partial string OpenDialog { get; }
    [TestId<SurfaceButton>] public static partial string OpenMenu { get; }
    [TestId<IconButton>] public static partial string Info { get; }
    [TestId<SurfaceButton>] public static partial string CloseDialog { get; }

    private static readonly string[] s_plans = ["Free", "Pro", "Team"];
    private static readonly SurfaceName[] s_families = [SurfaceName.Primary, SurfaceName.Secondary, SurfaceName.Tertiary, SurfaceName.Error, SurfaceName.Success, SurfaceName.Warning, SurfaceName.Info];
    private static readonly SurfaceName[] s_surfaces = [SurfaceName.SurfaceContainerLowest, SurfaceName.SurfaceContainerLow, SurfaceName.SurfaceContainer, SurfaceName.SurfaceContainerHigh, SurfaceName.SurfaceContainerHighest, SurfaceName.Inverse];

    public override Element? Build(BuildContext context)
    {
        var agree = context.UseState(true);
        var notify = context.UseState(false);
        var plan = context.UseState(1);
        var level = context.UseState(0.6f);
        var view = context.UseState(0);
        var mode = context.UseState((IReadOnlySet<int>)new HashSet<int> { 0 });
        var chosen = context.UseState(true);
        var list = context.UseState(0);
        var dialog = context.UseState(false);
        var menu = context.UseState(false);
        var menuAnchor = context.UseRef(new ElementRef()).Value;
        return new Box
        {
            Layout = new LayoutStyle { RowGap = 24 },
            Children =
            [
                Section("Type",
                    new SurfaceText("Display") { TextType = TextType.DisplaySmall },
                    new SurfaceText("Headline, for page titles") { TextType = TextType.HeadlineSmall },
                    new SurfaceText("Title, for cards and sections") { TextType = TextType.TitleMedium },
                    new SurfaceText("Body text sets the tone of everything you read. A good body face is calm at length and clear at small sizes.") { Legibility = Legibility.Medium },
                    new SurfaceText("Label · Overline") { TextType = TextType.LabelLarge },
                    new SurfaceText("var theme = context.UseTheme();") { TextType = TextType.Code }),
                Section("Buttons",
                    new Row(
                        new SurfaceButton("Filled") { TestId = Filled, Icon = "add" },
                        new SurfaceButton("Tonal", ButtonVariant.Tonal) { TestId = Tonal },
                        new SurfaceButton("Outlined", ButtonVariant.Outlined) { TestId = Outlined },
                        new SurfaceButton("Text", ButtonVariant.Text) { TestId = TextButton },
                        new SurfaceButton("Elevated", ButtonVariant.Elevated) { TestId = Elevated },
                        new SurfaceButton("Disabled") { TestId = Disabled, ShowDisabled = true }),
                    new Row(
                        new IconButton("favorite", "Favourite", IconButtonVariant.Filled) { TestId = IconFilled, IconFilled = true },
                        new IconButton("settings", "Settings", IconButtonVariant.Tonal) { TestId = IconTonal },
                        new IconButton("delete", "Delete", IconButtonVariant.Outlined) { TestId = IconOutlined },
                        new IconButton("share", "Share") { TestId = IconStandard })),
                Section("Chips and tags",
                    new Row(
                        new Chip("Assist") { Icon = "event" },
                        new Chip("Chosen") { Selected = chosen.Value, OnPress = () => chosen.Set(!chosen.Value) },
                        new Chip("Elevated") { Elevated = true },
                        new Tag("Shipped") { Icon = "local_shipping" },
                        new Tag("Beta") { Color = SurfaceName.Tertiary })),
                Section("Fields and controls",
                    new Row(
                        new TextField("Name") { TestId = Name, InitialText = "Ada Lovelace", Layout = new LayoutStyle { Width = 220 } },
                        new TextField("Email") { TestId = Email, Variant = TextFieldVariant.Outlined, LeadingIcon = "mail", Layout = new LayoutStyle { Width = 220 } }) { Gap = 16 },
                    new Row(
                        new Checkbox(agree.Value, agree.Set) { TestId = Agree, Label = "I agree" },
                        new Switch(notify.Value, notify.Set) { TestId = Notify, Label = "Notify me" },
                        new RadioGroup(s_plans, plan.Value, plan.Set) { TestId = Plan, Label = "Plan", Horizontal = true }) { Gap = 16 },
                    new Box
                    {
                        Layout = new LayoutStyle { MaxWidth = 420, RowGap = 12 },
                        Children =
                        [
                            new Slider(level.Value, level.Set) { TestId = Level, Label = "Level" },
                            new LinearProgress { Value = level.Value, Label = "Progress" },
                        ],
                    }),
                Section("Navigation",
                    new Tabs([new Tab("Overview") { Icon = "dashboard" }, new Tab("Activity") { Icon = "history" }, new Tab("Settings") { Icon = "settings" }], view.Value, view.Set) { TestId = ViewTabs },
                    new SegmentedButton([new Segment("Day"), new Segment("Week"), new Segment("Month")], mode.Value, mode.Set) { TestId = Mode }),
                Section("Cards and lists",
                    new Row(
                        new Card(new SurfaceText("Elevated card")) { Layout = new LayoutStyle { Width = 150 } },
                        new Card(new SurfaceText("Filled card")) { Variant = CardVariant.Filled, Layout = new LayoutStyle { Width = 150 } },
                        new Card(new SurfaceText("Outlined card")) { Variant = CardVariant.Outlined, Layout = new LayoutStyle { Width = 150 } }) { Gap = 16 },
                    new Card(
                        new ListItem("Inbox") { LeadingIcon = "inbox", TrailingText = "24", Selected = list.Value == 0, OnPress = () => list.Set(0) },
                        new ListItem("Starred") { LeadingIcon = "star", SupportingText = "Messages you marked", Selected = list.Value == 1, OnPress = () => list.Set(1) },
                        new ListItem("Archive") { LeadingIcon = "archive", Selected = list.Value == 2, OnPress = () => list.Set(2) })
                    {
                        Variant = CardVariant.Outlined,
                        Layout = new LayoutStyle { MaxWidth = 360, Padding = Edges.Symmetric(0, 8) },
                    }),
                Section("Overlays",
                    new Row(
                        new SurfaceButton("Open dialog", ButtonVariant.Outlined) { TestId = OpenDialog, Icon = "open_in_new", OnPress = () => dialog.Set(true) },
                        new Box { Ref = menuAnchor, Children = [new SurfaceButton("Menu", ButtonVariant.Tonal) { TestId = OpenMenu, Icon = "menu", OnPress = () => menu.Set(true) }] },
                        new Tooltip("Tooltips wait 600 ms", new IconButton("info", "About tooltips") { TestId = Info }))),
                Section("Colour",
                    new Row([.. Swatches(s_families, container: false)]),
                    new Row([.. Swatches(s_families, container: true)]),
                    new Row([.. Swatches(s_surfaces, container: false)])),
                new Menu(menuAnchor, menu.Value, () => menu.Set(false),
                [
                    new MenuItem("Copy") { Icon = "content_copy", Shortcut = "⌘C" },
                    new MenuItem("Paste") { Icon = "content_paste", Shortcut = "⌘V" },
                    new MenuItem("Delete") { Icon = "delete", DividerBefore = true },
                ]),
                new Dialog(dialog.Value, () => dialog.Set(false))
                {
                    Icon = "palette",
                    Title = "A dialog in this theme",
                    Text = "Its shape, padding, title and surface all come from the theme.",
                    Actions = [new SurfaceButton("Close", ButtonVariant.Text) { TestId = CloseDialog, OnPress = () => dialog.Set(false) }],
                },
            ],
        };
    }

    private static Box Section(string title, params Element?[] content) => new()
    {
        Layout = new LayoutStyle { RowGap = 12 },
        Children = [new SurfaceText(title) { TextType = TextType.TitleSmall, HeadingLevel = 3, Legibility = Legibility.Medium }, .. content],
    };

    private static IEnumerable<Element?> Swatches(IReadOnlyList<SurfaceName> names, bool container)
    {
        foreach (var name in names)
        {
            yield return new Surface
            {
                SurfaceColor = name,
                SurfaceContainerToggle = container,
                CornerShape = CornerShapeRole.Small,
                Layout = new LayoutStyle { Width = 88, Height = 44, Padding = Edges.All(6), JustifyContent = Justify.FlexEnd },
                Children = [new SurfaceText(container ? $"{ThemeFields.Words(name.ToString())} container" : ThemeFields.Words(name.ToString())) { TextType = TextType.LabelSmall, MaxLines = 1 }],
            };
        }
    }
}
