# Radiant Scrolling & Gestures

A React-Native-inspired scroll system: a shared scroll-physics core (momentum, bounce, snap,
animated scroll), a declarative `ScrollBehaviour`, and a composable gesture layer for input
arbitration.

- **Animation** (`Radiant.Animation`) — reusable `SmoothDamp` spring and frame-rate-independent `Decay`.
- **Scrolling** (`Radiant.Scrolling`) — the physics core (`ScrollAxis` and `ScrollController`) and config.
- **Gestures** (`Radiant.Gestures`) — composable recognisers and a single-owner arbiter.
- **`ScrollArea`** (`Radiant.UI.Core`) — the element that puts a scroll controller in the UI.

The gesture layer was built for the retained widget tree that `Radiant.UI.Core` replaced, and isn't
wired into the new UI yet: `ScrollArea` takes the wheel, trackpad and scroll-bar drags itself. The
plan is for UI.Core's pointer events to feed a per-pointer gesture arena built on it.

## The core idea

React Native Gesture Handler / Reanimated are **event-driven and multi-pointer**. Radiant is **polled
and single-pointer** — one mutable `InputState` is walked top-down through `UIElement.Update(input, dt)`
once per frame. The collapse is clean: each frame synthesises one immutable `PointerFrame`; each gesture
runs a state machine against that one sample; a per-detector arbiter resolves a single owner.

## Animation primitives — `Radiant.Animation`

```csharp
// Critically-damped spring (Unity Mathf.SmoothDamp). Single source of truth; an application camera
// can forward to it. Drives animated scroll / snap settle / bounce return.
offset = SmoothDamp.Step(offset, target, ref velocity, smoothTime, dt);

// Frame-rate-independent exponential momentum decay (per-ms retention: 0.998 normal, 0.99 fast).
velocity *= Decay.Factor(Decay.NormalRatePerMs, dt);
float restPoint = offset + Decay.ProjectedDistance(velocity, rate); // analytic v0/λ — predict snap landing
```

`IAnimating { bool IsAnimating }` is the continuous-frame signal (see *host integration*).

## Scroll physics — `Radiant.Scrolling`

- **`ScrollAxis`** (internal) — the single-axis kernel: offset/velocity/extents plus the per-frame
  integration step. Momentum decay, iOS rubber-band overscroll `b(x) = x·dim·c / (dim + c·x)` (saturates
  at the viewport dimension), analytic snap projection, `SmoothDamp` settle. Phases: `Idle / Dragging /
  Momentum / Animating`.
- **`ScrollController : IAnimating`** — composes 1–2 axes, applies a `ScrollBehaviour`, and exposes the
  direct input API; physics advance in `Update(dt)` (dt clamped ≤ 1/30 so a post-idle hiccup can't
  teleport the spring).

```csharp
var c = new ScrollController(new ScrollBehaviour { Overscroll = OverscrollMode.Bounce });
c.SetExtents(viewport, contentSize);     // Vector2 per axis
c.ApplyWheel(wheelDelta);                // instant (or momentum, per behaviour)
c.BeginDrag(); c.Drag(deltaPixels, dt); c.EndDrag();   // kinetic drag → momentum
c.ScrollTo(new Vector2(0, 450), animated: true);       // spring to a target
c.Update(dt);                            // integrate; raises the lifecycle events
// events: Scroll, ScrollBeginDrag, ScrollEndDrag, MomentumBegin, MomentumEnd  (ScrollMetrics payload)
```

### `ScrollBehaviour` — the declarative config (RN ScrollView prop matrix)

A `record` describing how a scroller feels: `Axes` (Vertical/Horizontal/Both), `DirectionalLock`,
`WheelStep`, `LineStep`, `WheelMomentum`, `Deceleration` (Normal/Fast/custom), `PagingEnabled`,
`Snap` (`SnapConfig`: interval / offsets / alignment / snapToStart|End / disableIntervalMomentum),
`Overscroll` (`Clamp`/`Bounce`/`Glow`), `RubberFactor`, `BounceSmoothTime`, `ScrollToSmoothTime`,
`Indicators`, `DraggableThumb`, `IndicatorFlashDuration`, `RestThreshold`, `ActivationThreshold`,
`ContentExtentOverride` (the explicit-measurement escape hatch).

