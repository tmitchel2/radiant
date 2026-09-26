# Radiant Host — a native desktop application shell

`Radiant` draws a window. `Radiant.Host` turns one executable into a desktop application: a
Chrome-style tabbed window whose tabs are separate processes, tear-off and merge between windows, one
Dock tile for all of them, a native File menu, recent files, headless GPU rendering for tests, and a
filesystem control protocol an agent or a CLI can drive. UI apps answer the same protocol over a socket
as well, for automation: see [automation.md](automation.md). `tools/bundle-macos-app.sh` and
`src/Directory.Publish.props` take the result to a Native AOT `.app`.

Three assemblies:

| Assembly | What it holds | References |
|---|---|---|
| `Radiant.Host` | the compositing host (`LiveHost`, `HostCompositor`, the tab strip and its gestures), the drag overlay, the renderer half (`TabSession`), macOS integration, `RadiantAppIdentity` | `Radiant`, the two below |
| `Radiant.Host.Ipc` | the data plane: `SharedFrameBuffer`, `InputRing`, `TabProtocol`, `RecentFilesStore` | nothing |
| `Radiant.Host.AgentControlProtocol` | the control plane: `InstanceRegistry`, the `AgentDispatcher`, the file and socket transports, `AgentClient`, `AgentLauncher`, selectors and log entries | nothing |

A renderer that only publishes frames needs `Radiant.Host.Ipc`; a CLI that only sends commands needs
`Radiant.Host.AgentControlProtocol`. Neither needs a window.

## The process model

Native window tabbing is same-process only, and cross-process window reparenting is unsupported by
GLFW. So the host follows Chrome: **renderer processes own no windows**. Each renders its tab into an
off-screen target and publishes the pixels to shared memory; one **host** process owns the window and
the tab strip and composites the active tab's frame under the strip.

All of it is **one executable selected by `--type`**:

- `--type host` → `HostEntry.Run(args, identity)`: the window. `--name` sets its instance name (default
  `<prefix>-host`); `--follow` starts it hidden, for a live tear-off.
- `--type drag-overlay` → `DragOverlayEntry.Run(args, identity)`: the transparent, click-through window
  that carries a dragged tab's thumbnail between host windows.
- `--attach --name <name> [--scene <path>]` → the application's own renderer, which appears as a tab
  through `TabSession`. This flag is the convention of `RadiantAppIdentity.RelaunchAsTab`; an
  application that renders tabs differently supplies its own `TabLaunch`.
- a plain launch → the application's choice. The usual one is: become a renderer tab and call
  `HostSpawn.EnsureRunning(identity)`, so launching the app opens a window with a tab in it.

Every spawn relaunches the *calling* executable through `SelfRelaunch`, which resolves the right
binary under Native AOT, single-file, a framework-dependent apphost and `dotnet run`. That is what
makes each application's windows carry its own name and icon.

Headless verification sub-modes of `--type host`: `--selftest [--out <png>]` draws the strip with no
window; `--compose-once --instance <name>` composites one published frame to a PNG; `--input-test
--instance <name>` round-trips an input event through a renderer's ring; `--control-test` exercises
the `tab.*` actions against the live registry.

## Two planes

**Control plane (low rate): the filesystem.** `InstanceRegistry` keeps
`<data directory>/instances/<name>/instance.json` per live process, with PID-liveness checks and
automatic cleanup of the dead (`$RADIANT_INSTANCES_DIR` moves it, to keep test runs apart).
`CommandClient` writes `commands/cmd-<id>.json`; the target's `FileDropTransport` (a
`CommandReceiver` watching with a `FileSystemWatcher`) queues each command on an `AgentDispatcher`,
which the target pumps once a frame on its own thread, and the answer is written to `responses/`. UI
apps also listen on a Unix domain socket, which is faster and can push events
([automation.md](automation.md)). The host registers as an instance of kind `host` with the `tab`
capability; `TabController.Register` puts these actions on its dispatcher:

