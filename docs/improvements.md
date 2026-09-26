# Improvements and known issues

This is a running list of workarounds, shortcuts and rough edges found while building the
platform. Add to it whenever something is done the second-best way, and remove an entry when it
is fixed. The newest entries in each section come first.

Each entry says what's wrong, why it's that way now, and what would be better.

## UI core (`Radiant.UI.Core`)

- **`Grid` lays out twice when its width changes.** Yoga has no grid, so the column width is
  worked out after layout and applied to the children, then the root lays out again (up to four
  passes, for nested grids). A custom layout node, or Yoga gaining grid, would do it in one. There
  are no column spans, explicit rows or per-cell alignment yet.
- **Physical positions need converting in right-to-left layouts.** `Edges` are logical only, so a
  box placed at a pointer position or measured bounds has to go through `Edges.Physical`, which
  needs the direction passed in. `Left`/`Right` insets in `LayoutStyle` (Yoga has them) would let a
  box say "from the left" directly. A portal also looks up its direction on each update by walking
  the element tree; a direction change above a portal whose own props didn't change doesn't reach
  it until it updates.
- **A single-line field's scroll is worked out as it's painted.** The right-to-left alignment of a
  field's text and its horizontal scroll both come from `EditableTextRenderNode.Paint`, so a press
  before the first paint maps to the wrong place, and tests can't check it without a GPU. Working
  it out after layout would fix both.
- **Latin text in a right-to-left UI moves its punctuation.** "+31%" shows as "31%+" and a
  sentence's full stop moves to its left end: that's the bidi algorithm doing its job (browsers do
  the same), but the gallery, written in English, looks odd with `--rtl`. Arabic or Hebrew sample
  text would show it properly.
- **Layered facets are found by convention.** A facet type with a `T Merge(T over)` method is
  merged over the target's value by the forwarders instead of replacing it. It's implicit; an
  attribute on the facet property (`[Layered]`) would say so where it's declared. Components that
  don't forward `IHasLayout` still merge by hand (`SurfaceIcon`, `TextField`).
- **Image textures are never freed.** `ImageSource` keeps one texture per renderer in a weak table,
  but nothing disposes the GPU texture when the source goes. Pictures are also decoded
  synchronously on the UI thread, and there are no mipmaps, so heavy downscaling aliases. Add
  async decoding, an image cache with release, and mipmaps.
- **Tests still reach into internals.** `UINode`, selectors and `Radiant.UI.Driver` are now the
  public way to find elements and read their bounds, visibility and state
  ([automation.md](automation.md)), but the existing component tests still read the render tree
  through `InternalsVisibleTo`, and `TemplateHarness` still walks `GetSemantics()` by hand. Move them
  to the driver as they're touched.
- **Children lists compare by reference.** A host element whose parent rebuilds always updates,
  even when its children are equal element for element. A structural list comparison, or a
  generated one, would let unchanged subtrees skip.
- **Animations redraw everything.** `RadiantUI.Run` draws only while `UIRoot.NeedsUpdate` (an
  idle window waits for input and uses no CPU), but anything animating, a spinner included,
  rebuilds its component and redraws the whole window every frame. Nodes out of view are no longer
  painted, and `Radiant.Gallery --bench N` times frames: in Release the components page takes about
  0.3 ms to update and 0.35 ms to paint, yet a window of it on a 120 Hz display still uses about 25%
  of a core, mostly in the frame loop, wgpu and the driver. Paint-time animation (no rebuild),
  cached display lists for still subtrees, and presenting only damaged rects (P11) would make a
  spinner cost a spinner.
- **Culling trusts a margin.** A node is skipped when its bounds widened by 32 px miss the visible
  area; a descendant drawn further outside its ancestor (an absolutely placed child far away)
  can disappear with it. Portals are separate, so menus and tooltips are safe.
- **Idle waiting is only in `RadiantUI.Run`.** The tab host (`Radiant.Host`) still draws every
  frame; `RadiantApplication.NeedsFrame` is there for it to use.
- **Effect order is approximate.** Effects run deepest first. That runs children before parents,
  but isn't React's strict post-order across sibling subtrees.
- **Text sizes round up to whole pixels.** Rounding up keeps text from wrapping earlier when it's
  laid out at its measured width. 1/64 px would be closer.
- **Styles reset in full.** A changed `LayoutStyle` resets every Yoga property and re-applies it;
  diffing would touch fewer.
- **Yoga isn't CSS by default.** Its default `flexShrink` is 0 (CSS: 1), so scroll areas force
  shrink 1. Consider Yoga's web-defaults config for every node, so layout matches CSS
  expectations everywhere.
