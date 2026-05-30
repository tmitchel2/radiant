# Radiant Scrolling & Gestures

A React-Native-inspired scroll system for Radiant's retained-mode widget tree: a composable gesture
layer for input arbitration, a shared scroll-physics core (momentum / bounce / snap / animated scroll),
a declarative `ScrollBehaviour`, and the `ScrollView` widget.

- **Animation** (`Radiant.Animation`) — reusable `SmoothDamp` spring + frame-rate-independent `Decay`.
- **Scrolling** (`Radiant.Scrolling`) — the physics core (`ScrollAxis` + `ScrollController`) and config.
- **Gestures** (`Radiant.Gestures`) — composable recognisers + a single-owner arbiter.
- **`ScrollView`** (`Radiant.UI`) — the widget that ties them together.

All of it is **opt-in and additive**: a `ScrollView` you don't create costs nothing, and the gesture
layer is only driven by widgets that own a `GestureDetector`.

## The core idea

React Native Gesture Handler / Reanimated are **event-driven and multi-pointer**. Radiant is **polled
and single-pointer** — one mutable `InputState` is walked top-down through `UIElement.Update(input, dt)`
once per frame. The collapse is clean: each frame synthesises one immutable `PointerFrame`; each gesture
runs a state machine against that one sample; a per-detector arbiter resolves a single owner.

## Animation primitives — `Radiant.Animation`

```csharp
// Critically-damped spring (Unity Mathf.SmoothDamp). Single source of truth; the Dynamis camera
// forwards to it. Drives animated scroll / snap settle / bounce return.
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

## `ScrollView` — `Radiant.UI`

`UIElement, IUiContainer, ILayoutBoundary, IAnimating`. The `ScrollController`-backed scroller (it
replaced the old `ScrollPanel`).

- **Render-time translate, not child mutation.** `Renderer2D.PushScrollOffset/PopScrollOffset` translate
  all four emitted vertex streams since the push (composing cumulatively across nesting); the clip stack
  stays in window space so the viewport doesn't move. O(1)/frame, sub-pixel safe, never corrupts the
  children's real layout positions.
- **Content-space hit-testing.** While updating children the pointer is shifted by the offset, then
  restored — so input lines up with the translated render.
- **Inputs:** wheel, drag-to-pan, a draggable scrollbar thumb + track-click paging, and keyboard
  (PageUp/Down, Home/End, arrows) while hovered.
- **Nested scrolling.** Children update first in content space; a hovered scrollable child takes
  drag/keyboard. Wheel arbitration is by **consumption** — a scroller zeroes `InputState.ScrollDelta`
  when it consumes a notch, so an unconsumed notch (inner at its boundary) hands off to the outer.
- **`IsCapturingInput`** = the detector owns/claims, OR hovering scrollable content, OR a child captures.
- **Drop-in for the old `ScrollPanel`:** settable `ContentHeight` / `WheelStep`, `Add`/`Clear`/`Children`,
  `DrawBackground`/`BackgroundColor`, `ScrollbarWidth`, `ILayoutBoundary`.

```csharp
var sv = new ScrollView(new ScrollBehaviour { Overscroll = OverscrollMode.Bounce });
sv.Add(child);                 // children positioned in panel-relative (absolute) coords
sv.ContentHeight = totalY;     // explicit extent (or leave unset to auto-measure from child bounds)
```

`ListView` is also backed by a `ScrollController` (wheel-only parity).

## Host integration

`UIManager.NeedsContinuousFrame` walks the tree for any `IAnimating` element that is currently animating
(scroll momentum, spring settle, animated scroll). The host should OR this into its idle-throttle / frame
decision so motion doesn't stall — the same way it honours a not-at-rest camera. The host also decides
"is the user interacting with UI vs the 3D scene?": a `ScrollView` reports `IsCapturingInput` while
hovering scrollable content, and an opaque host panel should additionally gate on pointer-over-bounds so
input over chrome / non-scrollable areas doesn't fall through.

## Testing

MSTest, hand-built `InputState`, driven by repeated `Update(input, dt)` at fixed dt. Physics tests are
fully deterministic (clamp, momentum rest, bounce overshoot+settle, snap, animated scrollTo); gesture
tests assert state transitions, `RequireToFail`, Race + sticky ownership; nested tests cover
inner-consumes-wheel and boundary hand-off. See `src/Radiant.Tests/{Animation,Scrolling,Gestures}` and
the `ScrollView*` / `ListView` UI tests.

## Deferred (why deferred → trigger to revisit)

| Deferred | Why | Revisit when |
|---|---|---|
| `WheelGesture` as a first-class gesture | Wheel is instantaneous; direct poll + `ScrollDelta`-consumption already gives nested hand-off | Wheel needs `RequireToFail`/`Simultaneous` relations |
| True multi-owner `Simultaneous` | Arbiter keeps one primary owner; `Simultaneous` only blocks mutual cancel | A multi-pointer / pan+zoom surface lands |
| Directional-lock / `FailOffset` on `PanGesture` | Axis threshold-gate suffices for V/H panels | A 2D free-scroll surface needs single-axis lock-in |
| Drag (not just wheel) nested boundary hand-off | Wheel hand-off done; drag hand-off needs cross-detector negotiation | A nested draggable region is shipped |
| Cross-detector child-click vs parent-drag cancel | Arbitration is per-detector; press-then-drag-off-a-child doesn't yet cancel the child's click | A flick-scroll list of clickable rows is needed |
| Yoga-measured content extent | Child-bounds measure + `ContentExtentOverride` cover callers | A `ScrollView` hosts a flex subtree whose extent isn't max-child-bottom |
| List virtualization | Render-translate + clip cull cheaply for hundreds of rows | A ≥thousands-row consumer shows vertex-build cost |
| Sticky headers / pinch-zoom / contentInset | Section model + 2nd translate / multi-pointer / desktop has no safe-area | Grouped list / touch host / overlay embedding |
| Horizontal-axis & `Both` indicator + keyboard polish | Physics is axis-agnostic; indicator/keyboard are vertical-first | A horizontal scroller ships in a real panel |