| Action | Description |
|---|---|
| `tab.list` | open tabs and the active index |
| `tab.activate` | activate by `index` or `name` |
| `tab.spawn` | `dotnet run` a worktree build with `--attach` as a new tab. `buildPath` (alias `project`): a `.csproj`, a project directory, or a worktree root, which resolves through the identity's `WorktreeProject`. Optional `name`, `scenes` |
| `tab.close` | close by `index` or `name`, terminating its renderer |
| `tab.detach` | tear a tab out into a new host window |
| `tab.adopt` | take a renderer owned by another host into this strip; optional `cursorX` picks the slot |
| `tab.handoff` | give a tab this host owns to another host (the source side of a live re-merge) |
| `window.focus` | raise this host's window (the Dock menu uses it) |
| `actions.list` | the above (the dispatcher's own) |

Each renderer registers under its own name, so an application's own actions address each tab
directly. An application serialises its action results through its own
`JsonSerializerContext`, combined with `AgentJsonContext` by `JsonTypeInfoResolver.Combine`;
`AgentJsonContext` carries only the protocol and the generic `BoolResult`, `ExitResult` and
`ScreenshotResult`.

**Data plane (per frame, active tab): shared memory.** Both transports are file-backed
memory-mapped files under the renderer's instance directory — .NET named shared memory throws on
macOS and Linux — synchronised by interlocked counters, with no native semaphores, so they are
AOT-safe.

- `SharedFrameBuffer` (`frames.bin`, magic `RAD1`): triple-buffered, single producer and single
  consumer, newest wins. The host uploads the active tab's newest frame to a texture each frame.
- `InputRing` (`input.bin`, magic `RIN1`): a ring of discrete events (buttons, keys, scroll, chars)
  plus a latest-value block (pointer, desired size, focus, active). The host writes the active tab's
  input, offset below the strip.

The transport is a **CPU readback bounce**, not a zero-copy shared GPU surface: the bound
Silk.NET.WebGPU (wgpu-native) exposes no IOSurface/DMABUF import. One active tab at 1280×720 is about
3.7 MB a frame, which is cheap; background tabs are paused through the input ring's `Active` field.

## Protocol versioning

There are two versions. `AgentProtocol.Version` (currently **2**, in `InstanceInfo.AgentProtocolVersion`)
covers the control plane's commands, transports and the UI actions; see
[automation.md](automation.md). `TabProtocol.Version` (currently **3**) stamps the contract: both layouts and the `tab.*` actions. A
renderer advertises it in `InstanceInfo.ProtocolVersion`; the host adopts a renderer only when it
publishes frames **and** speaks the same version, and logs and skips it otherwise without opening its
buffer. The magic and version words in each transport are a second guard. Bump `Version` on any
incompatible change to either layout or the action contract.

History: v2 added the input ring's `Active` field; v3 marks the move of these transports into Radiant
and the change of their magic words (`DYN1`/`DIN1` → `RAD1`/`RIN1`), so a pre-move build is refused
rather than adopted and then failed.

## Windows are processes

Radiant is single-window, so a second window is a second host **process**. A renderer does not care
which host reads its frames, so this costs nothing and isolates window crashes.

- **Ownership** (`TabOwnership`): a renderer's `owner.txt` names the host that owns it. The primary
  host adopts every tab whose owner is unset or itself; a secondary host adopts only tabs assigned to
  it. The primary reclaims tabs whose owner died — but not within an 8 s grace after assignment, while
  a torn-off host is still booting.
- **Tear-off** (`TabController.DetachTab`, `tab.detach`, or dragging a tab off the strip): assign the tab
  to a fresh `<prefix>-host-<pid>-<n>`, spawn that host with `--follow`, drop the tab locally.
- **Merge**: dropping a dragged tab on another window's strip hands it over with `tab.adopt`.
- **A host is never left with zero tabs**: tearing off a host's only tab is refused, and closing the
  last tab closes the window.
- **Arbitration**: `HostLock` makes a racing pair of plain launches start one primary host, not two;
  `overlay.lock` does the same for the drag overlay.

## macOS integration

Every macOS call goes through the Objective-C runtime from C# (`MacObjc`, `[UnmanagedCallersOnly]`
callbacks added to GLFW's app delegate with `class_addMethod`) — no native code, and AOT-safe.

- **One Dock tile for many hosts.** `DockOwnerLock` elects one host to keep the `Regular` activation
  policy, set the icon (`MacDockIcon`) and install the Dock menu (`MacDockMenu`), which lists every
  host by its active tab and raises the chosen one with `window.focus`. The others become accessory
  apps. If the owner dies, a survivor promotes itself. `<prefix>_DOCK_CONSOLIDATE=0` restores one tile
  per host.
- **File menu** (`MacMainMenu`): New (only when the identity has `NewDocument`), Open… (`MacOpenPanel`,
  filtered to `OpenFileExtensions`), Open Recent (`RecentFilesStore`), Close Tab, Close Window. Opening
  spawns a new tab through `TabLaunch`.
- **Dock icon** at runtime from `RadiantAppIdentity.DockIcon`, because neither `dotnet run` nor a bare
  Native AOT binary is a bundle.

## Application identity

`RadiantAppIdentity` holds everything the shell would otherwise hard-code about one application:

| Member | Used for | `Default` |
|---|---|---|
| `Name` | window title, log prefix | `Radiant` |
| `DataDirectory` | the registry (`instances/`), locks, `drag.json`, `recent.json` | `~/.radiant` |
| `InstancePrefix` | `<prefix>-host`, `<prefix>-<pid>`, `<prefix>-drag-overlay` | `radiant` |
| `EnvironmentPrefix` | `<prefix>_DRAG_DEBUG`, `_DRAG_OVERLAY`, `_DOCK_CONSOLIDATE` | `RADIANT` |
| `DockIcon` | PNG bytes for the Dock | Radiant's icon |
| `OpenFileExtensions` | File ▸ Open… filter | any file |
| `NewDocument` | File ▸ New: create a document, return its path | none (item hidden) |
| `TabLaunch` | start info for a renderer tab | `RelaunchAsTab` |
| `WorktreeProject` | `tab.spawn` given a worktree root | none |

**The identity is process-wide, and every process of an application must install the same one**: the
processes find each other only through `DataDirectory`. `HostEntry.Run`, `DragOverlayEntry.Run` and
`HostSpawn.EnsureRunning` install the identity they are given; a renderer calls
`RadiantAppIdentity.Use` before `TabSession`, and a CLI that references only the control protocol sets
`InstanceRegistry.RootDir` to `identity.InstancesDirectory`. Build the identity with `with` from
`Default` so a new member gets its default:

```csharp
// Illustrative: an application's identity.
public static readonly RadiantAppIdentity Identity = RadiantAppIdentity.Default with
{
    Name = "My App",
    DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".myapp"),
    InstancePrefix = "myapp",
    EnvironmentPrefix = "MYAPP",
    DockIcon = () => MacDockIcon.LoadEmbeddedPng(typeof(Program).Assembly, "MyApp.Resources.icon.png"),
    OpenFileExtensions = ["txt"],
};
```

## A minimal application

```csharp
// Illustrative: Program.Main of an application built on the host.
static int Main(string[] args)
{
    RadiantAppIdentity.Use(MyIdentity.Identity);
    return TypeOf(args) switch
    {
        "host" => HostEntry.Run(args, MyIdentity.Identity),
        "drag-overlay" => DragOverlayEntry.Run(args, MyIdentity.Identity),
        _ => RunTab(args),   // HostSpawn.EnsureRunning(identity), then a TabSession render loop
    };
}
```

The renderer half is `TabSession`: `TabSession.Attach(TabAttachOptions)` registers the instance at the
current protocol version and creates its frame buffer; each frame the application calls `Pump` for the
host's input and the size and scale it wants, renders off-screen with `HeadlessGpu` +
`OffscreenReadback` (both in `Radiant.Graphics2D`), and hands the pixels to `Publish`. The frame is
rendered for the same sRGB format the host's window uses and holds premultiplied alpha, which is what
`Texture2D` expects, so compositing it changes no pixel (see [rendering.md](rendering.md)). `OwningHostAlive` tells a tab its window has
gone.

## Packaging

- `src/Directory.Publish.props` — import it from the application's csproj for the Native AOT publish
  settings (self-contained, source-generated JSON only, stripped symbols, the osx `-ld_classic`
  linker workaround) and a trimmed single-file JIT fallback.
- `tools/bundle-macos-app.sh --name <Name> --executable <exe> --bundle-id <id> --publish-dir <dir>
  --out <dir> [--icon <icns>] [--version <v>] [--category <type>]` — wraps a publish in a `.app`
  (Info.plist, `Contents/MacOS`, `Contents/Resources`) so LaunchServices starts it as a GUI app rather
  than handing it to Terminal. Unsigned.
- `tools/generate_app_icon.py` regenerates Radiant's own `icon.png` and `Radiant.icns`.

## Open questions

- **Zero-copy frames.** Blocked on wgpu-native exposing external-memory import; the bounce stays until
  it does.
- **Linux and Windows.** The host builds and its logic is tested everywhere, but the drag overlay's
  focus behaviour and the whole multi-window flow have only been verified on macOS.
  `<prefix>_DRAG_OVERLAY=0` is the kill-switch.
- **One identity per process** is a consequence of the registry root being process-wide. A process
  that needed to act for two applications would need the registry to take its root per call.
