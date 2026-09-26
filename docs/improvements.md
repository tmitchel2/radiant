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
- **Layered facets are found by convention.** A facet type with a `T Merge(T over)` method is
  merged over the target's value by the forwarders instead of replacing it. It's implicit; an
  attribute on the facet property (`[Layered]`) would say so where it's declared. Components that
  don't forward `IHasLayout` still merge by hand (`SurfaceIcon`, `TextField`).
- **A jump fires `Scroll`.** `ScrollController.ScrollTo(…, animated: false)` now raises `Scroll`,
  which it didn't, so listeners see jumps. Momentum and bounce raise it from `Update` as before.

- **Image textures are never freed.** `ImageSource` keeps one texture per renderer in a weak table,
  but nothing disposes the GPU texture when the source goes. Pictures are also decoded
  synchronously on the UI thread, and there are no mipmaps, so heavy downscaling aliases. Add
  async decoding, an image cache with release, and mipmaps.
- **Tests reach into internals.** Component tests read the render tree through
  `InternalsVisibleTo`. The planned `Radiant.Testing` should offer a public way to find elements
  and read their resolved props and bounds, like Testing Library's queries.

- **Callbacks make props unequal.** Records compare delegates by reference, so a component
  given a fresh lambda each build always rebuilds. The P5 generator should emit props equality
  that ignores delegates, and refresh callbacks in place.
- **Children lists compare by reference.** A host element whose parent rebuilds always updates,
  even when its children are equal element for element. A structural list comparison, or a
  generated one, would let unchanged subtrees skip.
- **Animations redraw everything.** `RadiantUI.Run` now draws only while `UIRoot.NeedsUpdate` (an
  idle window waits for input and uses no CPU), but anything animating, a spinner included,
  rebuilds its component and redraws the whole window every frame: about 37% of a core for the
  gallery's components page in a Debug build. Paint-time animation and damage regions (P11)
  would make a spinner cost a spinner.
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
- **Duplicated style mapping.** `YogaStyle` repeats the legacy `YogaLayoutEngine` mapping. It
  becomes the only copy when the legacy UI is deleted (the hard replace).
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
- **Semantics have no actions.** Nodes can't be pressed, incremented or scrolled through the
  tree yet, which the P7 accessibility bridge needs.

## Platform (`Radiant.Platform`)

- **Three copies of the Objective-C interop.** `Radiant.Platform.MacOS.ObjC` is the full one;
  `Radiant.Host.MacObjc` and a private copy in `RadiantApplication` predate it. They were left
  alone so P7 didn't disturb the host. Point both at one shared interop, either
  `Radiant.Platform.MacOS` or a small interop assembly that both it and `Radiant` reference.
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

- **No text field yet.** `PlatformCheck`'s input field is a sketch of what a `TextField` needs
  (a text input client, marked text underlined, the caret at the composition's start). In it, a
  `TextBlock` sized to its text gave trailing spaces no width, so a composition after "a " sat
  against the "a"; the real field needs the caret from text layout, spaces included.

- **No golden images yet.** The plan's matrix (variants × enabled, hover, focus, pressed,
  disabled × light and dark) doesn't exist yet. The gallery's `--snapshot` renders offscreen to
  PNG, so it could start from there.
- **Presses don't animate fully.** State layers fade in and out (`UseTransition`), but there's no
  press scale and no ripple.
- **Sliders are single-valued.** No range slider, no value label while dragging, and no tick marks
  for stepped sliders.
- **Tabs are primary tabs only.** The indicator spans the whole tab rather than its content,
  secondary tabs are missing, and many tabs don't scroll.
- **Menus show focus even when opened by pointer.** A menu opened with the pointer focuses its first
  item with the keyboard focus ring; Material shows it only when opened from the keyboard.
- **Menus are basic.** No submenus, no type-to-select, no check or radio items.
- **Tooltips add a box.** `Tooltip` wraps its child in a `Box`, which can change the layout of a
  child that relied on its parent's flex settings. Nothing announces the tip to assistive
  technology yet.
- **Dialogs have one form.** There's no full-screen variant, and no scrolling body for long
  content.
- **Only 272 icons are embedded.** They're curated in `tools/icons/icons.txt`, to keep the font
  at 408 KB rather than 15 MB. An app wanting icons outside the list must register the full font
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
  freed yet; the gamut's edge is stepped at that resolution. There's no alpha, no eyedropper, no
  saved swatches, and no contrast readout against a chosen background.
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
  in `SurfaceText` would do it), there's no ordering by recent use, and a `Command.Shortcut` is
  text: nothing binds it. A command registry would bind chords, feed menus and the palette, and
  let the palette show whether a command is available.
