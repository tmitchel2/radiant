# Radiant Platform

`Radiant.Platform` is how the UI reaches the operating system: the clipboard, the pointer's
cursor, the user's appearance settings, file dialogs, and text input with input methods. The UI
only ever talks to its interfaces; `Radiant.Platform.MacOS` implements them with AppKit.

```csharp
RadiantUI.Run(
    new ThemeProvider(themes, new App()) { FollowAppearance = true },
    new UIAppOptions { Platform = MacPlatform.CreateOrHeadless });

// In any component:
var platform = context.UsePlatform();
platform.Clipboard.SetText(selection);
var files = await platform.Dialogs.OpenAsync(new OpenFileOptions { AllowMultiple = true });
```

## Projects

| Project | What | References |
|---|---|---|
| `Radiant.Platform` | The interfaces, `IPlatform` grouping them, and `HeadlessPlatform` | Nothing |
| `Radiant.Platform.MacOS` | `MacPlatform`: AppKit through the Objective-C runtime | `Radiant.Platform` |
| `Radiant.UI.Core` | `Box.Cursor`, `UIRoot.Cursor`, `UIRoot.TextInputClient`, `PlatformContext` | `Radiant.Platform` |
| `Radiant.Theming` | `ThemeController.FollowAppearance`, `ThemeProvider.FollowAppearance` | `Radiant.Platform` |

The UI never references an implementation. The app chooses one: `UIAppOptions.Platform` is a
factory that `RadiantUI.Run` calls with the window's native handles once it is open. Without one,
the app runs on a `HeadlessPlatform`.

## The services

| Interface | What | macOS |
|---|---|---|
| `IClipboard` | `HasText`, `GetText`, `SetText` | `NSPasteboard.generalPasteboard`, UTF-16 exact |
| `ICursorService` | `Show(CursorShape)`, `Current` | `NSCursor`'s system cursors |
| `IAppearance` | `IsDark`, `AccentColor` (sRGB ARGB), `IncreaseContrast`, `ReduceMotion`, `Changed` | `NSApp.effectiveAppearance`, `NSColor.controlAccentColor`, `NSWorkspace` accessibility settings |
| `IFileDialogs` | `OpenAsync`, `SaveAsync` | `NSOpenPanel` and `NSSavePanel`, as sheets |
| `ITextInput` | `Focus(ITextInputClient?)`, `IsComposing`, `InvalidateCaret` | `NSTextInputClient` on GLFW's content view |

A platform belongs to the UI thread (on macOS, the main thread): call it from there, and its
events and task completions arrive there. Components reach it with `context.UsePlatform()`.
`RadiantUI.Run` provides it above the app through `PlatformContext.Platform`, and anywhere
nothing provides one (tests, offscreen snapshots) it is headless.

## Cursors

`Box.Cursor` gives a box its pointer shape; `null` uses its parent's.
- **Choosing one:** `UIRoot.Cursor` is the shape of the deepest hovered box that sets one, or
  the arrow. While a press is held, the pressed box's path decides instead, so dragging a
  splitter keeps its resize cursor wherever the pointer goes.
- **Showing it:** `UIRoot.CursorChanged` fires when the shape changes, through hover or
  through a hovered box's props changing, and `RadiantUI.Run` shows it through the platform.
- **Shapes:** `CursorShape` has the common ones (`Arrow`, `IBeam`, `PointingHand`,
  `ResizeLeftRight`, `ResizeUpDown`, `Crosshair`, `NotAllowed`, `Grab`, `Grabbing`) and the
  rest of AppKit's public cursors (one-way resize edges, vertical I-beam, drag copy and link,
  context menu, disappearing item).
- **GLFW's resets:** GLFW's content view sets the arrow whenever the pointer re-enters it
  (`cursorUpdate:`). The macOS platform replaces that method to show the UI's cursor instead.

## Appearance

`IAppearance` reads the user's settings and raises `Changed` when any of them changes. On macOS:
- **Dark mode** is `NSApp.effectiveAppearance` matched against Aqua and Dark Aqua, observed by
  key-value observing, with the distributed `AppleInterfaceThemeChangedNotification` as a
  second route.
