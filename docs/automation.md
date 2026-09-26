# Radiant Automation — driving UI apps from tests and agents

Automation drives a running Radiant UI app from outside it. Tests use it the way Detox does: find an
element, wait for the app to settle, act, and expect. An agent uses it through a CLI. It works from
structured JSON (roles, labels, bounds, what's visible, where a tap lands, scroll positions), from
annotated screenshots, or from both. Everything done to the app goes into one interaction log,
whether a person did it through the window or a test or agent did it through the protocol.

| Assembly | What it holds |
|---|---|
| `Radiant.Host.AgentControlProtocol` | The wire model, including selectors and log entries. The dispatcher, the file and socket transports, `AgentClient`, `AgentLauncher`. No UI dependency. |
| `Radiant.UI.Core` | What the automation builds on: `UINode` for inspection, `IsIdle`, ticker kinds, busy tracking, `Element.TestId`, `ScrollIntoView`, `InputReceived`, and `UIAppSession`, which runs a UI headed, headless or by hand. |
| `Radiant.UI.Automation` | The `ui.*`, `app.*` and `log.*` actions, the selector engine, the inspector, screenshots, the interaction log, `AgentServer`, and `RadiantAutomation.Configure`. |
| `Radiant.UI.Driver` | The C# SDK: `AppDriver`, `Locator`, expectations. |
| `Radiant.UI.Driver.MSTest` | `RadiantUITest`: a failed test keeps its screenshot and log. |
| `Radiant.AgentCli` | `radiant-agent`, the CLI. It references only the protocol, so it needs no GPU and publishes AOT. |

## For agents: `radiant-agent`

```sh
radiant-agent launch src/Radiant.Gallery --headless       # builds, starts, waits until ready; prints the instance
radiant-agent tree                                        # what's on screen, one element a line
radiant-agent find 'role=tab label~=sign'                 # matches, with bounds and where to tap
radiant-agent tap 'role=tab label="Sign in"'
radiant-agent type 'role=textField label=Email' 'ada@example.com'
radiant-agent inspect @list --fields basic,scroll,hit --depth 1
radiant-agent screenshot out.png --annotate               # numbered marks on the image, listed with tap points
radiant-agent log                                         # what's happened so far
radiant-agent exit
```

- **One line per element.** `tree` prints lines like `#648 textField "Email" (630,188 237×24) focusable`.
  It lists only what can be seen unless you pass `--all`. `--json` prints the raw result instead.
- **Tap from the structure, not the picture.** `find` and `inspect --fields hit` give the point to tap,
  and so does a screenshot's legend. `tap --at x,y` takes logical points; `--px` takes screenshot pixels.
- **Errors go to stderr and say what to do.** For example:
  `error no_match: Nothing matches text=Dashbord. Did you mean tab "Dashboard" #11?`.
- **Exit codes:**
  - 0: done
  - 1: the app said no (no match, ambiguous, covered)
  - 2: timed out, or the app never went idle
  - 3: no app to talk to
  - 64: usage
- **Choosing an instance.** `-i <name>` picks one. So does `$RADIANT_AGENT_INSTANCE`. With neither,
  it uses the only UI instance running.
- **Choosing a transport.** `--transport socket|file` picks one; the default is the socket when
  there is one. `--timeout 10s` sets how long the app may take.
- **Running with a window.** Leave out `--headless`. Add `-- <args>` to pass arguments to the app.
- **Help.** `radiant-agent help` lists every command.

The file transport is plain JSON files, so an agent that can only read and write files can drive an
app too:

1. Write `{"id":"c1","action":"ui.tap","params":{"selector":"@save"}}` to
   `<instances>/<name>/commands/cmd-c1.json`. Write it to a temporary name first, then rename it.
2. Read the answer from `responses/cmd-c1.json`, then delete that file.

## For tests: `Radiant.UI.Driver`

```csharp
[TestClass]
public sealed class SignInTests : RadiantUITest
{
    [TestInitialize]
    public void Start() => Use(AppDriver.InProcess(new MyApp(), new InProcessOptions { Size = new(1200, 800) }));

    [TestMethod]
    public async Task SigningInGreets()
    {
        await Driver.ByTestId("email").TypeAsync("ada@example.com");
        await Driver.Get("role=button label=\"Sign in\"").TapAsync();
        await Driver.ByTestId("greeting").Expect().ToHaveTextAsync("Hello, Ada");
    }
}
```

- **In-process.** `AppDriver.InProcess` runs the app on the test's thread, headless, on the fixed
  clock. Each command steps frames until it's answered, so a 300 ms transition takes about a
  millisecond, and every run is the same.
- **Another process.** `AppDriver.LaunchAsync(new AgentLaunchOptions { Target = "src/MyApp", Headless = true })`
  builds and starts the real app. `AttachAsync(name)` connects to one already running.
- **Same code either way.** The same actions run in both cases (the in-process client queues onto the
  same dispatcher), so one test means the same in-process, over the socket and over files.
  `RemoteTests` checks this.
- **Locators** (`Get`, `ByTestId`, `ByText`, `ByRole`, `Nth`, `Within`) are found again each time they're used.
- **Actions:**
  - `TapAsync`, `DoubleTapAsync`, `RightClickAsync`, `PressAsync`, `LongPressAsync`, `HoverAsync`
  - `FocusAsync`, `TypeAsync`, `FillAsync`
  - `ScrollIntoViewAsync`, `ScrollAsync`, `ScrollToAsync`
  - `SwipeAsync`, `DragToAsync`
- **Reading:** `InspectAsync(fields, depth)`, `QueryAsync`, `CountAsync`, `TextAsync`.
- **`Expect()`** waits until its condition holds: `ToExist`, `ToBeVisible`, `ToBeHittable`, `ToBeGone`,
  `ToBeHidden`, `ToBeEnabled`/`Disabled`, `ToBeFocused`, `ToBeChecked`/`Unchecked`, `ToHaveText`,
  `ToContainText`, `ToHaveValue`.
- **Failures** throw `AppDriverException`. It carries the error code and the details, and its message
  includes the last dozen log entries.
- **`RadiantUITest`** (MSTest) disposes the driver after each test. Before that, if the test failed,
  it saves `interaction.log`, `interaction.jsonl` and an annotated `failure.png` (when there's a
  GPU) with the test's results.

## Turning it on in an app

```csharp
var options = RadiantAutomation.Configure(new UIAppOptions { Title = "My app", … }, args);
RadiantUI.Run(new MyApp(), options);
```

`Configure` returns the options unchanged unless automation is asked for, so an app pays nothing
normally. When it is asked for, it adds an `AgentServer` extension, which:

- registers the instance;
- serves the file and socket transports;
- writes the log;
- marks the instance ready once the tree is first laid out.

| Variable (or argument) | Effect |
|---|---|
| `RADIANT_AGENT=1` (`--agent`) | turns it on |
| `RADIANT_AGENT_NAME` (`--agent-name n`) | the instance's name; `<app>-<pid>` otherwise |
| `RADIANT_AGENT_HEADLESS=1` (`--headless`) | no window |
| `RADIANT_AGENT_CLOCK=real\|fixed` | the clock; fixed when headless, real otherwise |
| `RADIANT_AGENT_SIZE=1200x800`, `RADIANT_AGENT_SCALE=2` | the size, and the pixel scale when headless |
| `RADIANT_AGENT_LOG=path` | the interaction log; `<data dir>/logs/<name>-<time>.jsonl` otherwise |
| `RADIANT_AGENT_TRANSPORTS=file,socket` | which transports |
| `RADIANT_AGENT_LOG_TEXT=0`, `RADIANT_AGENT_LOG_HOVER=1` | leave typed text out of the log; add hovering to it |
| `RADIANT_INSTANCES_DIR` | where instances register: keeps test runs apart |

`radiant-agent launch` and `AppDriver.LaunchAsync` set these. The gallery opts in, so
`radiant-gallery --agent` works too.

## Selectors

A selector is a set of tests that must all pass. The compact form is used on command lines and in
tests. The JSON form is an object; a string in the compact form is accepted anywhere an object is.

| Compact | JSON | Matches |
|---|---|---|
| `@save`, `testId=save` | `{"testId":"save"}` | the test ID |
| `#412` | `{"id":412}` | a node id (from a tree; good only while the node lives) |
| `role=button` | `{"role":"button"}` | the semantics role; case, `-` and `_` are ignored |
| `label="Save all"` | `{"label":"Save all"}` | the label, exactly |
| `text=Save`, or just `Save` | `{"text":"Save"}` | the label, value or text: what it shows |
| `value^=ada` | `{"value":{"exact":"ada","ignoreCase":true}}` | the value, ignoring case |
| `label~=sav` / `label*=Sav` | `{"label":{"contains":"sav","ignoreCase":true}}` | contains (`~=` ignores case) |
| `label=/^Sa/i` | `{"label":{"regex":"^Sa","ignoreCase":true}}` | a regular expression |
| `visible`, `enabled`, `focused`, `checked`, `selected`, `checked=false` | `{"checked":false}` | state |
| `[2]`, `[-1]` | `{"index":2}` | which match, from 0; negative counts from the end |
| `@list >> text=Row` | `{"text":"Row","within":{"testId":"list"}}` | inside another match |

- **Where matching runs.** It runs over the semantics tree, which is what assistive technology
  sees, with each node joined to its laid-out node for geometry.
- **Text inside a control.** A button absorbs the text inside it as its label, so `text=Save` finds
  the button.
- **Actions need exactly one match.** When several match, the action picks:
  - the one that can be seen, if only one can;
  - the innermost, if they all sit one inside another;
  - otherwise it answers `ambiguous`, listing the matches.
- **No match.** It answers `no_match` and suggests near misses: a typo, an abbreviation, or the same
  role with a similar name.

## Test IDs

`Element.TestId` names an element for tests. It is never shown; on macOS it becomes the
accessibility identifier, so XCUITest and Appium can use it too. Set it on a `Box`, a `TextBlock`,
a `ScrollArea` or any component.

- **On a component** (`new SurfaceButton("Save") { TestId = "save" }`), it names the one box the
  component draws.
- **When nested components all set one,** the outermost wins. That lets a caller rename what a
  component names inside itself.
- **`Semantics.TestId`** takes precedence over all of these.
- **A box with only a test ID** appears in the semantics tree with role `None` and no label.
  VoiceOver passes over it.
- **When a test ID names a container, actions look inside it.** A `TextField`'s test ID names its
  outer box, so focus and typing go to the first focusable element inside, and its value is the
  field's.

## Acting, the Detox way

Each action on an element does these steps, stopping at the command's timeout:

1. Waits until the app is idle for two frames running (`waitIdle: false` skips this).
2. Resolves the selector, waiting for it to match.
3. If some of it is out of view and a scroll area can show it, scrolls it in
   (`UIRoot.ScrollIntoView`) and waits for idle again (`scroll: "none"` skips this).
4. Waits until a point on it would take the press. It tries the centre of its visible part, then a
   3×3 grid, then a 5×5 grid, each time hit testing to check (`force: true` uses the centre,
   whatever covers it).
5. Acts through `UIRoot`'s input methods, as a window would. A tap moves the pointer there, then
   presses and releases.
6. Waits for idle again (`waitAfter: false` skips this). The result says what it hit, where, and
   in how many frames.

If the timeout comes first, the error says why:

| Error | Carries |
|---|---|
| `busy` | the busy reasons |
| `no_match` | suggestions |
| `not_visible` | where it is |
| `not_hittable` | what covers it (`obscuredBy`) |

## Inspecting

`ui.tree` and `ui.inspect` answer
`{frame, tree, coords: "logical", pixelScale, window, nodes: [...]}`. Each node has the fields
asked for, plus `children` down to `depth`; where the depth stops, it has `childCount`. Name fields
and groups separated by commas, and put `-` before one to leave it out. The default is
`basic,geometry,state`.

| Group | Fields |
|---|---|
| `basic` | `id`, `role`, `label`, `testId`, `kind` |
| `geometry` | `bounds` (after transforms, in window points), `size`, `position` (in its parent) |
| `visibility` | `visible` (bounds cut by every clipping ancestor and the window, or null), `visibleRatio`, `opacity` (with its ancestors'), `onScreen` |
| `hit` | `tap` (where a press lands on it), `hittable`, `obscuredBy` (what's on top at its centre) |
| `scroll` | `offset`, `max`, `content`, `viewport`, `canScroll {x,y}`, `animating` |
| `semantics` | `value`, `description`, `headingLevel` |
| `state` | `disabled`, `checked`, `selected`, `expanded`, `focusable`, `focused` |
| `text` | `text`, `selection {start,end}`, `placeholder` |
| `render` | `type`, `clips`, `hitTestVisible`, `transformed` |
| `screen` | `screenBounds` (plus the window's position), `pixelBounds` (times the pixel scale) |
| `all` | everything |

- **Empty fields.** A field that comes from a group is left out when it's false or empty. A field
  named on its own is always written.
- **Which tree.** `tree: "semantics"` (the default) follows the semantics tree. `tree: "render"`
  follows every laid-out node, for layout questions.
- **Coordinates:**
  - Logical window points, top left at the origin: `UIRoot`'s input takes these.
  - Pixels are points times `pixelScale`.
  - Screen positions add the window's position.

## Idle, time and headless running

- **Idle.** `UIRoot.IsIdle` is true when the tree is mounted, nothing is waiting to rebuild or run,
  no scroll is moving, and no `TickerKind.Animation` ticker or busy work is running.
  `BusyReasons()` says what's still going: `animation: transition`, `scroll area #69 moving`,
  `busy: loading orders`.
- **Ticker kinds.** `AddTicker(tick, kind, reason)` says what a ticker is. Every kind still gets
  frames.
  - `Continuous` never keeps the UI busy: spinners, skeletons, the caret's blink, anchored popovers.
  - `Timer` doesn't either: tooltip and hover card delays, a snackbar's timeout.
- **Busy work.** Mark work outside the UI with `using (root.BeginBusy("saving")) …` or
  `root.TrackBusy(task, "loading orders")`. Both work from any thread.
- **Clocks.**
  - `UIClockMode.Real` advances by the time since the last frame, capped at 50 ms.
  - `UIClockMode.Fixed` advances exactly 1/60 s per frame, and `UIRoot.Clock` reads the same virtual time.
  - `app.step` (`StepAsync`) moves time on, to let a timer fire.
- **Headless.** `RadiantUI.Run` with `Headless` (or `RadiantUI.RunHeadless`) runs frames on the
  calling thread, on the headless platform.
  - On the fixed clock, frames run only while the UI isn't idle or a command is waiting, and as fast
    as they can. Otherwise it sleeps. Waits cost no wall time, and spinners stay where they are,
    the same in every screenshot.
  - On the real clock, it runs at up to 60 frames a second while the tree needs frames.
- **`UIAppSession`** is the app's frame loop, whoever drives it. It owns the root and its platform,
  the clock and the frame count, and runs `BeforeFrame` (where commands are pumped) and
  `AfterUpdate`. `RadiantUI.Run`, the headless loop and `UIAppSession.CreateManual(...).Step()` all
  use it. Extensions (`UIAppOptions.Extensions`) plug into it.

## Screenshots

`ui.screenshot` draws the tree again on a headless GPU device of its own, at the window's size and
pixel scale (or the headless one's), and writes a PNG.

- **Where it goes.** The path you give, or `<log>-shots/NNNN.png` beside the log.
- **Annotation.**
  - `annotate: "interactive"` numbers every visible control on the image, with an outline and a tab,
    and lists each mark with its id, role, label, test ID, bounds and tap point.
  - `annotate: "all"` numbers every node that has a role or a test ID.
- **Cropping.** `selector` cuts the image to one element; `origin` gives where the crop starts.
- **What it can't show.** It's a fresh drawing of the tree, not a capture of the window, so native
  chrome, menus and input method windows aren't in it. With no GPU it answers `unsupported`.

## The interaction log

Each running app writes JSON Lines to `<data dir>/logs/<instance>-<time>.jsonl`. The instance lists
the path in `InstanceInfo.LogPath`. The file starts afresh at 20 MB. An entry looks like this:

```json
{"seq":57,"t":"2026-09-26T10:00:01.234Z","ms":8123.4,"frame":882,"src":"human","kind":"input",
 "input":{"type":"tap","x":412,"y":300},"target":{"id":41,"role":"button","label":"Save","testId":"save","path":"dialog \"Edit\" > button \"Save\""}}
```

- **`src`** is who did it: `human` (through the window), `agent` (a transport), `test` (in-process)
  or `app`.
- **`kind`:**
  - `input`: a person's tap, drag, wheel, key, text or drop.
  - `action` and `result`: a command and how it went, with a one-line summary.
  - `state`: focus moving, put down to whoever caused it.
  - `screenshot`, `note` (from `log.note`) and `session`.
- **People's input is gathered up.**
  - A press and release make one tap, or a drag if the pointer moved more than 4 points.
  - Wheel movement is summed until it pauses for 200 ms.
  - Typing is gathered until it pauses for 1 s, or other input comes. It includes text committed
    through an input method.
  - Printable keys are recorded as text, not as keys.
  - Each input names the element it reached: the nearest node at or above the hit that assistive
    technology sees.
- **What's left out.** Queries (`ui.tree`, `ui.inspect`, `ui.query`, `app.info`) aren't logged
  unless `InteractionLogOptions.RecordQueries` is set. Typed text becomes `•••` when
  `RADIANT_AGENT_LOG_TEXT=0` is set, including the text in an action's params.
- **Reading it:**
  - `radiant-agent log` prints the file.
  - `radiant-agent log -f` streams it over the socket (`log.subscribe`), or tails the file on the
    file transport.
  - `AppDriver.LogAsync()` streams it and `LogTailAsync()` returns the end of it.
  - `LogFormatter.Format` renders one line:
    `10:00:01.234  #882  human  tap  button "Save" @save #41 (412,300)`.

## The protocol

**Discovery.** Each app writes `<instances>/<name>/instance.json`. The protocol-2 fields are:

- `kind` (`ui` or `host`), `appName`, `agentProtocolVersion`
- `transports`, `socketPath`, `logPath`
- `headless`, `clock`, `ready`

`InstanceRegistry.RootDir` follows `$RADIANT_INSTANCES_DIR`.

**The socket** (`agent.sock` in the instance's directory, or a hashed name in the temp directory
when that path is too long) carries one compact JSON message per line. Only this user can read and
write it.

```
← {"type":"hello","protocol":2,"instance":"g1","app":"radiant-gallery","capabilities":["ui","ui.screenshot","log","clock.fixed"],"clock":"fixed","headless":true}
→ {"type":"req","id":"c7","action":"ui.tap","params":{"selector":"@save"},"timeoutMs":10000}
← {"type":"res","id":"c7","status":"ok","result":{…},"durationMs":41.2,"frame":1880}
← {"type":"evt","event":"log","sub":"s1","seq":212,"data":{…}}
→ {"type":"cancel","id":"c7"}
```

**Files** use the same command and response objects without `type`. They can't push events.

**The dispatcher** (`AgentDispatcher`) runs every command on the UI thread, at the start of a frame.

- Queries start at once. Mutations run one at a time, in the order they came.
- An action is an `AgentOperation`, polled once a frame, so a wait spans frames without blocking.
- Every command is answered once: with its result, its error, `timeout` (by its own `timeoutMs`,
  default 10 s, counted on the app's clock) or `cancelled`.
- A socket client that disconnects has its commands cancelled.

The error codes are `invalid_params`, `not_found` (no such action), `no_match`, `ambiguous`,
`not_visible`, `not_hittable`, `timeout`, `busy`, `unsupported`, `cancelled`, `unreachable` and
`internal`.

| Actions | |
|---|---|
| `app.info`, `app.idle`, `app.step`, `app.exit` | the app; wait for idle; move time on; end it |
| `ui.tree`, `ui.inspect`, `ui.query`, `ui.waitFor`, `ui.screenshot` | look |
| `ui.tap`, `ui.tapAt`, `ui.press`, `ui.longPress`, `ui.hover`, `ui.focus`, `ui.type`, `ui.key`, `ui.scroll`, `ui.scrollTo`, `ui.swipe`, `ui.drag` | act |
| `log.subscribe`, `log.unsubscribe`, `log.note`, `log.tail` | the log |
| `actions.list` | every action, with a JSON Schema of its params generated from the params records |

**Versioning.** `AgentProtocol.Version` is **2**:

- Version 1 was the file transport with the host's `tab.*` actions.
- Version 2 adds the socket, the dispatcher and the UI actions.
- Adding actions or fields keeps the version; changing or removing them bumps it.
- It's separate from `TabProtocol.Version` (see [host.md](host.md)).

## Checking it

- **Normal tests.** `Radiant.UI.Automation.Tests` runs with the rest of the suite:
  - driver tests on a small form app and on the gallery, in-process;
  - the same test in-process, over the socket and over files;
  - served apps on a thread, on both clocks;
  - the CLI;
  - an annotated screenshot (category `Gpu`).
- **Integration tests.** `LaunchTests` (category `Integration`, left out by `src/build.sh`) builds
  and launches the gallery as a process over each transport:
  `dotnet test src/Radiant.UI.Automation.Tests --filter TestCategory=Integration`.
- **By hand:**
  1. `radiant-agent launch src/Radiant.Gallery` opens a window.
  2. In another shell, `radiant-agent log -f` follows the log.
  3. Click in the window and `human` entries appear.
  4. `radiant-agent tap 'role=button label=Filled'` adds `agent` entries.
- **Native AOT.** Both publish ahead of time:
  `dotnet publish src/Radiant.AgentCli -c Release -r osx-arm64` and the same for the gallery. The
  published CLI can launch and drive the published gallery.