- **Percentage minimums inside nested scroll areas.** Yoga.Net resolves them against the outer
  area. `ScrollArea` fills its viewport with `flexGrow` instead. Worth a minimal repro upstream.
- **Two kinds of key.** `Key` is an element's identity, so keyboard keys are `KeyCode`, whose
  values equal Silk's. `RadiantApplication` and `InputState` still expose Silk's `Key` and
  `MouseButton`; the P7 platform layer should expose only Radiant types.
- **Wheel units are a guess.** GLFW reports notches, converted at `UIAppOptions.WheelStep` (40 px).
  Trackpads send precise deltas that this over-scales. Fix with the P7 platform input.
- **No key repeat.** Input has no key-repeat flag. Typing without a text input client still
  arrives from GLFW one `char` at a time as `TextInput` events; with a client, the platform
  delivers whole strings and compositions (see [platform.md](platform.md)).
- **`ScrollArea` is basic.** It has no keyboard scrolling, no fading indicators (`Auto` behaves
  like `Always`), and the thumb doesn't widen under the pointer, so the 12 px grip is invisible
  until used.
- **`ElementRef.Bounds` ignores transforms.** It gives the untransformed rectangle.
  `RenderNode.RootBounds()` (what `UINode.Bounds` uses) now follows them, and could replace it.
- **VoiceOver gets a first cut.** The whole tree is converted on every read (fine for hundreds of
  nodes, not tens of thousands; a virtual table's rows are only those built), text fields can't be
  typed into or have their text and selection read through accessibility (`AXSelectedText`,
  `setAccessibilityValue:`), sliders can't be stepped (`accessibilityPerformIncrement`), tables and
  trees don't give rows and columns their structure (`AXRowCount`, `AXDisclosureLevel`), and
  there are no announcements (snackbars, alerts appearing) or live regions.
- **Semantics act only by press, focus and scrolling.** `UIRoot.Press`, `FocusNode`,
  `ScrollIntoView` and `ScrollTo` act on a node by id; there's no increment or decrement (sliders),
  setting a value (text fields), or custom actions yet, and VoiceOver doesn't use the scrolling.
- **Three copies of the Objective-C interop.** `Radiant.Platform.MacOS.ObjC` is the full one;
  `Radiant.Host.MacObjc` and a private copy in `RadiantApplication` predate it. They were left
  alone so P7 didn't disturb the host. Point both at one shared interop, either
  `Radiant.Platform.MacOS` or a small interop assembly that both it and `Radiant` reference.
- **A title bar drag hides the release.** `performWindowDragWithEvent:` runs AppKit's own loop
  until the button comes up, so GLFW never sees the release and the UI still thinks the press is
  held (its pressed path lingers until the next press). Sending the UI a release when
  `BeginDrag` returns would tidy it.
- **Chrome is macOS only.** Windows' caption buttons (`TrailingInset`), snap layouts on hover
  and dragging through `WM_NCHITTEST` need the Windows platform.
- **Drops arrive without a drag.** GLFW's drop callback gives only the dropped paths, so a drop
  zone can't highlight while files hover over it or refuse a drop it won't take, and nothing can
  be dragged out. Registering the content view for dragging (`NSDraggingDestination` on macOS,
  as the input methods are wired) would give enter, move and leave, and the drag's types.
- **The platform is opt-in.** `UIAppOptions.Platform` defaults to headless because
  `Radiant.UI.Core` mustn't reference an implementation, so an app that forgets it gets no
  clipboard, cursors or input methods. A desktop package that references every implementation
  and picks one by operating system would make it the default.
- **GLFW's methods are replaced class-wide.** The text input methods are swapped on
  `GLFWContentView` itself, which every GLFW window shares. Views without a Radiant input pass
  through to GLFW, but it's still a global change. A per-window subclass (`object_setClass` to
  a Radiant subclass of GLFW's view) would confine it to the windows that asked.
- **The input method sees only the composition.** `ITextInputClient` doesn't expose the
  client's text or selection, so the marked range starts at 0, `attributedSubstringForProposedRange:`
  returns nil, and `replacementRange` is ignored. Reconversion and context-aware prediction need
  the text field to expose its text, selection and composition range, and a rectangle for any
  range.
- **Every key is held back while composing.** On macOS, `keyDown:` skips GLFW for any key
  during a composition, shortcuts included, because the input method may use any of them. Asking
  the input context whether it handled the event (`[[view inputContext] handleEvent:]`) and
  passing on only what it didn't would be finer.
- **Clicks don't reach the input method.** GLFW's `mouseDown:` doesn't offer events to
  `[[self inputContext] handleEvent:]`, so clicking inside a composition doesn't move within it
  or pick a clause.
- **Marked text loses its styling.** The input method's attributes (which clause is being
  converted, thick and thin underlines) are dropped; the client only gets the selection.