- **The accent** is `NSColor.controlAccentColor` in sRGB, re-read on
  `NSSystemColorsDidChangeNotification`.
- **Contrast and motion** are `NSWorkspace.accessibilityDisplayShouldIncreaseContrast` and
  `…ShouldReduceMotion`, re-read on `NSWorkspaceAccessibilityDisplayOptionsDidChangeNotification`.

Every notification re-reads all four settings and raises `Changed` only if something differs.
AppKit delivers them on the main thread's run loop, which GLFW pumps when polling for events,
so `Changed` arrives between frames on the UI thread.

Themes follow the settings with `themes.FollowAppearance(appearance)`, or
`new ThemeProvider(themes, app) { FollowAppearance = true }`, which reads the platform above it.
- **What maps where:** dark mode sets `IsDark`, the accent becomes the seed, increase contrast
  sets contrast level 1, and reduce motion sets `MotionScheme.Reduced`. `AppearanceFollowing`
  turns each off.
- **When:** the theme is adjusted at once, before the first frame is drawn. Later changes by the
  user animate over the theme's medium duration, or change at once when motion is reduced.

## File dialogs

Both dialogs are asynchronous: the task completes when the user closes the panel, with the
chosen paths (empty when they cancel) or the save path (null when they cancel).
- **With a window**, the panel is a sheet on it (`beginSheetModalForWindow:completionHandler:`),
  so the app keeps drawing while it is open. The completion handler is an Objective-C block
  built by hand (`CompletionBlock`).
- **Without one** (a platform created with no window), the panel is app-modal (`runModal`) and
  the task is already complete when returned.
- **Continuations:** the task completes on the main thread, and without a synchronization
  context an `await` continues inline there, on the UI thread.
- **Filters:** they become the panel's allowed content types (`UTType`s from their extensions).
  macOS has no filter menu, so every filter's files are allowed at once.

## Window chrome

`IPlatform.Chrome` lets the app draw its own title bar. On macOS, `ExtendIntoTitleBar(true)`
gives the window a full-size content view under a transparent, untitled title bar: the window
keeps its frame, the content grows by the bar's height (28 px), and the traffic lights stay where
they are. `TitleBarHeight` and `LeadingInset` (68 px, past the zoom button) say how much room to
leave; `BeginDrag` moves the window from a press (`performWindowDragWithEvent:` with the press
AppKit is handling), and `TitleBarDoubleClick` does what the user chose in System Settings (zoom,
minimise or nothing). The `TitleBar` component does all of this while it's shown.

The self-test checks the content fills the window when extended and gets its size back after. By
hand, run `PlatformCheck`: its title bar is the app's own. Drag it by its empty space or title to
move the window, double-click it to zoom, and press its ⓘ button: the status line changes and the
window doesn't move.

## Menus

`IPlatform.Menus.ShowContextMenu` shows the platform's own context menu at a point and waits for a
choice. On macOS it's an `NSMenu` popped up in the content view: each item's action goes to a small
target object that notes its tag, and AppKit's menu loop returns when the user chooses or
dismisses it. `ContextMenu` uses it wherever it's supported and draws its own menu otherwise.

No test can drive AppKit's menu loop, so check it by hand: run `PlatformCheck` and right-click the
box under "Context menu". The system's menu appears at the pointer, Paste is greyed out, a
separator sits before Delete, and choosing Cut shows "Chose Cut." in the status line.

## Dropped files

Files dropped on the window (from the Finder or another app) arrive through GLFW's drop callback
as `RadiantApplication.FilesDropped`; `RadiantUI.Run` hands them to `UIRoot.DropFiles` at the
pointer, and they bubble to `Box.OnFileDrop` from the box under it. `DropZone` is the component
for it. To check by hand, run the gallery and drop an image on the components page's drop zone;
its line under the title shows the file's name.

GLFW reports only the drop, not a drag passing over the window, so nothing can highlight while
files hover, and there's no way to refuse a drop before it lands (the cursor always shows it's
accepted). Dragging out of the window isn't there either.

## Text input and input methods

Input methods (Japanese, Chinese, Korean, dead keys for accents) *compose* text before committing
it. The user types "nihongo", sees provisional text marked where the caret is, picks a
conversion from a candidate window, and only then commits. A text field takes part as an
`ITextInputClient`:

