namespace Radiant.Theming;

/// <summary>
/// How the components are built and drawn, beyond colour, shape and type: sizes, which colour
/// families each variant uses, and which of a component's structures to build (underlined or
/// segmented tabs, floating or fixed labels). The default reproduces the tonal look;
/// <see cref="Hairline"/> is flat and edged with 1 px lines.
/// </summary>
public sealed record ComponentStyles
{
    /// <summary>Focus rings and the disabled look.</summary>
    public InteractionStyle Interaction { get; init; } = new();

    /// <summary>Icon size and stroke.</summary>
    public IconStyle Icons { get; init; } = new();

    /// <summary>Labelled buttons.</summary>
    public ButtonStyle Button { get; init; } = new();

    /// <summary>Icon buttons.</summary>
    public IconButtonStyle IconButton { get; init; } = new();

    /// <summary>Chips and tags.</summary>
    public ChipStyle Chip { get; init; } = new();

    /// <summary>Cards.</summary>
    public CardStyle Card { get; init; } = new();

    /// <summary>Menus, dialogs and popovers.</summary>
    public OverlayStyle Overlay { get; init; } = new();

    /// <summary>The app bar, drawer and tabs.</summary>
    public NavigationStyle Navigation { get; init; } = new();

    /// <summary>Text fields.</summary>
    public FieldStyle Field { get; init; } = new();

    /// <summary>Check boxes, radio buttons, switches, sliders and progress.</summary>
    public SelectionStyle Selection { get; init; } = new();

    /// <summary>List rows and accordion headers.</summary>
    public ListStyle List { get; init; } = new();

    /// <summary>
    /// Tonal: containers in tonal colours, pill controls, floating labels, underlined tabs, state
    /// layers and halos, and greyed-out disabled controls. The default.
    /// </summary>
    public static ComponentStyles Tonal { get; } = new();