- **There's no UI synchronization context.** Dialog tasks complete on the main thread, and an
  `await` without a synchronization context continues inline there, which is the UI thread only
  because of that. A `SynchronizationContext` that posts to the frame loop would make every
  `await` in UI code safe.
- **Dialogs without a window block.** A platform created with no window runs panels app-modally
  (`runModal`), which stops the frame loop until the user answers.
- **Panels have no filter menu.** macOS allows every filter's files at once. An accessory view
  with a pop-up of the filters, as other apps have, would let the user choose.
- **Some cursors are missing.** Diagonal resize cursors need macOS 15's
  `frameResizeCursorFromPosition:inDirections:` (or private cursors before it), and there's no
  way to hide the cursor (`[NSCursor hide]` counts must balance).
- **The self-test ends by exiting the process.** Nothing in the UI can end
  `RadiantUI.Run`, so `PlatformCheck --selftest` calls `Environment.Exit`. `UIRoot` or the
  platform should be able to close the window.
- **`Radiant.Platform` shadows ColorSystem's `Platform` enum.** Inside `Radiant.*` namespaces,
  `Platform` now means the namespace, so theming code writes `ColorSystem.Platform.Phone`.
  Renaming the enum (to `DevicePlatform`, say) would avoid the surprise.

## Components (`Radiant.Components`)

- **A `SegmentedButton` needs a width from its parent (in the tonal look).** Its joined segments
  share the width equally (flex basis 0), so in a row that doesn't give it one they shrink to
  48 px and their labels truncate. Segmented ones (the `Hairline` look) size to their labels. It
  could size to its widest segment times the count when nothing constrains it.
- **Hugging content takes an extra box.** `Tag` and `Badge` sit in a plain box of their own and
  set `AlignSelf = FlexStart` inside it, so they keep to their content across a column yet line
  up with a row's alignment. A "hug" alignment in the layout would do it without the box.