- **Overlay primitives add boxes.** `DismissableLayer` and `FocusScope` each wrap their content in
  a box that sizes to it, so a panel inside can't simply fill or be a percentage of its parent.
  Both now take a `Layout` for their box (a side sheet has its layer stretch and its scope grow),
  but every overlay has to know about them. Layout-transparent host elements (a box that passes
  its children straight to its parent's flex layout, like CSS `display: contents`) would remove
  the problem.
- **Context menus are drawn in the window.** They're Radiant menus at the pointer, not the
  platform's (NSMenu on macOS), so they can't extend past the window and don't get system
  services items. A platform menu service (P7's `IMenuService`) would give native ones.
- **Toolbars aren't a single Tab stop.** Arrows move within a toolbar, but Tab still visits every
  control in it; a roving tab index (only the last-focused control tabbable) needs `TabIndex` on
  `IconButton` and the other controls, which only `PressableSurface` and `ToggleButton` have.
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
- **The slider's track is inset.** The track now sits 10 px (the handle's radius) inside the
  slider, so the handle stays inside at either end; code measuring a slider by its bounds must
  allow for it.
- **Icon buttons in fields were named by their icon.** `TextField`'s trailing button used the
  icon's name ("visibility") as its label; `TrailingIconLabel` now names it, but it falls back to
  the icon name when unset. Require a label when there's an `OnTrailingIconPress`, or analyse for it.
- **Button heights are fixed.** They are 40 px plus density; Material 3's newer button sizes
  (XS–XL) aren't modelled.

- **`Anchored` checks its anchor every frame.** It runs a ticker while shown, which keeps frames
  coming. Layout-change notifications from the render tree would avoid the polling.
- **Unplaced content can still be clicked.** Before its first placement, `Anchored` content is
  invisible (opacity 0) but its children can still be hit for that one frame.
- **Escape only dismisses from inside.** `DismissableLayer` sees Escape only when focus is within
  it; Radix listens on the document. Add a root key observer if layers without focus need it.

## Templates (`Radiant.Templates`)

- **Docks are fixed.** `WorkspaceLayout`'s parts can't be dragged to other docks, the panel can't
  be maximised or closed from its header, editors can't be split into groups, and splitter sizes
  aren't remembered between runs.
- **Icons for source control and mail are missing.** The embedded subset lacks `account_tree`,
  `call_split`, `reply` and `drafts`, so the gallery's workspace and mail use stand-ins. Add them
  to `tools/icons/icons.txt` and re-subset.
- **The gallery frames shells by hand.** A shell previewed in a frame is inset by the outline's
  width, but its corners still cross the frame's rounded outline; clipping children to the shape
  inside the border would fix it generally.

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

- **A theme change rebuilds every reader.** Components reading the theme rebuild on every change,
  and on every frame of a transition. The expensive part was text: colour lived in `TextStyle`,
  so a recolour re-shaped and re-measured every paragraph. Plain text now takes its colour at
  paint, which brought a theme change on a 158-node page from 9.7 ms to 1.2 ms (Release), well
  inside a 120 fps frame. The plan's goal is still zero rebuilds: render nodes holding symbolic
  tokens (colour roles, shape roles) resolved at paint time, which needs a token type `Box` can
  take and a paint-time theme scope, so a dark section can sit inside a light app.
- **Schemes use the Phone platform.** Material's phone and watch are the only platforms upstream;
  check whether a desktop tuning is wanted.
- **The type scale is sized for phones.** Material 3's body text is 14 px, larger than typical
  desktop UI (13 px on macOS). Consider a desktop `TypeScale`, or density scaling type.
- **Monochrome custom families are grey.** Custom families take the variant's primary palette, so
  success, warning and info are grey under Monochrome.
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
  AOT factories aren't generated yet.
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
- **Two HarfBuzzSharp gaps.** It lacks `Font.MakeImmutable` (P/Invoked), and `Blob.FromStream`
  keeps managed memory, which a compacting GC moved (fixed: native copy).

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

- **SixLabors.ImageSharp** (Split License) still decodes PNGs. Consider a permissively licensed
  decoder; ask first.
- **`MsdfBaker` is due for replacement.**
  - It still uses SixLabors.Fonts 2.0.7.
  - Its MSDF core fixes signs with an even-odd test that flips individual channels, which breaks
    corners and overlapping contours.
  - Replace it with the runtime MSDF generator and HarfBuzz outlines once they land.

## Tooling and process

- **The Write tool writes escapes as characters.** It turns `\uXXXX` escapes in C# strings into
  the literal (often invisible) characters. Check new files for control or invisible characters
  and write escapes back.
- **BOMs are inconsistent.** `.editorconfig` asks for UTF-8 with BOM, but newer files have none,
  and some csproj files are CRLF. Settle on one convention and normalise.
- **No window capture.** Agent sessions can't capture windows, so visual checks render offscreen
  (`HeadlessGpu` + `OffscreenReadback`) to PNG.
- **GPU tests run on this Mac only**, by design: there is no remote CI.
