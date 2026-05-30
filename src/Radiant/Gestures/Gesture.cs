using System;
using System.Collections.Generic;
using System.Numerics;
using Radiant.Input;

namespace Radiant.Gestures;

/// <summary>
/// Base class for recognised gestures. A gesture observes one <see cref="PointerFrame"/>
/// per frame and advances its own recognition; the owning <see cref="GestureArbiter"/>
/// decides which single gesture becomes the active owner, honouring cross-gesture
/// relations (<see cref="RequireToFail"/>, <see cref="Blocks"/>, <see cref="SimultaneousWith"/>).
/// </summary>
public abstract class Gesture
{
    private protected Gesture(GestureKind kind) => Kind = kind;

    public GestureKind Kind { get; }
    public GestureState State { get; internal set; } = GestureState.Idle;

    /// <summary>Current pointer position.</summary>
    public Vector2 Position { get; protected set; }

    /// <summary>Pointer position when recognition began.</summary>
    public Vector2 StartPosition { get; protected set; }

    /// <summary>Accumulated pointer translation since recognition began.</summary>
    public Vector2 Translation { get; protected set; }

    /// <summary>Pointer velocity (px/sec).</summary>
    public Vector2 Velocity { get; protected set; }

    /// <summary>This frame's raw pointer delta.</summary>
    public Vector2 FrameDelta { get; protected set; }

    /// <summary>This frame's elapsed time (seconds).</summary>
    public double Dt { get; protected set; }

    /// <summary>True the frame the gesture would like to claim ownership.</summary>
    public bool WantsActivate { get; protected set; }

    /// <summary>Began or Active.</summary>
    public bool IsActive => State is GestureState.Began or GestureState.Active;

    /// <summary>Instantaneous gestures (e.g. tap) complete in the frame they activate and never retain ownership.</summary>
    internal virtual bool Instantaneous => false;

    internal bool WantsEnd { get; private protected set; }

    internal readonly List<Gesture> RequiredToFail = [];
    internal readonly List<Gesture> Blocked = [];
    internal readonly List<Gesture> Simultaneous = [];

    public Action<Gesture>? OnBegin { get; set; }
    public Action<Gesture>? OnChange { get; set; }
    public Action<Gesture>? OnEnd { get; set; }
    public Action<Gesture>? OnCancel { get; set; }

    /// <summary>
    /// Optional per-gesture hit area. When set, the gesture treats a frame as "inside"
    /// only when the pointer is within this area (e.g. a scrollbar thumb), overriding the
    /// detector-level inside flag. When null, the detector's flag is used.
    /// </summary>
    public Func<Vector2, bool>? HitArea { get; set; }

    /// <summary>This gesture will not activate until <paramref name="other"/> has failed.</summary>
    public Gesture RequireToFail(Gesture other) { RequiredToFail.Add(other); return this; }

    /// <summary>When this gesture activates, <paramref name="other"/> is cancelled.</summary>
    public Gesture BlocksGesture(Gesture other) { Blocked.Add(other); return this; }

    /// <summary>This gesture may be active at the same time as <paramref name="other"/>.</summary>
    public Gesture SimultaneousWith(Gesture other) { Simultaneous.Add(other); return this; }

    /// <summary>Advance recognition for one frame. Must not transition into ownership — that is the arbiter's role.</summary>
    internal abstract void Observe(in PointerFrame frame, bool inside);

    // ---- Arbiter-driven transitions ----

    internal void Grant()
    {
        State = GestureState.Began;
        WantsActivate = false;
        OnBegin?.Invoke(this);
    }

    internal void Continue()
    {
        if (State == GestureState.Began) State = GestureState.Active;
        OnChange?.Invoke(this);
    }

    internal void EndActive()
    {
        State = GestureState.Ended;
        OnEnd?.Invoke(this);
    }

    internal void CancelActive()
    {
        if (IsActive) OnCancel?.Invoke(this);
        State = GestureState.Cancelled;
    }

    internal void Fail() => State = GestureState.Failed;

    /// <summary>Clear terminal states back to Idle at the start of a frame, resetting tracking.</summary>
    internal void ClearTerminal()
    {
        if (State is GestureState.Ended or GestureState.Failed or GestureState.Cancelled)
        {
            State = GestureState.Idle;
            Translation = Vector2.Zero;
            Velocity = Vector2.Zero;
            WantsActivate = false;
            WantsEnd = false;
        }
    }
}