- **Goldens don't cover everything.** Components, the templates' blocks (two widths, light, dark
  and compact) and a few scheme variants have goldens. Page-sized templates (the gallery's pages),
  the drawer, the navigation bar, pickers opened as popovers and animation mid-way don't, and
  there's no third width. About 2.8 MB of PNGs so far.
- **A missing golden passes.** `Golden.AssertMatches` writes a golden that isn't there and passes,
  so a forgotten `git add` goes unnoticed; a switch that fails instead (for a pre-commit run)
  would catch it.
- **Two GPU test helpers.** `Radiant.Tests`' `GpuFrame` (linked into the UI tests) predates
  `Radiant.Testing.GpuCanvas` and does the same with BGRA bytes; the renderer's tests could move
  to the canvas, and `GoldenImageHelper` to `Golden`.
- **Absolutely placed boxes are held to their container's width.** Yoga gives an absolute child
  with one horizontal inset its container's width as the most it can be, and a non-wrapping
  `TextBlock` then overflows the box it's in. `Badge` measured "99+" and sized its pill to fit;
  a `TextBlock` with `Wrap = false` that always measured its full width (as CSS `nowrap` does)
  would fix it everywhere.
- **The time picker is typed, not picked.** Its parts step and take digits, as macOS's do, but
  there's no clock face or list of times to choose from, no seconds, and no time zone.
- **Property grids take any control.** A `PropertyItem`'s editor is whatever the app gives it, so
  there are no built-in editors by type (a colour swatch, an enum menu), no multi-object editing
  (showing "mixed"), and no reset-to-default.
- **Presses don't animate fully.** State layers fade in and out (`UseTransition`), but there's no
  press scale and no ripple.
- **Sliders are single-valued.** No range slider, no value label while dragging, and no tick marks
  for stepped sliders.
- **Tabs are primary tabs only.** The indicator spans the whole tab rather than its content,
  secondary tabs are missing, and many tabs don't scroll.
- **Menus are basic.** No submenus, no type-to-select, no check or radio items.
- **Tooltips add a box.** `Tooltip` wraps its child in a `Box`, which can change the layout of a
  child that relied on its parent's flex settings. Nothing announces the tip to assistive
  technology yet.
- **Dialogs have one form.** There's no full-screen variant, and no scrolling body for long
  content.
- **Only 285 icons are embedded.** They're curated in `tools/icons/icons.txt`, to keep the font
  at 417 KB rather than 15 MB. An app wanting icons outside the list must register the full font
  itself; a build-time subset of the icons an app actually uses would be better.
- **Composited colours can be slightly off.** A faded surface (a disabled container) is mixed into
  the window background, not whatever is actually behind it.
- **Tables are a first cut.** No horizontal scrolling when the columns are wider than the table,
  no column reordering or hiding, no sticky first column, no cell editing, and cells aren't
  exposed to assistive technology (a row is named by its cells' text joined). The keyboard moves
  by row only; spreadsheet-style cell focus isn't there. Select-all builds a set of every index,
  which is fine at 100k rows but wants a range representation beyond that.
- **The colour picker's images are cached for good.** A plane per tone step (51) and a tone
  strip per 3° of hue and 3 of chroma are made once and kept, because image textures aren't
  freed yet. There's no alpha, no eyedropper, no saved swatches, and no contrast readout against
  a chosen background.
- **Dates are single and Gregorian.** No range picking (start and end in one calendar), no
  time picker, no month or year view to jump far (only a month at a time), and only the
  Gregorian calendar (`DateOnly`), though names and the first weekday follow the culture.
  Typed dates use the culture's short pattern; "3 Oct" or "next Friday" aren't understood.
- **Combo boxes are single-choice and synchronous.** No multiple selection (chips in the field),
  no options loaded as you type (a loading row, debouncing), no grouped options, and the list
  isn't virtualised, so thousands of options would build every row.
- **Opening sections don't grow.** An accordion's content fades in at full height; sliding it
  open needs the content's measured height and a height transition, which `Presence` doesn't
  offer yet.
- **Command palettes show matches plainly.** The matched letters aren't highlighted (text spans
  in `SurfaceText` would do it), and there's no ordering by recent use.
- **Greyed menu-bar items hide their shortcut.** AppKit takes a key equivalent's key even when its
  item is disabled, so a greyed Select All would take ⌘A from a focused table. Disabled items get
  no key equivalent, and so show none. Letting the window answer first (`performKeyEquivalent:`
  on the content view, asking the UI whether it handled the key) would let them keep it.
- **Only text inputs offer Edit commands.** Tables, lists and grids handle ⌘A and ⌘C in their own
  key handlers, but don't register focus-scoped Select All or Copy, so the Edit menu stays greyed
  while they have focus.
- **Menu items are named by all their text.** A drawn menu item's accessible name includes its
  shortcut ("Save ⌘S"); the shortcut would be better as a description or the key-shortcuts
  property.
- **Overlay primitives add boxes.** `DismissableLayer` and `FocusScope` each wrap their content in
  a box that sizes to it, so a panel inside can't simply fill or be a percentage of its parent.
  Both now take a `Layout` for their box (a side sheet has its layer stretch and its scope grow),
  but every overlay has to know about them. Layout-transparent host elements (a box that passes
  its children straight to its parent's flex layout, like CSS `display: contents`) would remove
  the problem.
- **Platform menus are plain.** Native menus show titles, separators, enabled and checked states,
  and the menu bar gives shortcuts as key equivalents, but there are no icons, no submenus, and a
  context menu shows no shortcuts. `Radiant.Host`'s `MacMainMenu` still builds its own File menu;
  a hosted app using `CommandMenuBar` would replace it.
- **Docked panels start again when they move.** A panel dragged to another area is mounted anew
  there, losing its state (scroll position, a text field's contents), and so is everything when
  an area appears or empties (the splitters around the centre are nested differently). Moving an
  element between parents while keeping its state (a keyed reparent, or panels rendered once and
  placed through portals) would fix both. There's also no reordering within a tab strip, no
  floating panels, and a strip with too many tabs overflows instead of scrolling.
- **The icon subset is hand-kept.** Icons outside `tools/icons/icons.txt` show as their names, and
  nothing warns: `navigate_next` and `navigate_before` are only aliases in Material Symbols, so
  they can't be added and don't mirror with the embedded font. A debug-build check that an icon
  name has a glyph, or an analyzer over string literals given to `SurfaceIcon`, would catch it.
- **Toolbars aren't a single Tab stop.** Arrows move within a toolbar, but Tab still visits every
  control in it; a roving tab index (only the last-focused control tabbable) needs `TabIndex` on
  `IconButton` and the other controls, which only `PressableSurface` and `ToggleButton` have.
- **Charts are a first cut.** Line areas are filled with plain quads (hard edges under the line,
  fine at the axis), series colours come from theme roles that can sit close together (primary and
  tertiary under some seeds), there's no zooming, panning, time axis, logarithmic scale or
  keyboard way to read values (only the pointer and the text summary), and a chart repaints its
  plot every frame it's drawn rather than caching it.
- **Navigation keeps only the page showing.** A `StackNavigator` rebuilds a page when it's
  revealed again, so its state (scroll position, typed text) is lost on the way back, and the
  page going away doesn't animate out. Keeping pages mounted but hidden, or saving their state by
  key, would fix both; deep links and history (forward) aren't there either.
- **Trees are a first cut.** No type-ahead (a letter jumps to the next item starting with it), no
  multiple selection, no drag and drop, no lazily loaded children (a "loading" row while an
  item fetches its children), and `*` doesn't expand siblings. The rows are re-flattened on every
  build, which is linear in the open items.
- **Column resize grips sit inside their own cell.** The 6 px grip is at the cell's right edge
  rather than straddling the boundary, for the same tree-order reason as the splitter's handle.
- **`UseState<T?>(null)` is ambiguous.** `null` fits both the value and the factory overload, so
  a nullable state needs a cast (`UseState(((int, int)?)null)`). A distinct name for the factory
  form (`UseLazyState`) would avoid it.
- **Virtual lists need a fixed row height.** Variable heights want measured rows and an
  estimated-height index (a Fenwick tree of heights). Past 2^24 px (560k rows of 30 px) the scroll
  offset is a float and moves in 2 px steps; rows stay aligned, because whole-pixel tops and
  their differences are exact. The scroll thumb can be dragged through the whole list.
- **The range lags a frame on resize.** `ScrollController.ExtentsChanged` is raised during
  layout, so a list that grows builds its new rows on the next frame.
- **Splitter handles overlap panes by tree order.** The divider's grip reaches 4 px into both
  panes; it's placed inside the second pane's wrapper so it's later in the tree and above both.
  That's why the second pane clips through an inner box. A z-index or an overlay layer for
  handles would be cleaner, and would let a splitter collapse a pane (snap shut below its
  minimum), which it can't yet.
- **Document tabs are basic.** No drag to reorder or to another group, the chosen tab isn't
  scrolled into view, the vertical wheel doesn't scroll the strip, there's no overflow menu, and
  the close button isn't a tab stop (closing from the keyboard wants a command, Ctrl+W).
- **Icon buttons in fields can be named by their icon.** `TextField`'s trailing button takes its
  name from `TrailingIconLabel`, but falls back to the icon's name ("visibility") when that's
  unset. Require a label when there's an `OnTrailingIconPress`, or analyse for it.
- **Button heights are fixed.** They are 40 px plus density; Material 3's newer button sizes
  (XS–XL) aren't modelled.
- **`Anchored` checks its anchor every frame.** It runs a ticker while shown, which keeps frames
  coming. Layout-change notifications from the render tree would avoid the polling.
- **Unplaced content can still be clicked.** Before its first placement, `Anchored` content is
  invisible (opacity 0) but its children can still be hit for that one frame.
- **Escape only dismisses from inside.** `DismissableLayer` sees Escape only when focus is within
  it; Radix listens on the document. Add a root key observer if layers without focus need it.

## Templates (`Radiant.Templates`)

- **`WorkspaceLayout` doesn't dock.** Its parts are fixed in place: they can't be dragged to other
  docks, the panel can't be maximised or closed from its header, and editors can't be split into
  groups. `DockPanel` does the docking; the template could be rebuilt on it.
- **The gallery frames shells by hand.** A shell previewed in a frame is inset by the outline's
  width, but its corners still cross the frame's rounded outline; clipping children to the shape
  inside the border would fix it generally.
- **Forms take text fields only.** `Form` fields are text with string checks; check boxes, selects,
  dates and numbers keep their own state beside the form, and there are no checks across fields
  (a password confirmation), no checks that wait on a server, and no summary of errors.
- **The password field is masked by the form.** `TextField` has no password mode, so
  `SignInForm` shows a dot per character and maps edits on the dots back onto the real text. That
  works for typing and deleting, but copy would copy dots, the platform isn't told it's secure
  input (macOS secure event input, no input methods), and every form must repeat it. Add
  `TextField.Obscured` (and `TextInput` support) and drop the mapping.
- **Formatting is fixed to invariant culture.** Stats format their change as `+12.4%`, and the
  cart as `$` plus two decimals. Apps need culture-aware number and currency formatting; take an
  `IFormatProvider` or a formatter delegate.
- **`SidebarLayout` doesn't adapt.** It's always a 260 px drawer; narrow windows want a rail or a
  modal drawer, and the sidebar isn't resizable or collapsible.
- **Blocks aren't stateful about their data.** `CartSummary` reports quantity changes but
  doesn't apply them, and `SignInForm` has no busy state while signing in. That's intended (the
  app owns the data) but each app writes the same glue; small controller types would help.

## Editing (`TextInput`)

- **Input methods need checking by hand.** `TextInput` is the platform's text input client while
  focused, so macOS input methods compose in place. That's verified only through the headless
  platform and the check program's manual steps (`docs/platform.md`), not in a live gallery field.
- **Every keystroke reshapes the whole text.** That's fine for fields; a code editor wants
  incremental layout per line.
- **Dragging doesn't scroll.** Neither a one-line input dragged past its end nor a multiline one
  dragged past its bottom scrolls.
- **Vertical movement bypasses history.** Up and Down call `OnChange` directly, not through the
  change helper that clears the goal column, which is intended but not obvious; a shared "move"
  path would read better.
- **No context menu, spell check or drag-and-drop of text.**

## Theming (`Radiant.Theming`)

- **Hand-picked palettes only raise contrast roughly.** For `ColorRoles`, a raised contrast level
  moves quiet text and borders towards stronger ones, but accents and containers stay as picked
  and a reduced level does nothing. A palette could carry its own high-contrast `ColorRoles`. The
  seed and variant don't touch a palette either, so the gallery hides its colour picker there;
  deriving a palette's primary family from the seed would let the user pick an accent while
  keeping the palette's neutrals.
- **A few desktop components share one look.** `DocumentTabs` (a strip with the chosen tab's
  accent line), `StatusBar`, and `DataTable`'s header and row heights draw the same in every
  preset: they're neutral already, but a theme can't size or recolour them beyond the colour
  roles. Their chosen rows do follow `ListStyle.Selected`.
- **Fine icons are an approximation.** Hairline draws Material Symbols at weight 300 and 20 px,
  close to a thin outline set but not the same drawing. Another icon font would need a map from
  names to its characters, since icons are drawn by ligature name.
- **Component styles are theme-wide.** A `SurfaceLook` or a `TabsLook` applies to every instance;
  there's no way to give one screen's tabs a different structure than another's except by
  providing a different theme below it. A per-instance override (a `Look` prop) would be the
  next step if apps need it. Templates in the full sense (a style supplying its own build
  function) aren't exposed.
- **Segmented trays and pills are worked out, not roles.** The tray is the surface tinted 6%
  towards its content and the pill white (light) or tinted 14% (dark), so they read on any
  surface. Named roles would let a palette choose them.
- **A preset switch jumps its layout.** Type, density and button padding switch at the start of an
  animated theme change (colours and corners animate), so text reflows once. Fine for a settings
  choice, but a switch that animates layout would need measuring both ends.

- **A theme change rebuilds every reader.** Components reading the theme rebuild on every change,
  and on every frame of a transition. The expensive part was text: colour lived in `TextStyle`,
  so a recolour re-shaped and re-measured every paragraph. Plain text now takes its colour at
  paint, which brought a theme change on a 158-node page from 9.7 ms to 1.2 ms (Release).
  Measured with `--bench N --bench-theme` (the theme animating between light and dark for every
  frame), a transition on the heaviest gallery page updates in a median 0.65 ms, 99th percentile
  1.8 ms and slowest 3.5 ms under Native AOT: comfortably inside 120 fps (8.3 ms), so the plan's
  zero-rebuild design (render nodes holding colour and shape roles resolved at paint, with a
  paint-time theme scope for a dark section in a light app) isn't needed for speed. It would
  still cut the 2 MB a frame a transition allocates. Under the JIT, the first seconds of
  transitions after launch run unoptimised code and reach 5–8 ms; ReadyToRun (or AOT) removes
  that warm-up.
- **Schemes use the Phone platform.** Material's phone and watch are the only platforms upstream;
  check whether a desktop tuning is wanted.
- **The type scale is sized for phones.** Material 3's body text is 14 px, larger than typical
  desktop UI (13 px on macOS). Consider a desktop `TypeScale`, or density scaling type.
- **Some role mappings are judgement calls.** Containers use `SurfaceContainer`, and `Inverse`'s
  container is `inversePrimary` with `inverseSurface` content. Revisit when components use them.

## Generators (`Radiant.Generators`)

- **Props equality still compares callbacks.** Delegate-ignoring equality isn't generated, because
  skipping a rebuild when only callbacks differ would leave the old callbacks captured in the
  subtree built from the old props. It needs a way to read the latest props (a stable callback
  wrapper or a `UseLatest`) first.
- **No hook-order analyzer yet.** RAD001, for hooks called conditionally, doesn't exist; the
  runtime throws when hook order changes.
- **Forwarders are private.** A derived component can't call them. Records are usually sealed, but
  an option for `protected` may be wanted.
- **Some planned generation is missing.** Snapshot structs, `[Lerpable]`, story knob metadata and
  AOT factories aren't generated yet. (Nothing needs AOT factories so far: the gallery publishes
  and runs under Native AOT without them.)
- **Generation runs whole.** The generator collects every candidate record and dedupes by name,
  which re-runs output for every record on any change. Fine at this scale; key the pipeline by
  symbol if large projects feel slow.

## Text (`Radiant.Text`)

- **Layout speed.** 10k characters lay out in ~6 ms (Release); shaping alone is ~2 ms.
  - Placing glyphs searches graphemes per cluster.
  - Splitting into runs allocates per grapheme.
  - Each bidi paragraph copies its text.
- **Tabs are fixed width** (`TabSize` spaces); real tab stops are needed.
- **No justification or hyphenation.**
- **Selecting a line break shows nothing.** Editors usually show a sliver for the newline and for
  empty lines.
- **An empty last line takes the previous paragraph's direction.**
- **Every size is a new font instance.** An `opsz` instance per distinct size means animated
  sizes create many; quantise `opsz`.
- **Word and grapheme `Previous`/`Next` are O(n) per call.** `Paragraph` caches the boundary
  arrays; other callers should too.
- **General_Category is partial.** Its table keeps only the values line breaking needs. Generate
  it in full when another algorithm needs more.
- **One caret case is untested.** Carets inside a right-to-left ligature: no test font merges
  right-to-left clusters.
- **HarfBuzzSharp lacks `Font.MakeImmutable`**, so it's P/Invoked. (Its `Blob.FromStream` kept
  managed memory a compacting collector could move; fonts are copied to native memory instead.)

## Animation

- **Transitions rebuild each frame.** `UseTransition` rebuilds its component every frame while it
  moves. That's fine for small components; large animated subtrees want paint-time animated
  properties (opacity or transform on the render node) that skip the rebuild.
- **No springs yet.** Transitions are duration and easing only. Material 3's newer motion is
  spring-based; add a spring driver (Radiant has `SmoothDamp`, and `Decay` for momentum).

## Rendering (`Radiant.Graphics2D`)

- **Edge correction guesses the ground.** Shape edges blend as in sRGB against a border's own
  opaque fill, or else against white (dark edges) or black (light ones). A shape whose real
  ground differs (a mid-grey outline round a clear fill on a mid-grey page) is corrected against
  the wrong ground. Reading the destination (framebuffer fetch or a copy) would make it exact.
- **MSDF text has no gamma correction.** Its edges aren't gamma-corrected as coverage and Slug
  text are; at the sizes it's used for that barely shows.
- **MSDF can't resolve very thin strokes.** At the 40 px generation em it can't resolve strokes
  under about 2 texels, which shows only at Inter weight 100. Generate the thinnest weights at a
  larger em.
- **The MSDF atlas-trim test uses small pages.** It sets internal 128-texel pages, because filling
  real pages under coverage instrumentation took minutes.
- **Slug is costly and soft when small.** It isn't pixel-snapped, so it's a little softer than
  coverage at small sizes. It also costs 3–4× coverage on the GPU: ~3.7 ms for a full 1080p
  screen of 14 px text on an M4 Pro, against ~0.8 ms.
  - Features under 2 px (a period at 11 px) are its weak spot, because it takes one ray each way
    rather than the pixel's area.
  - The paper's band splitting and corner clipping aren't implemented.
- **Slug's cache is per font instance.** Inter's optical-size axis gives each size from 14 to 32 px
  its own instance, so its own curve data. Share curve data across `opsz`, or quantise.
- **One Slug GPU test depends on a chosen offset** (`-10.37f`). An earlier value put a crossbar edge
  exactly on a pixel boundary; a different font version could trip it again.
- **The Slug patent dedication isn't on Google Patents yet.** It dedicates US 10,373,352 to the
  public domain from 2026-03-17, per Lengyel's post
  (https://terathon.com/blog/decade-slug.html), but Google Patents still shows the patent as
  active. Recheck later.
- **The coverage atlas empties itself.** It clears in full past four pages rather than evicting the
  least-recently-used glyphs. Each glyph is also uploaded on its own; batch the uploads per frame.
- **Text gamma is a heuristic tuned by eye.** It mixes by the text's luminance. Tune it against
  native reference crops at 11–15 px, 1× and 2×; this needs screen capture or a reference
  renderer.
- **Rotated coverage text is soft.** Glyphs under rotation are unsnapped. Use `TextRendering.Hybrid`
  once runtime MSDF lands.
- **Nested transforms cost more.** Transforms apply to vertices on the CPU at `PopTransform`, so
  nesting is O(depth × vertices), and glyph snapping composes the stack per run. A per-draw
  transform (instancing, or a uniform per batch) would fix both.
- **Clipping and layers are limited.**
  - Only the innermost rounded clip applies.
  - Windows have no MSAA.
  - There is no backdrop blur.
  - Opacity layers need the frame begun with its attachment size.

## Assets and licences

- **Source Serif 4 adds about 1.1 MB.** It's embedded (Latin only, upright and italic, built by
  `tools/fonts/serif.sh`) for the `Linen` preset's headings, so every app carries it. A way to
  register optional fonts only when a theme needs them would keep it out of apps that don't.
- **SixLabors.ImageSharp** (Split License) still decodes PNGs. Consider a permissively licensed
  decoder; ask first.
- **`MsdfBaker` is due for replacement.**
  - It still uses SixLabors.Fonts 2.0.7.
  - Its MSDF core fixes signs with an even-odd test that flips individual channels, which breaks
    corners and overlapping contours.
  - Replace it with the runtime MSDF generator and HarfBuzz outlines once they land.

## Automation (`Radiant.UI.Automation`, `radiant-agent`)

- **Screenshots are fresh drawings.** They repaint the tree on a second GPU device, so native window
  chrome, menus, input method windows and anything drawn outside the tree are missing, and the first
  one pays for its own glyph atlases. Capturing the window (ScreenCaptureKit) needs the Screen
  Recording permission; worth it for checking chrome.
- **Selectors rebuild the semantics tree every frame they wait.** Fine for thousands of nodes; a
  virtual table with tens of thousands would want the tree kept and patched between frames.
- **Node ids aren't stable across runs.** `#412` is a process-wide counter: good for the life of a
  node, useless in a saved test. Tests should use test IDs or roles and labels.
- **No `state` entries for dialogs.** The log records focus moving; dialogs opening and closing, and
  alerts appearing, would make a person's session easier to read back.
- **Hovering is off by default** (`RADIANT_AGENT_LOG_HOVER=1`), and a person's drag is one entry
  from where it started to where it ended, with nothing in between.
- **Modal loops stall commands.** While macOS runs its own loop (a live window resize, a native menu
  open, a native dialog), no frames run, so commands wait and then time out. Headless apps use the
  headless file dialogs, which can be scripted.
- **A never-ending `Animation` ticker spins a core** in a headless app on the fixed clock, which runs
  frames as fast as it can while the UI isn't idle. Components should say `Continuous`; an app's own
  can be found with `app.idle`'s busy reasons.
- **Windows.** The socket works on Windows 10 1803 and later, but its permissions aren't set (Unix
  file modes), and `AgentLauncher` doesn't send the app's output to a file there. File-drop works
  everywhere.