```csharp
public interface ITextInputClient
{
    void InsertText(string text);                                          // commit: replaces the composition (or selection)
    void SetMarkedText(string text, int selectionStart, int selectionLength); // provisional text; "" cancels
    void UnmarkText();                                                     // end the composition, keeping its text
    RectangleF CaretRect { get; }                                          // window coordinates, top-left origin
}
```

- **Focusing a client:** a focused field sets `UIRoot.TextInputClient = this`, and clears it on
  blur. `RadiantUI.Run` passes it to `ITextInput.Focus`.
- **Where typing goes:** while a client has focus, typed text goes to it rather than to
  `TextInput` events. Key events still reach the UI, except while an input method is
  composing: then the platform holds keys back (on macOS, all of them), so Enter that accepts a
  conversion isn't also Enter.
- **Changing focus:** a composition in progress ends when focus moves, keeping its text.
- **The candidate window:** after each update, if the client's `CaretRect` moved,
  `RadiantUI.Run` calls `InvalidateCaret` so an open candidate window follows. While composing,
  the start of the composition is the best caret to report: the window then stays put as the
  user types.

### On macOS: completing GLFW's view

GLFW 3.4's content view (`GLFWContentView` in `cocoa_window.m`) adopts `NSTextInputClient`, but
only to receive committed characters, which it turns into GLFW's char callback. The rest falls
short:
- It keeps marked text to itself and reports its marked range one character short.
- `firstRectForCharacterRange:` returns the view's origin in *window* coordinates, so candidate
  windows open at the bottom left of the screen.
- `keyDown:` reports every key to the app *before* `interpretKeyEvents:` gives it to the input
  method.

`MacContentView` fixes this at runtime. `class_replaceMethod` swaps in `[UnmanagedCallersOnly]`
functions for `keyDown:`, `insertText:replacementRange:`,
`setMarkedText:selectedRange:replacementRange:`, `unmarkText`, `hasMarkedText`, `markedRange`,
`selectedRange`, `firstRectForCharacterRange:actualRange:` and `cursorUpdate:`, and keeps
GLFW's implementations.
- **Passing through:** the class is shared by every GLFW window, so each replacement looks up
  the view it was called on. A view with no Radiant text input attached, or with no client
  focused, gets GLFW's original method, and behaves exactly as before.
- **With a client:** the text methods go to `MacTextInput`, which keeps the composition's length
  and selection and forwards the text to the client. GLFW never sees the text, so no char
  events are sent.
- **The caret:** `firstRectForCharacterRange:` converts the client's `CaretRect` from the UI's
  top-left coordinates through the view (`convertRect:toView:`) and window
  (`convertRectToScreen:`) to screen coordinates.
- **Keys while composing:** `keyDown:` skips GLFW and only calls `interpretKeyEvents:`, so the
  UI gets no key event. GLFW ignores a release for a key it never saw pressed, so the key-up is
  dropped too. Outside a composition, GLFW's `keyDown:` runs as normal.
- **Command:** text typed with ⌘ held isn't inserted, as GLFW decides too, since it's a
  shortcut.
- **Clearing up:** changing the client calls `discardMarkedText` on the view's input context to
  reset the input method, and clears GLFW's own marked text.

**Limits:**
- **The document is only the composition.** Clients don't expose their text, so the marked range
  starts at 0 and `attributedSubstringForProposedRange:` returns nil. Features that read around
  the caret, such as reconversion of committed text and context-aware prediction, get nothing,
  and `replacementRange` is ignored.
- **Mouse clicks don't reach the input method**, so clicking inside a composition doesn't move
  within it.
- **Marked text styling is lost.** The input method's attributes aren't passed on, and the client
  only has the selection to show which clause is being converted.
- **It's tied to GLFW 3.4.** The replaced selectors are AppKit's, so they survive GLFW updates,
  but a GLFW that stops calling `interpretKeyEvents:` from `keyDown:` would need revisiting.
  `PlatformCheck --selftest` catches that.

## The headless platform

`HeadlessPlatform` has no operating system behind it, and its parts are concrete types so tests
can script and inspect them:

