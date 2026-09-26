# Radiant Components

`Radiant.Components` is the component catalogue, built on `Radiant.UI.Core` (elements and hooks),
`Radiant.Theming` (the theme and surface state) and `Radiant.Generators` (style facets).

## Facets

Components share props through style facets, generated onto each component by
`Radiant.Generators` (see [ui.md](ui.md#style-facets)):

| Facet | Props |
|---|---|
| `IHasBackgroundColor` | `SurfaceColor`, `SurfaceLegibility`, `SurfaceOnToggle`, `SurfaceContainerToggle`, `ContentColor`, `ContentLegibility`, `ContentOnToggle`, `ContentContainerToggle`, `ContentFocusedColor`, `ShowSurface`, `ShowError`, `ShowDisabled` |
| `IHasCornerShape` | `CornerShape` |
| `IHasElevation` | `Elevation` |
| `IHasOutline` | `ShowOutline`, `OutlineWidth`, `OutlineVariant` |
| `IHasText` | `Text`, `TextType` |
| `IHasPressable` | `OnPress` |
| `IHasLayout` | `Layout` |
| `IHasIcon` | `Icon`, `IconFilled`, `IconSize` |

A component forwards the facets each part needs with `[ForwardFacets]`. Facets set on the
component override its presets: `new SurfaceButton("Delete") { SurfaceColor = SurfaceName.Error }`
turns a filled button red.

## Components so far

| Component | What |
|---|---|
| `Surface` | Applies its background-colour props to the inherited surface state and draws the surface (colour, corners, elevation, outline); children inherit the new state |
| `PressableSurface` | A surface that presses: focus, pointer, Enter and Space; hover, focus and pressed state layers; disabled fades and stops responding; a button to assistive technology (or another `Role`, with `Selected` and `Expanded` states) |
| `SurfaceText` | Text in the surface's content colour, in a type-scale step, with optional legibility; `HeadingLevel` makes it a heading to assistive technology |
| `SurfaceButton` | Filled, tonal, outlined, text and elevated buttons: a pill-shaped `PressableSurface` with a label |
| `Card` | Elevated, filled and outlined cards |
| `SurfaceIcon` | A Material Symbols icon in the content colour (decorative: hidden from assistive technology) |
| `IconButton` | Standard, filled, tonal and outlined icon buttons, named by a required label |

| `Checkbox` | Controlled check box: checked, indeterminate, error, disabled; the tick fades in. `AccessibleLabel` names it without showing a label (as do `Radio`'s and `Switch`'s) |
| `Switch` | Controlled switch; the handle slides and grows (bigger still while pressed) |
| `Radio` | Controlled radio button; selecting calls `OnSelect`, and the dot grows in |

| `Divider` | A thin outline-variant line, across or down, optionally inset |
| `ListItem` | One-, two- or three-line rows with leading and trailing icon or text; pressable and selectable |
| `Menu` | Drops from an anchor: arrow keys and Tab move, a choice closes it, as do Escape and an outside press; icons, shortcuts, dividers, disabled items |
| `Dialog` | Modal: scrim, focus trap, Escape and scrim close (unless it must be answered), focus restored, fade and scale |
| `Tooltip` | After the pointer rests 600 ms, a small inverse-surface label beside its child |
| `Chip` | Assist, filter (tick and tonal fill when chosen) and input (trailing ×) chips, elevated or outlined |
| `Badge` | A dot or a count (99+) in the error colour on the top right of its child |
| `LinearProgress` | Determinate, or an indeterminate sliding segment |
| `CircularProgress` | A ring filling clockwise from the top, or a spinner whose arc turns as it grows and shrinks |
| `Slider` | Continuous or stepped; drag, press the track, or use arrows, Page Up/Down, Home and End |
| `Tabs` | Tabs with optional icons and an indicator that slides to the chosen tab; Left and Right choose neighbours |
| `TopAppBar` | Navigation button, title and actions; the container colour when content scrolls under it |
| `NavigationRail` | A desktop side rail: icons in pills that fill when current, labels, badges |
| `NavigationDrawer` | A sidebar: heading, sections, rows with counts, the current one in a filled pill |
| `SegmentedButton` | Joined outlined segments, single or multi choice, ticked when chosen |
| `SelectField` | A read-only field that drops a menu (as wide as itself) of options, the chosen one ticked |
| `SnackbarHost`, `Snackbars` | Brief messages at the bottom: queued, timed, with an optional action; `context.UseSnackbars().Show(…)` |
| `Avatar` | Initials on a container colour chosen stably from the name, or a person icon |
| `Skeleton` | A gently pulsing placeholder in the shape of content still loading |
| `VirtualList` | A scrolling list that builds only the rows in view, so a million rows cost a screenful; fixed row height; `ScrollToIndex` brings a row into view |
| `DataTable`, `DataColumn` | A virtualised table: sortable headers (the owner sorts), draggable column widths, a growing column, single or multi selection by press, Ctrl/⌘ and Shift, check boxes with select-all, full keyboard (arrows, pages, Home/End, Space, Enter, select all), double click to activate; controlled or not |
| `TreeView`, `TreeNode` | A virtualised tree: chevrons or a double click expand, a press selects, a double click activates leaves; Up/Down, Right to open or go in, Left to close or climb, Home/End, Enter; selection and expansion controlled or not |
| `CommandPalette` | ⌘K-style search over `Command`s (usually `context.UseCommands()`): ranked by `FuzzyMatch` on titles and keywords, grouped when nothing is typed, disabled ones left out; arrows, Enter or a press run one; Escape or outside closes |
| `CommandMenuBar` | The menu bar made from the registered commands: each command with a `Menu` is an item on it, grouped, with its shortcut, disabled and checked states. The platform's own menu bar where there is one (macOS), a drawn `MenuBar` otherwise (or nothing, with `DrawWithoutPlatformMenuBar` off) |
| `Accordion`, `AccordionItem` | Sections that open under their headers in an outlined card, one at a time or several; chevrons turn, content fades in; Up/Down/Home/End between headers; controlled or not |
| `ComboBox` | A text field that suggests options as you type, ranked by `FuzzyMatch`, with focus staying in the field; arrows, Enter or a press pick, Escape puts the text back; custom text kept only with `AllowCustom` |
| `Calendar` | A month grid: the culture's first weekday, today ringed, the chosen day filled; arrows, pages, Home/End and Enter, one Tab stop; minimum, maximum and refused days |
| `DatePicker` | A date field read in the culture's short format (errors for text that isn't a choosable date), with a calendar that opens from its button or Down, focused on the date |
| `ColorPicker` | Picks in HCT: a hue × chroma plane at the current tone (colours outside sRGB left clear), a tone strip, a swatch and a hex field; pointer and keyboard; the gallery's settings drive the theme seed with it |
| `ContextMenu` | A menu at the pointer on right-click (Control-click on macOS), or at the element with Shift+F10 or the Menu key |
| `Popover` | A floating panel of any content beside an anchor; takes focus, closes on Escape or an outside press, doesn't dim the app |
| `Sheet` | A modal panel sliding in from the end, start or bottom edge over a scrim, with a title, close button and actions |
| `AlertDialog` | A decision that must be answered: a dialog Escape and the scrim don't close, confirm in the error colour when destructive |
| `SearchField` | A compact 36 px search box: icon, text, a clear button once there's text; Escape clears, Enter submits |
| `Link` | Primary-coloured text that presses, underlined under the pointer or focus, a link to assistive technology |
| `Kbd` | Keycaps for a key or chord (`Kbd.For(KeyChord.Command(KeyCode.K))`) |
| `Breadcrumb`, `Crumb` | Links to each level above the current page; long trails fold their middle into "…" |
| `Pagination` | Previous, next, the ends, the current page's neighbourhood and gaps |
| `Alert`, `AlertKind` | An in-page message: info, success, warning or error colour and icon, title, text, actions, dismiss; announced as an alert |
| `Fab` | The floating main action: a 56 px rounded square, or extended with its label |
| `ToggleButton`, `ToggleGroup` | Icon buttons that stay on; groups of one-of (alignment) or any (bold, italic) |
| `Toolbar` | A row of controls where Left/Right/Home/End move focus, stopping at its ends |
| `SplitButton` | The usual action with a joined arrow dropping its alternatives |
| `NumberField` | A number typed in the culture's format or stepped by its arrows, Up/Down and Page Up/Down; min, max, step, decimals, suffix |
| `RangeSlider` | Two handles on one track: a press moves the nearer, each is a keyboard slider, they can't cross |
| `LineChart`, `BarChart`, `DonutChart` | Charts: lines (or areas), grouped or stacked bars, and a donut of shares; round-numbered axes (`ChartScale`), legends, a hover tooltip of every series' value, and the values as text for assistive technology |
| `DescriptionList` | Terms beside their details, divided by lines; details wrap under their terms when narrow |
| `Timeline`, `TimelineEvent` | Events down a line with markers (dots or icons), times and detail |
| `UseForm`, `Form`, `FormField`, `Validators` | Form state and checks: fields keep their text and show their first failing check once left; `Submit` shows every error and focuses the first bad field, or hands over the values |
| `StackNavigator`, `Navigator` | Drill-down pages under an app bar with a back button; pages push the next through `context.UseNavigator()`; each slides in from the side it came from; ⌘[ or Alt+Left goes back |
| `MenuBar`, `MenuBarMenu` | A window's row of menus drawn in the window: a press opens one, the pointer then moves between them, Left/Right step |
| `Carousel` | Slides in a row, a slide at a time: side buttons, dots, Left/Right; settles on the nearest slide after a wheel or trackpad scroll |
| `DropZone` | A place to drop files from the Finder, or browse with the platform's panel; filters by extension and says how many it refused |
| `TitleBar` | The app's own title bar under the platform's window controls: presses on its empty space move the window, a double click zooms it, its buttons stay buttons |
| `AvatarGroup` | Overlapping ringed avatars with the rest counted as "+N"; every name read out |
| `HoverCard` | Rich content beside a trigger after the pointer rests, staying while the pointer is on the card |
| `OtpInput` | A one-time code's boxes: typed or pasted characters fill them, non-digits ignored, called when whole |
| `GridList` | Selectable tiles in as many columns as fit: arrows move across and down, Ctrl/⌘ and Shift select several, Enter or a double click activates |
| `Splitter` | Two panes and a divider to drag between them: side by side or stacked, either pane sized, minimums for both, keyboard steps, double click to restore; controlled or not |
| `DockPanel`, `DockLayout`, `DockGroup`, `DockItem`, `DockArea` | IDE-style docking: panels as tabs on the left, right, bottom and centre, with dividers between. Drag a tab to another area (the landing area is shown, and near an edge it docks on that side even where nothing is yet); each tab's context menu moves it or closes it. Controlled by an immutable `DockLayout` (`Move`, `Close`, `Activate`, `Resize`), so the layout can be saved |
| `DocumentTabs` | An editor's open documents: the chosen tab joins the page below, close buttons on the chosen and hovered tabs, a dot for unsaved changes, middle click to close, sideways scrolling |
| `StatusBar`, `StatusItem` | The thin bar along a window's bottom, with small text-and-icon items at each end, pressable when they do something |
| `TextField` | Filled and outlined fields (a `Trailing` element can replace the trailing icon): the label floats up and shrinks on focus or text (cutting the outline); primary or error indicator; supporting text, error, character count; leading and trailing icons (the trailing one pressable); controlled or uncontrolled |

Buttons take a leading icon (`Icon = "add"`). The selection controls are *controlled*, like
React's: they show the value they're given and report presses with the value they should become.
The whole row, label included, presses and takes focus, and the state layer is a 40 px circle
round the indicator.

## Icons

Icons are Material Symbols Rounded (Apache-2.0), drawn as text: an icon's name, set in
`FontLibrary.Icons`, becomes the icon through the font's ligatures. FILL, weight and optical size
are variable (`TextStyle.Variations`).
- **Embedded set:** Radiant embeds 272 common icons (408 KB). The list is `tools/icons/icons.txt`;
  `tools/icons/subset.sh` rebuilds the font from a pinned upstream commit.
- **All icons:** register the full font under `FontLibrary.Icons` for the rest.

## Primitives (`Radiant.Components.Primitives`)

These are the headless building blocks of overlays, like Radix's:

| Primitive | What |
|---|---|
| `DismissableLayer` | Calls `OnDismiss` on a press outside it (content it shows through a portal counts as inside) or Escape inside it |
| `FocusScope` | Moves focus in when shown, keeps Tab inside (`Box.TrapFocus`), gives focus back when it goes |
| `Anchored` | Floating content in a portal, placed against an `ElementRef`: side, alignment, offset, flip, shift, match width. It follows the anchor as it moves and stays hidden until placed |
| `AnchoredPlacement` | The placement maths on its own |

They rest on UI core hooks: `UIRoot.ObservePointerDown`, `SaveFocus`/`FocusSnapshot.Restore`,
`FocusFirst` and `Box.TrapFocus`.

## Colours are opaque

Legibility (text at 0.87, secondary text at 0.6) and state layers (hover at 0.08) are *mixed
into the surface colour in sRGB* by `ResolvedTheme.ContentColor` and `StateLayerColor`. They are
not drawn translucent. Material's opacities were designed for sRGB blending; the same opacity
blended in linear light makes 87% text look about 40% lighter. The surface state always knows
the colour underneath, so the result can be opaque, which is also better for text rendering.

## The gallery

`Radiant.Gallery` is a `SidebarLayout` app (see [templates.md](templates.md)). Its first page is
the component catalogue, with a button that shuffles the theme (seed, variant, light or dark,
corner scale) and animates everything to it; the other pages are the templates.

```
dotnet run --project src/Radiant.Gallery                                            # in a window
dotnet run --project src/Radiant.Gallery -- --snapshot out.png --dark --scale 2     # to a PNG
dotnet run --project src/Radiant.Gallery -- --snapshot out.png --page 6 --height 900  # one page
dotnet run -c Release --project src/Radiant.Gallery -- --snapshot out.png --page 0 --bench 120  # frame timings
```

Pages: 0 components, 1 dashboard, 2 settings, 3 sign in, 4 empty state, 5 table, 6 landing page,
7 store, 8 workspace, 9 mail, 10 new project (wizard), 11 preferences.