- **The host's `tab.*` actions are on files only.** `LiveHost` pumps a dispatcher now, but doesn't
  serve the socket or write a log.
- **Test IDs aren't keyed.** Repeated parts (rows, items, days) share one ID and are told apart by
  position or text. A keyed form (`Row(key)`, giving `"DataTable.Row:42"`) would let a test name one
  row whatever it shows.
- **The locator generator isn't packaged.** A test project references `Radiant.Generators` as an
  analyzer by hand; shipped as a NuGet package, `Radiant.UI.Driver` should carry it in its
  `analyzers` folder.
- **A component that builds several elements in the flow has no scope.** A popup alongside a control
  doesn't count (a portal, or nothing while closed), but a component that lays out two things side by
  side has no single root: its parts are found by their unique IDs, while `Containing` or `Nth` on the
  component find nothing.
- **Loose radios don't take arrow keys.** `RadioGroup` does; the checkout form's delivery options are
  still separate `Radio`s, as their rows show a name, detail and price that the group's labels don't.
- **`Chip` isn't a required-ID control.** Only a pressable chip should be, and the analyzer can't
  tell from the type. A `PressableChip`, or the rule looking at `OnPress`, would close the gap.
- **Typing is recorded twice where a platform sends both.** Committed text is reported from the text
  input client and from `UIRoot.TextInput`; macOS sends one or the other, but a platform that sent
  both would log the text twice.

## Tooling and process

- **The Write tool writes escapes as characters.** It turns `\uXXXX` escapes in C# strings into
  the literal (often invisible) characters. Check new files for control or invisible characters
  and write escapes back.
- **BOMs are inconsistent.** `.editorconfig` asks for UTF-8 with BOM, but newer files have none,
  and some csproj files are CRLF. Settle on one convention and normalise.
- **No window capture.** Agent sessions can't capture windows. `ui.screenshot` draws the tree again
  offscreen (`HeadlessGpu` + `OffscreenReadback`) at the window's pixel scale instead, which misses
  native chrome, menus and input method windows (see Automation).
- **GPU tests run on this Mac only**, by design: there is no remote CI.
- **AOT publishes aren't part of the test run.** The analysers run on every build, but only a
  publish shows that ILC and the linker are happy, and it takes a minute or two. A script that
  publishes PlatformCheck and runs its self-test (like `src/build.sh`) would keep it honest.
- **Silk.NET's AOT warnings are silenced wholesale.** `Directory.Publish.props` turns off IL2026,
  IL2104, IL3000, IL3002, IL3050 and IL3053 for the whole app, which would hide the same warnings
  from Radiant's own code at publish time (the build-time analysers still see them).