## Gestures — `Radiant.Gestures`

A `PointerFrame` (`PointerFrame.From(input, dt)`) is the per-frame snapshot. A `Gesture` advances a state
machine (`Idle → Possible → Began → Active → Ended/Failed/Cancelled`), carrying `Translation`/`Velocity`/
`Position`/`FrameDelta`, with an optional per-gesture `HitArea` and cross-gesture relations.

```csharp
var pan = new PanGesture { Axis = ScrollAxes.Vertical, ActivationThreshold = 3f };
pan.OnBegin = _ => controller.BeginDrag();
pan.OnChange = g => controller.Drag(g.FrameDelta, g.Dt);
pan.OnEnd = _ => controller.EndDrag();

var tap = new TapGesture { MaxTravel = 5f };
GestureComposition.Exclusive(pan, tap);     // tap waits for pan to fail (drag ≠ tap)
var detector = new GestureDetector(pan, tap);

detector.Update(PointerFrame.From(input, dt), pointerInside);
if (detector.HasActiveOrClaimingOwner) { /* widget.IsCapturingInput derives from this */ }
```

- **`PanGesture`** (drag; activation threshold + axis gate), **`TapGesture`** (instantaneous; press-release
  within travel). The wheel is instantaneous and routed directly by `ScrollView` (no `WheelGesture` — see
  deferred items).
- **`GestureArbiter`** — resolves a single owner per frame: **sticky** (an active drag is never stolen),
  relation-eligible, priority by list order. Relations: `RequireToFail` / `BlocksGesture` /
  `SimultaneousWith`, composed via `GestureComposition.Race` / `Exclusive` / `Simultaneous`.
- **`GestureDetector`** — binds a gesture set to a widget and exposes `HasActiveOrClaimingOwner`, the
  signal a widget's `IsCapturingInput` should derive from so recognised input can't fall through.

## `ScrollArea` — `Radiant.UI.Core`

A viewport onto content larger than it (see [ui.md](ui.md#scrolling)):

- **Layout:** the content is laid out in a node inside the viewport, unconstrained along the
  scrolling axes; it's drawn and hit-tested moved by the scroll offset.
- **Input:** the wheel and trackpad scroll through the controller, passing to an enclosing area at
  a boundary; the scroll bars can be dragged, and pressing their track jumps there.
- **Frames:** the area asks the UI to animate it while momentum, bounce or an animated `ScrollTo`
  runs (`ScrollController.AnimationStarted` covers scrolls asked for in code), and listeners hear
  `Scroll` for wheel moves as well, which is how a `VirtualList` builds the rows coming into view.
- **Position** survives rebuilds; pass a `Controller` to read or set it.

## Testing

MSTest, driven at a fixed step. Physics tests are deterministic (clamp, momentum rest, bounce
overshoot and settle, snap, animated `ScrollTo`); gesture tests assert state transitions,
`RequireToFail`, and race and sticky ownership. See `src/Radiant.Tests/{Animation,Scrolling,Gestures}`,
and `ScrollTests` and `VirtualListTests` for the UI side.

## Deferred (why deferred → trigger to revisit)

| Deferred | Why | Revisit when |
|---|---|---|
| Gestures in `Radiant.UI.Core` | `ScrollArea` handles the wheel, trackpad and scroll bars itself; nothing else needs a recogniser yet | Touch, drag-to-pan content, or a pinch or long press lands |
| `WheelGesture` as a first-class gesture | The wheel is instantaneous and scroll areas pass it on at their boundaries | Wheel input needs `RequireToFail` or `Simultaneous` relations |
| True multi-owner `Simultaneous` | The arbiter keeps one primary owner | A multi-pointer pan-and-zoom surface lands |
| Directional lock on `PanGesture` | An axis threshold suffices for vertical or horizontal panels | A 2D free-scroll surface needs single-axis lock-in |
| Keyboard scrolling in `ScrollArea` | Controls inside scroll areas move focus, and focus doesn't yet scroll into view | A scroll area of plain content needs Page Up and Down |
| Sticky headers, pinch zoom, content insets | Not needed by the components so far | A grouped list, a touch host or overlay embedding |
