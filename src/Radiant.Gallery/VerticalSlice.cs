using System;
using System.Collections.Generic;
using System.Linq;
using Radiant.ColorSystem;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;
using Color = Radiant.Graphics2D.Color;

namespace Radiant.Gallery;

/// <summary>
/// The first milestone: a card with filled, tonal and outlined buttons, and a button that shuffles
/// the theme (seed, variant, light or dark, corner scale), animating everything to it.
/// </summary>
internal sealed partial record VerticalSlice(ThemeController Themes) : Component
{
    [TestId<SurfaceButton>] public static partial string Filled { get; }
    [TestId<SurfaceButton>] public static partial string Tonal { get; }
    [TestId<SurfaceButton>] public static partial string Outlined { get; }
    [TestId<SurfaceButton>] public static partial string TextButton { get; }
    [TestId<SurfaceButton>] public static partial string Elevated { get; }
    [TestId<SurfaceButton>] public static partial string ShuffleTheme { get; }
    [TestId<SurfaceButton>] public static partial string Error { get; }
    [TestId<SurfaceButton>] public static partial string Success { get; }
    [TestId<SurfaceButton>] public static partial string DisabledButton { get; }
    [TestId<SurfaceButton>] public static partial string Add { get; }
    [TestId<SurfaceButton>] public static partial string Download { get; }
    [TestId<IconButton>] public static partial string SearchIcon { get; }
    [TestId<IconButton>] public static partial string Favourite { get; }
    [TestId<IconButton>] public static partial string Settings { get; }
    [TestId<IconButton>] public static partial string DeleteIcon { get; }
    [TestId<Checkbox>] public static partial string IAgree { get; }
    [TestId<Checkbox>] public static partial string NotifyMe { get; }
    [TestId<Checkbox>] public static partial string Some { get; }
    [TestId<Switch>] public static partial string WiFi { get; }
    [TestId<Switch>] public static partial string WiFiOff { get; }
    [TestId<RadioGroup>] public static partial string Size { get; }
    [TestId<Checkbox>] public static partial string DisabledCheckbox { get; }
    [TestId<SurfaceButton>] public static partial string OpenDialog { get; }
    [TestId<SurfaceButton>] public static partial string MenuButton { get; }
    [TestId<IconButton>] public static partial string AboutTooltips { get; }
    [TestId<Slider>] public static partial string Volume { get; }
    [TestId<Slider>] public static partial string Stepped { get; }
    [TestId<TextField>] public static partial string Name { get; }
    [TestId<TextField>] public static partial string Email { get; }
    [TestId<TextField>] public static partial string Code { get; }
    [TestId<ComboBox>] public static partial string City { get; }
    [TestId<DatePicker>] public static partial string StartDate { get; }
    [TestId<IconButton>] public static partial string Undo { get; }
    [TestId<IconButton>] public static partial string Redo { get; }
    [TestId<SplitButton>] public static partial string Save { get; }
    [TestId<Fab>] public static partial string Compose { get; }
    [TestId<SurfaceButton>] public static partial string Upgrade { get; }
    [TestId<NumberField>] public static partial string Quantity { get; }
    [TestId<NumberField>] public static partial string Width { get; }
    [TestId<RangeSlider>] public static partial string Price { get; }
    [TestId<SearchField>] public static partial string SearchComponents { get; }
    [TestId<Link>] public static partial string ReadTheDocs { get; }
    [TestId<SurfaceButton>] public static partial string OpenSheet { get; }
    [TestId<SurfaceButton>] public static partial string PopoverButton { get; }
    [TestId<SurfaceButton>] public static partial string OpenDeleteDialog { get; }
    [TestId<TextField>] public static partial string SearchFilter { get; }
    [TestId<Checkbox>] public static partial string InStock { get; }
    [TestId<Checkbox>] public static partial string OnSale { get; }
    [TestId<SurfaceButton>] public static partial string Reset { get; }
    [TestId<SurfaceButton>] public static partial string Apply { get; }
    [TestId<SurfaceButton>] public static partial string GotIt { get; }
    [TestId<SurfaceButton>] public static partial string Cancel { get; }
    [TestId<SurfaceButton>] public static partial string ConfirmDelete { get; }