| Part | Does |
|---|---|
| `HeadlessClipboard` | Holds the text in a field |
| `HeadlessCursorService` | Records `Current` and counts `ShowCount` |
| `HeadlessAppearance` | Has settable properties that raise `Changed` |
| `HeadlessFileDialogs` | Answers through `OnOpen` and `OnSave`, and cancels if they aren't set |
| `HeadlessTextInput` | `Type`, `Compose` and `Unmark` act as an input method would on the focused client |

## Checking it

- **Unit tests:** `Radiant.Platform.Tests` covers the headless platform, and
  `Radiant.UI.Core.Tests` (`PlatformTests`) covers cursors from hover, the client and caret
  forwarding, and the platform context. `Radiant.Theming.Tests` (`ThemeAppearanceTests`) covers
  following the appearance.
- **macOS tests:** `Radiant.Platform.MacOS.Tests` runs in the normal test run:
  - the clipboard round-trips through a private pasteboard;
  - every cursor shape is a distinct `NSCursor`, and showing one makes it current;
  - appearance reading agrees with `defaults`, and notifications lead to `Changed`;
  - completion blocks survive `Block_copy`;
  - text input is checked on a stand-in `NSView` subclass that records which original methods
    ran: marked text, commits, ranges, the caret rectangle, key suppression while composing, and
    passing through without a client.
- **Integration** (excluded from the normal run):
  - the real system clipboard, restored afterwards;
  - `PlatformCheck --selftest` in a child process. It opens a window and makes AppKit's calls on
    GLFW's real content view, printing PASS or FAIL for each.

  ```
  dotnet test src/Radiant.slnx --no-build --settings src/settings.runsettings --filter "TestCategory=Integration"
  dotnet run --project src/PlatformCheck -- --selftest
  ```

### Checking input methods by hand

No test can make a real input method compose, so check that by hand:

```
dotnet run --project src/PlatformCheck
```

1. In System Settings › Keyboard › Text Input, add **Japanese – Romaji** and/or **Chinese,
   Simplified – Pinyin**.
2. Start the check. The field at the top has focus (an I-beam over it, a caret inside).
3. **Japanese:** switch input source (Ctrl+Space or the menu bar) and type `nihongo`.
   - Provisional kana appear underlined in the accent colour.
   - The log below the field shows entries like `marked "にほんご" selection 4+0`.
4. Press **Space**:
   - A conversion appears, and a candidate window opens just below the start of the underlined
     text, not at the bottom left of the screen.
   - Space again or the arrow keys move through candidates.
   - The log shows no `key Space` or `key Down`: the UI didn't also get those keys.
5. Press **Enter**:
   - The text is committed (`insert "日本語"`) and the underline goes.
   - There's no `key Enter` in the log. Press Enter again, with nothing composing, and there is.
   - Type again and press **Escape** instead (twice, if the first only undoes the conversion):
     the composition is cancelled (`cancel composition`, or an empty `insert`), again with no
     `key Escape`.
6. **Chinese:** switch to Pinyin, type `nihao`, and choose 你好 with **1** or **Space**. The
   candidate window follows the field.
7. **Dead keys:** in the ABC layout, press ⌥E then E: an underlined ´ appears, then é.
8. Click outside the field mid-composition: the field loses focus, and the composition ends
   keeping its text (`unmark`).

The window also checks the other services:
- **Cursors:** hover each tile to see its cursor.
- **Appearance:** switch dark mode, the accent colour, or Accessibility › Display › Increase
  contrast or Reduce motion. The theme and the readout follow within a frame.
- **Clipboard:** copy and paste the field's text.
- **Dialogs:** try Open… (multiple selection, images and text) and Save…, as sheets.

## Not yet

- **Other platforms:** Windows and Linux implementations; they run headless until then.
- **The accessibility bridge:** exposing `UIRoot.GetSemantics()` to VoiceOver comes later in P7.
- **Rich clipboard formats:** images, files and styled text.
- **Platform input:** key repeat and precise trackpad scrolling still come through Silk and GLFW.

See [improvements.md](improvements.md#platform-radiantplatform) for what is done the second-best
way.