    /// <summary>
    /// Hairline: flat and neutral. White surfaces edged with 1 px lines, the accent kept for the
    /// main action and what's on, quiet ink-coloured secondary buttons, compact controls, labels
    /// above fields, segmented tabs, a ringed slider thumb, no halos, and disabled controls faded.
    /// </summary>
    public static ComponentStyles Hairline { get; } = new()
    {
        Interaction = new InteractionStyle
        {
            FocusRingWidth = 2f,
            FocusRingGap = 2f,
            FocusRingColor = SurfaceName.Primary,
            Disabled = DisabledLook.Fade,
            StateLayer = StateLayerLook.Shade,
            PressScale = 0.98f,
        },
        Icons = new IconStyle { Size = 20f, Weight = 300f, FillChosen = false },
        Button = new ButtonStyle
        {
            Height = 40f,
            Padding = 14f,
            TextPadding = 12f,
            IconSize = 18f,
            Tonal = new SurfaceLook { Surface = SurfaceName.Secondary, SurfaceContainer = true },
            ToggleOn = new SurfaceLook { Surface = SurfaceName.Primary, SurfaceContainer = true },
            Outlined = new SurfaceLook { Surface = SurfaceName.SurfaceBright, Outline = true },
            Text = new SurfaceLook(),
            Elevated = new SurfaceLook { Surface = SurfaceName.SurfaceBright, Outline = true, OutlineVariant = true, Elevation = ElevationLevel.Level1 },
        },
        IconButton = new IconButtonStyle
        {
            Size = 36f,
            IconSize = 20f,
            Tonal = new SurfaceLook { Surface = SurfaceName.Secondary, SurfaceContainer = true },
            Outlined = new SurfaceLook { Surface = SurfaceName.SurfaceBright, Outline = true },
            Standard = new SurfaceLook { Content = SurfaceName.SurfaceVariant, ContentOn = true },
        },
        Chip = new ChipStyle
        {
            Height = 30f,
            Padding = 12f,
            Shape = CornerShapeRole.Full,
            Label = TextType.BodyMedium,
            Rest = new SurfaceLook { Surface = SurfaceName.SurfaceBright, Outline = true, OutlineVariant = true },
            Chosen = new SurfaceLook { Surface = SurfaceName.Secondary, SurfaceContainer = true },
            Elevated = new SurfaceLook { Surface = SurfaceName.SurfaceBright, Outline = true, OutlineVariant = true, Elevation = ElevationLevel.Level1 },
            TagHeight = 24f,
            TagShape = CornerShapeRole.Full,
            OutlinedTags = true,
        },
        Card = new CardStyle
        {
            Shape = CornerShapeRole.Medium,
            Padding = 16f,
            Elevated = new SurfaceLook { Surface = SurfaceName.SurfaceBright, Outline = true, OutlineVariant = true, Elevation = ElevationLevel.Level1 },
            Filled = new SurfaceLook { Surface = SurfaceName.SurfaceContainer },
            Outlined = new SurfaceLook { Surface = SurfaceName.SurfaceBright, Outline = true, OutlineVariant = true },
        },
        Overlay = new OverlayStyle
        {
            Menu = new SurfaceLook { Surface = SurfaceName.SurfaceBright, Outline = true, OutlineVariant = true, Elevation = ElevationLevel.Level2 },
            MenuShape = CornerShapeRole.Medium,
            MenuPadding = 4f,
            MenuItemInset = 4f,
            MenuItemHeight = 36f,
            MenuItemShape = CornerShapeRole.Small,
            MenuItemText = TextType.BodyMedium,
            MenuIconSize = 20f,
            Dialog = new SurfaceLook { Surface = SurfaceName.SurfaceBright, Outline = true, OutlineVariant = true, Elevation = ElevationLevel.Level3 },
            DialogShape = CornerShapeRole.Large,
            DialogPadding = 24f,
            DialogTitle = TextType.TitleLarge,
            Popover = new SurfaceLook { Surface = SurfaceName.SurfaceBright, Outline = true, OutlineVariant = true, Elevation = ElevationLevel.Level2 },
            ScrimOpacity = 0.4f,
            DialogLook = DialogLook.Card,
            Snackbar = SnackbarLook.Toast,
            TooltipShape = CornerShapeRole.Small,
        },
        Navigation = new NavigationStyle
        {
            AppBarHeight = 56f,
            AppBarTitle = TextType.TitleMedium,
            AppBarTonalOnScroll = false,
            DrawerSurface = SurfaceName.SurfaceContainerLow,
            DrawerDivider = true,
            DrawerItemHeight = 36f,
            DrawerItemPadding = 10f,
            DrawerItemText = TextType.BodyMedium,
            DrawerIconSize = 20f,
            DrawerSectionText = TextType.LabelMedium,
            DrawerChosen = new SurfaceLook { Surface = SurfaceName.Secondary, SurfaceContainer = true },
            Tabs = TabsLook.Segmented,
            TabText = TextType.LabelLarge,
        },
        Field = new FieldStyle
        {
            Look = FieldLook.LabelAbove,
            Height = 40f,
            Shape = CornerShapeRole.Small,
            Text = TextType.BodyMedium,
            Label = TextType.LabelLarge,
            CalendarDay = CornerShapeRole.Small,
        },
        Selection = new SelectionStyle
        {
            Halo = false,
            CheckboxSize = 16f,
            CheckboxRadius = 4f,
            BorderWidth = 1f,
            RadioSize = 16f,
            Switch = SwitchLook.Compact,
            SliderThumb = SliderThumb.Ring,
            TrackThickness = 4f,
            Track = SurfaceName.SurfaceContainerHighest,
            TrackContainer = false,
            Label = TextType.BodyMedium,
        },
        List = new ListStyle
        {
            OneLineHeight = 44f,
            LineStep = 16f,
            Padding = 12f,
            Headline = TextType.BodyMedium,
            Supporting = TextType.BodySmall,
            Shape = CornerShapeRole.Small,
            Selected = new SurfaceLook { Surface = SurfaceName.Secondary, SurfaceContainer = true },
            AccordionHeaderHeight = 48f,
            AccordionTitle = TextType.TitleSmall,
        },
    };
}