    /// <summary>Open the dialog on the first frame (for snapshots).</summary>
    public bool StartWithDialog { get; init; }

    /// <summary>Open the menu on the first frame (for snapshots).</summary>
    public bool StartWithMenu { get; init; }

    public bool StartWithSheet { get; init; }

    private static readonly Variant[] s_variants = [Variant.TonalSpot, Variant.Vibrant, Variant.Expressive, Variant.Fidelity, Variant.Content, Variant.Neutral];
    private static readonly float[] s_corners = [0f, 0.5f, 1f, 1.5f, 2f];
    private static readonly string[] s_sizes = ["Small", "Medium"];

    public override Element? Build(BuildContext context)
    {
        var theme = context.UseTheme();
        var presses = context.UseState(0);
        var city = context.UseState((string?)"London");
        var date = context.UseState((DateOnly?)DateOnly.FromDateTime(DateTime.Today).AddDays(3));
        var agreed = context.UseState(true);
        var notify = context.UseState(false);
        var wifi = context.UseState(true);
        var size = context.UseState("Medium");
        var dialog = context.UseState(StartWithDialog);
        var menu = context.UseState(StartWithMenu);
        var sheet = context.UseState(StartWithSheet);
        var pageNumber = context.UseState(7);
        var dropped = context.UseState("");
        var quantity = context.UseState(2.0);
        var width = context.UseState(1280.0);
        var price = context.UseState((50f, 250f));
        var formats = context.UseState((IReadOnlySet<int>)new HashSet<int> { 0 });
        var align = context.UseState((IReadOnlySet<int>)new HashSet<int> { 0 });
        var popover = context.UseState(false);
        var alert = context.UseState(false);
        var popoverAnchor = context.UseRef(new ElementRef()).Value;
        var menuAnchor = context.UseRef(new ElementRef()).Value;
        var last = context.UseState("nothing yet");
        var volume = context.UseState(0.4f);
        var tab = context.UseState(0);
        var filters = context.UseState(("Open", true, false));
        var themes = Themes;
        return new Box
        {
            Layout = new LayoutStyle { RowGap = 24 },
            Children =
            [
                new Card(
                    new SurfaceText("Vertical slice") { TextType = TextType.TitleLarge },
                    new SurfaceText("A card on elevation 1 with each kind of button. Shuffle the theme and every colour, "
                        + "corner and shadow follows, animated.") { Legibility = Legibility.Medium },
                    new Row(
                        new SurfaceButton("Filled") { TestId = Filled, OnPress = () => presses.Update(n => n + 1) },
                        new SurfaceButton("Tonal", ButtonVariant.Tonal) { TestId = Tonal },
                        new SurfaceButton("Outlined", ButtonVariant.Outlined) { TestId = Outlined },
                        new SurfaceButton("Text", ButtonVariant.Text) { TestId = TextButton },
                        new SurfaceButton("Elevated", ButtonVariant.Elevated) { TestId = Elevated }) { Gap = 8 },
                    new SurfaceText($"Filled pressed {presses.Value} times") { TextType = TextType.LabelMedium, Legibility = Legibility.Medium })
                {
                    Layout = new LayoutStyle { MaxWidth = 640, Padding = Edges.All(20), RowGap = 12 },
                },
                new Row(
                    new SurfaceButton("Shuffle theme", ButtonVariant.Tonal) { TestId = ShuffleTheme, Icon = "palette", OnPress = () => Shuffle(themes) },
                    new SurfaceButton("Error") { TestId = Error, SurfaceColor = SurfaceName.Error },
                    new SurfaceButton("Success") { TestId = Success, SurfaceColor = SurfaceName.Success },
                    new SurfaceButton("Disabled") { TestId = DisabledButton, ShowDisabled = true }),
                new Row(
                    new SurfaceButton("Add", ButtonVariant.Filled) { TestId = Add, Icon = "add" },
                    new SurfaceButton("Download", ButtonVariant.Outlined) { TestId = Download, Icon = "download" },
                    new IconButton("search", "Search") { TestId = SearchIcon },
                    new IconButton("favorite", "Favourite", IconButtonVariant.Filled) { TestId = Favourite, IconFilled = true },
                    new IconButton("settings", "Settings", IconButtonVariant.Tonal) { TestId = Settings },
                    new IconButton("delete", "Delete", IconButtonVariant.Outlined) { TestId = DeleteIcon }),
                new Row(
                    new Checkbox(agreed.Value, agreed.Set) { TestId = IAgree, Label = "I agree" },
                    new Checkbox(notify.Value, notify.Set) { TestId = NotifyMe, Label = "Notify me" },
                    new Checkbox(false, null) { TestId = Some, Label = "Some", Indeterminate = true },
                    new Switch(wifi.Value, wifi.Set) { TestId = WiFi, Label = "Wi-Fi" },
                    new Switch(!wifi.Value, v => wifi.Set(!v)) { TestId = WiFiOff, AccessibleLabel = "Wi-Fi off" },
                    new RadioGroup(s_sizes, Array.IndexOf(s_sizes, size.Value), i => size.Set(s_sizes[i])) { TestId = Size, Label = "Size", Horizontal = true },
                    new Checkbox(true, null) { TestId = DisabledCheckbox, Label = "Disabled", Disabled = true }) { Gap = 12 },
                new Row(
                    new SurfaceButton("Open dialog", ButtonVariant.Outlined) { TestId = OpenDialog, Icon = "open_in_new", OnPress = () => dialog.Set(true) },
                    new Box
                    {
                        Ref = menuAnchor,
                        Children = [new SurfaceButton("Menu", ButtonVariant.Tonal) { TestId = MenuButton, Icon = "menu", OnPress = () => menu.Set(true) }],
                    },
                    new Tooltip("Tooltips wait 600 ms", new IconButton("info", "About tooltips") { TestId = AboutTooltips }),
                    new SurfaceText($"Last menu choice: {last.Value}") { Legibility = Legibility.Medium }),
                new Tabs([new Tab("Overview") { Icon = "dashboard" }, new Tab("Activity") { Icon = "history" }, new Tab("Settings") { Icon = "settings" }], tab.Value, tab.Set),
                new Row(
                    new Chip("Assist") { Icon = "event" },
                    new Chip("Open") { Selected = filters.Value.Item2, OnPress = () => filters.Set(filters.Value with { Item2 = !filters.Value.Item2 }) },
                    new Chip("Closed") { Selected = filters.Value.Item3, OnPress = () => filters.Set(filters.Value with { Item3 = !filters.Value.Item3 }) },
                    new Chip("tom@example.com") { Icon = "person", OnRemove = () => { } },
                    new Badge(new SurfaceIcon("notifications")) { Count = 3 },
                    new Badge(new SurfaceIcon("mail")) { Count = 120 },
                    new Badge(new SurfaceIcon("chat"))) { Gap = 12 },
                new Box
                {
                    Layout = new LayoutStyle { MaxWidth = 480, RowGap = 12 },
                    Children =
                    [
                        new SurfaceText($"Volume {volume.Value:0%}") { TextType = TextType.LabelLarge },
                        new Slider(volume.Value, volume.Set) { TestId = Volume, Label = "Volume" },
                        new Slider(volume.Value, volume.Set) { TestId = Stepped, Step = 0.25f, Label = "Stepped" },
                        new LinearProgress { Value = volume.Value, Label = "Progress" },
                        new LinearProgress { Label = "Loading" },
                        new Row(new CircularProgress { Value = volume.Value, Label = "Done" }, new CircularProgress { Label = "Working" }) { Gap = 16 },
                    ],
                },
                new Row(
                    new TextField("Name") { TestId = Name, SupportingText = "As it appears on your card", Layout = new LayoutStyle { Width = 260 } },
                    new TextField("Email") { TestId = Email, Variant = TextFieldVariant.Outlined, LeadingIcon = "mail", InitialText = "tom@example.com", Layout = new LayoutStyle { Width = 260 } },
                    new TextField("Code") { TestId = Code, Error = "That code has expired", MaxLength = 6, InitialText = "12345", Layout = new LayoutStyle { Width = 220 } }) { Gap = 16 },
                new Row(
                    new ComboBox("City", ["Berlin", "Lagos", "Lima", "London", "Oslo", "Paris", "Seoul", "Tokyo"], city.Value, city.Set)
                    {
                        TestId = City,
                        Variant = TextFieldVariant.Outlined,
                        LeadingIcon = "language",
                        Layout = new LayoutStyle { Width = 260 },
                    },
                    new Accordion(
                    [
                        new AccordionItem("Shipping", new SurfaceText("Two to four days, tracked.") { Legibility = Legibility.Medium }) { Icon = "schedule" },
                        new AccordionItem("Returns", new SurfaceText("Free within thirty days.") { Legibility = Legibility.Medium }) { Icon = "archive" },
                    ]) { InitiallyOpen = new HashSet<int> { 0 }, Layout = new LayoutStyle { Width = 360 } }) { Gap = 16 },
                new Row(
                    new DatePicker("Start date", date.Value, date.Set) { TestId = StartDate, Variant = TextFieldVariant.Outlined, Layout = new LayoutStyle { Width = 260 } },
                    new Card(new Calendar(date.Value, d => date.Set(d))) { Variant = CardVariant.Outlined, Layout = new LayoutStyle { Padding = Edges.All(0) } }) { Gap = 16 },
                new Row(
                    new Toolbar(
                    [
                        new IconButton("undo", "Undo") { TestId = Undo },
                        new IconButton("redo", "Redo") { TestId = Redo },
                        new Divider { Vertical = true },
                        new ToggleGroup([("format_bold", "Bold"), ("format_italic", "Italic"), ("format_underlined", "Underline")], formats.Value, formats.Set) { Multiple = true, Label = "Style" },
                        new Divider { Vertical = true },
                        new ToggleGroup([("format_align_left", "Left"), ("format_align_center", "Centre"), ("format_align_right", "Right")], align.Value, align.Set) { Label = "Alignment" },
                    ]) { Label = "Formatting" },
                    new SplitButton("Save", () => { }, [new MenuItem("Save as…", () => { }), new MenuItem("Save all", () => { })]) { TestId = Save, Icon = "save" },
                    new Fab("edit", "Compose") { TestId = Compose, Extended = true }) { Gap = 16 },
                new Alert("Your trial ends in 3 days") { Kind = AlertKind.Info, Text = "Add a payment method to keep your projects.", Actions = [new SurfaceButton("Upgrade", ButtonVariant.Text) { TestId = Upgrade }], OnDismiss = () => { } },
                new Row(
                    new Alert("Saved") { Kind = AlertKind.Success, Layout = new LayoutStyle { FlexGrow = 1, FlexBasis = 0 } },
                    new Alert("Low disk space") { Kind = AlertKind.Warning, Layout = new LayoutStyle { FlexGrow = 1, FlexBasis = 0 } },
                    new Alert("Sync failed") { Kind = AlertKind.Error, Layout = new LayoutStyle { FlexGrow = 1, FlexBasis = 0 } }) { Gap = 12 },
                new Row(
                    new NumberField("Quantity", quantity.Value, quantity.Set) { TestId = Quantity, Min = 0, Max = 99, Variant = TextFieldVariant.Outlined, Layout = new LayoutStyle { Width = 180 } },
                    new NumberField("Width", width.Value, width.Set) { TestId = Width, Min = 0, Max = 4000, Step = 10, Suffix = "px", Layout = new LayoutStyle { Width = 180 } },
                    new Box
                    {
                        Layout = new LayoutStyle { Width = 300, RowGap = 4 },
                        Children =
                        [
                            new SurfaceText($"Price ${price.Value.Item1:0} to ${price.Value.Item2:0}") { TextType = TextType.LabelLarge },
                            new RangeSlider(price.Value.Item1, price.Value.Item2, (l, h) => price.Set((l, h))) { TestId = Price, Min = 0, Max = 500, Step = 5, Label = "Price" },
                        ],
                    }) { Gap = 16 },
                new Grid
                {
                    MinColumnWidth = 360,
                    ColumnGap = 16,
                    Children =
                    [
                        new Card(new DescriptionList(
                        [
                            ("Order", DescriptionList.Text("#10482")),
                            ("Placed", DescriptionList.Text("26 September 2026")),
                            ("Status", new Tag("Shipped") { Icon = "local_shipping" }),
                            ("Deliver to", DescriptionList.Text("1 Infinite Loop, Cupertino")),
                        ]) { TermWidth = 110 }) { Variant = CardVariant.Outlined, Layout = new LayoutStyle { Padding = Edges.Symmetric(20, 4) } },
                        new Card(new Timeline(
                        [
                            new TimelineEvent("Order placed", "Mon 9:12") { Icon = "check" },
                            new TimelineEvent("Packed", "Tue 14:03") { Text = "Two boxes, 3.4 kg." },
                            new TimelineEvent("Out for delivery", "Today") { Color = SurfaceName.Tertiary },
                        ])) { Variant = CardVariant.Outlined, Layout = new LayoutStyle { Padding = Edges.All(20) } },
                    ],
                },
                new DropZone(files => dropped.Set(string.Join(", ", files.Select(System.IO.Path.GetFileName))))
                {
                    Description = dropped.Value.Length == 0 ? "Images or PDFs, from the Finder or Browse" : $"Last: {dropped.Value}",
                    Filters = [new Radiant.Platform.FileFilter("Images and PDFs", ["png", "jpg", "jpeg", "pdf"])],
                    Layout = new LayoutStyle { MaxWidth = 520 },
                },
                new Breadcrumb([new Crumb("Home", () => { }) { Icon = "home" }, new Crumb("Projects", () => { }), new Crumb("Radiant", () => { }), new Crumb("Components")]),
                new Row(
                    new SearchField("Search components") { TestId = SearchComponents, Layout = new LayoutStyle { Width = 260 } },
                    new Pagination(20, pageNumber.Value, pageNumber.Set),
                    new Link("Read the docs", () => { }) { TestId = ReadTheDocs },
                    new Kbd("⌘", "K")) { Gap = 16 },
                new Row(
                    new SurfaceButton("Open sheet", ButtonVariant.Tonal) { TestId = OpenSheet, Icon = "tune", OnPress = () => sheet.Set(true) },
                    new Box { Ref = popoverAnchor, Children = [new SurfaceButton("Popover", ButtonVariant.Outlined) { TestId = PopoverButton, OnPress = () => popover.Set(true) }] },
                    new SurfaceButton("Delete…", ButtonVariant.Text) { TestId = OpenDeleteDialog, Icon = "delete", OnPress = () => alert.Set(true) },
                    new ContextMenu(new Card(new SurfaceText("Right-click here") { Legibility = Legibility.Medium }) { Variant = CardVariant.Filled },
                    [
                        new MenuItem("Copy", () => last.Set("Copy")) { Icon = "content_copy" },
                        new MenuItem("Paste", () => last.Set("Paste")) { Icon = "content_paste" },
                    ])) { Gap = 12 },
                new Sheet(sheet.Value, () => sheet.Set(false), new Box
                {
                    Layout = new LayoutStyle { RowGap = 16 },
                    Children =
                    [
                        new TextField("Search") { TestId = SearchFilter, LeadingIcon = "search", Variant = TextFieldVariant.Outlined },
                        new Checkbox(filters.Value.Item2, v => filters.Set(filters.Value with { Item2 = v })) { TestId = InStock, Label = "In stock" },
                        new Checkbox(filters.Value.Item3, v => filters.Set(filters.Value with { Item3 = v })) { TestId = OnSale, Label = "On sale" },
                    ],
                })
                {
                    Title = "Filters",
                    Actions = [new SurfaceButton("Reset", ButtonVariant.Text) { TestId = Reset }, new SurfaceButton("Apply") { TestId = Apply, OnPress = () => sheet.Set(false) }],
                },
                new Popover(popoverAnchor, popover.Value, () => popover.Set(false), new Box
                {
                    Layout = new LayoutStyle { RowGap = 8, Width = 240 },
                    Children =
                    [
                        new SurfaceText("Popovers hold any content") { TextType = TextType.TitleSmall },
                        new SurfaceText("They close on Escape or a press outside, and give focus back.") { Legibility = Legibility.Medium },
                        new SurfaceButton("Got it", ButtonVariant.Text) { TestId = GotIt, OnPress = () => popover.Set(false) },
                    ],
                }) { Label = "About popovers" },
                new AlertDialog(alert.Value, "Delete this project?", () => alert.Set(false), () => alert.Set(false))
                {
                    Text = "Its files and history go for good.",
                    ConfirmText = "Delete",
                    Destructive = true,
                    Icon = "delete",
                },
                new Card(
                    new ListItem("Inbox") { LeadingIcon = "inbox", TrailingText = "24", OnPress = () => { }, Selected = true },
                    new ListItem("Starred") { LeadingIcon = "star", SupportingText = "Messages you marked", OnPress = () => { } },
                    new Divider(),
                    new ListItem("Archive") { LeadingIcon = "archive", TrailingIcon = "chevron_right", OnPress = () => { } })
                {
                    Variant = CardVariant.Outlined,
                    Layout = new LayoutStyle { MaxWidth = 360, Padding = Edges.Symmetric(0, 8) },
                },
                new Menu(menuAnchor, menu.Value, () => menu.Set(false),
                [
                    new MenuItem("Copy", () => last.Set("Copy")) { Icon = "content_copy", Shortcut = "⌘C" },
                    new MenuItem("Paste", () => last.Set("Paste")) { Icon = "content_paste", Shortcut = "⌘V" },
                    new MenuItem("Delete", () => last.Set("Delete")) { Icon = "delete", DividerBefore = true },
                    new MenuItem("Unavailable") { Icon = "block", Disabled = true },
                ]),
                new Dialog(dialog.Value, () => dialog.Set(false))
                {
                    Icon = "delete",
                    Title = "Delete this item?",
                    Text = "It will be gone for good. The dialog traps focus, closes on Escape or a press outside, and animates.",
                    Actions =
                    [
                        new SurfaceButton("Cancel", ButtonVariant.Text) { TestId = Cancel, OnPress = () => dialog.Set(false) },
                        new SurfaceButton("Delete", ButtonVariant.Text) { TestId = ConfirmDelete, OnPress = () => dialog.Set(false) },
                    ],
                },
                new Row(
                    new Card(new SurfaceText("Filled card")) { Variant = CardVariant.Filled },
                    new Card(new SurfaceText("Outlined card")) { Variant = CardVariant.Outlined },
                    new Card(new SurfaceText("Primary container")) { SurfaceColor = SurfaceName.Primary, SurfaceContainerToggle = true }) { Gap = 16 },
            ],
        };
    }

    private static void Shuffle(ThemeController themes)
    {
        var random = Random.Shared;
        var seed = Hct.From(random.NextDouble() * 360, 48 + random.NextDouble() * 40, 50).ToInt();
        var current = themes.Theme;
        themes.Set(current with
        {
            Colors = current.Colors with
            {
                Seed = Color.FromArgb(seed),
                Variant = s_variants[random.Next(s_variants.Length)],
                IsDark = random.Next(2) == 0,
            },
            // Corners scale from the current theme's own.
            Shape = (ThemePresets.Find(current.Name ?? "")?.Shape ?? new ShapeScale()).Scaled(s_corners[random.Next(s_corners.Length)]),
        }, TimeSpan.FromMilliseconds(400));
    }
}
