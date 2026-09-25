using Radiant.Host.AgentControlProtocol;
using Radiant.Host.Ipc.Frames;
using Radiant.Host.Ipc.Input;

namespace Radiant.Host;

/// <summary>
/// Everything a process must do to appear as a tab in a compositing host, so that any application
/// built on the host can be one.
/// </summary>
/// <remarks>
/// <para>
/// The renderer half of <c>docs/designs/TABS.md</c> existed only inside <c>InteractiveDemo</c> —
/// about three hundred lines of generic plumbing spread across five private members of a file that
/// is otherwise scene and CFD code. TABS.md's premise is that different binaries from different
/// worktrees can share one window; that was true of different builds of the one
/// application it was written for and of nothing else, because there was no way to do this without
/// copying it.
/// </para>
/// <para>
/// <b>What a tab actually is</b>: a process that publishes frames into
/// <c>&lt;data directory&gt;/instances/&lt;name&gt;/frames.bin</c> and registers an <see cref="InstanceInfo"/>
/// advertising a matching <see cref="Radiant.Host.Ipc.TabProtocol"/> version. The host's rescan
/// adopts anything meeting both conditions. There is no handshake beyond that; the control plane is
/// the filesystem.
/// </para>
/// <para>
/// <b>The host owns the input ring and creates it late</b> — only once it has adopted this tab — so
/// it is opened lazily and a failure to open is a normal state rather than an error. Its mouse
/// position, desired size, scale, focus and active flag are latest-value; button, key, scroll and
/// char events are queued and must be drained.
/// </para>
/// <para>
/// <b>A tab dies with the host that composites it.</b> Chrome's lifetime, and the reason is
/// practical: a renderer whose window has gone is a process holding a terminal open for nothing.
/// </para>
/// </remarks>
public sealed class TabSession : IDisposable
{
    private static readonly TimeSpan s_liveness = TimeSpan.FromSeconds(1);

    // Spans the tear-off handoff gap, mirroring the host-side reclaim grace in TabController
    // so neither side acts on the other before the new owner registers.
    private static readonly TimeSpan s_handoff = TimeSpan.FromSeconds(10);

    private readonly string _name;
    private readonly string _framesPath;
    private readonly SharedFrameBuffer _frames;

    private InputRing? _input;

    // MinValue rather than 'now', so the first call after adoption actually decides. Starting
    // the clock at construction meant the first second always answered 'alive' regardless --
    // which is not merely untestable, it is a second in which a dead host reads as live.
    private DateTime _checked = DateTime.MinValue;
    private bool _alive = true;
    private bool _disposed;

    private TabSession(
        string name,
        string framesPath,
        SharedFrameBuffer frames,
        int width,
        int height,
        int maxWidth,
        int maxHeight)
    {
        _name = name;
        _framesPath = framesPath;
        _frames = frames;
        MaxWidth = maxWidth;
        MaxHeight = maxHeight;
        LogicalWidth = width;
        LogicalHeight = height;
    }

    /// <summary>The logical content width the host last asked for.</summary>
    public int LogicalWidth { get; private set; }

    /// <summary>The logical content height the host last asked for.</summary>
    public int LogicalHeight { get; private set; }

    /// <summary>The device pixel ratio the host last reported. Never zero.</summary>
    public float Scale { get; private set; } = 1f;

    /// <summary>Whether this is the tab the host is currently showing.</summary>
    /// <remarks>
    /// A background tab should render nothing and publish nothing: the host keeps showing the last
    /// frame it read, so skipping the work costs nothing visible and is most of what makes a strip
    /// of heavyweight tabs affordable.
    /// </remarks>
    public bool Active { get; private set; }

    /// <summary>Whether the host window has keyboard focus.</summary>
    public bool Focused { get; private set; }

    /// <summary>Where the cursor is, in this tab's content pixels, origin below the strip.</summary>
    public float MouseX { get; private set; }

    /// <summary>Where the cursor is vertically.</summary>
    public float MouseY { get; private set; }

    /// <summary>The physical frame width to render at: the logical width times the scale.</summary>
    /// <remarks>
    /// <b>Clamped to the size the buffer was CREATED at, in pixels.</b> The first version clamped
    /// against <c>SharedFrameBuffer.Capacity</c>, which is a slot's size in BYTES -- so a 3024-pixel
    /// width compared against 16,384,000 and the clamp never fired. A retina display asking for its
    /// full screen then published a frame larger than the slot and the renderer died with
    /// "exceeds slot capacity". A dimension is clamped against a dimension.
    /// </remarks>
    public int PhysicalWidth => Clamp((int)MathF.Round(LogicalWidth * Scale), MaxWidth);

    /// <summary>The physical frame height.</summary>
    public int PhysicalHeight => Clamp((int)MathF.Round(LogicalHeight * Scale), MaxHeight);

    /// <summary>The largest frame width the shared buffer was sized for.</summary>
    public int MaxWidth { get; }

    /// <summary>The largest frame height.</summary>
    public int MaxHeight { get; }

    /// <summary>Whether a host has adopted this tab and is reading its frames.</summary>
    public bool Adopted => _input is not null;

    /// <summary>Creates the frame buffer and registers the instance.</summary>
    /// <param name="options">Who this tab is and how large it may get.</param>
    /// <returns>A live session; dispose it to deregister.</returns>
    public static TabSession Attach(TabAttachOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.InstanceName);

        if (options.Capabilities.Contains("tab", StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "A renderer tab must not advertise the 'tab' capability: that marks a compositing "
                + "host, and a launcher deciding whether to spawn one asks exactly this question.",
                nameof(options));
        }

        var directory = Path.Combine(InstanceRegistry.RootDir, options.InstanceName);
        _ = Directory.CreateDirectory(directory);

        var framesPath = Path.Combine(directory, "frames.bin");
        var frames = SharedFrameBuffer.CreateWriter(
            framesPath, options.MaxWidth, options.MaxHeight);

        InstanceRegistry.Register(new InstanceInfo
        {
            Name = options.InstanceName,
            Pid = Environment.ProcessId,
            StartTime = DateTime.UtcNow.ToString("o"),
            WorkingDirectory = Directory.GetCurrentDirectory(),
            StateDirectory = options.StateDirectory,
            Capabilities = options.Capabilities,

            // The host adopts a tab only when this matches its own, and logs the mismatch once.
            ProtocolVersion = Ipc.TabProtocol.Version,
        });

        return new TabSession(
            options.InstanceName,
            framesPath,
            frames,
            options.Width,
            options.Height,
            options.MaxWidth,
            options.MaxHeight);
    }

    /// <summary>
    /// Reads everything the host has said since the last call: the latest size, scale, focus and
    /// active flag, and every queued input event.
    /// </summary>
    /// <param name="events">What happened, in order. Empty until a host has adopted this tab.</param>
    /// <returns>Whether the size or the scale changed, so a caller can resize its target.</returns>
    public bool Pump(out IReadOnlyList<InputEvent> events)
    {
        events = [];

        if (_input is null)
        {
            // The host creates the ring when it adopts this tab, and may be mid-creation when we
            // look: a short read or a torn header is a normal race, not a fault.
            if (!File.Exists(Path.Combine(InstanceRegistry.RootDir, _name, "input.bin")))
            {
                return false;
            }

            try
            {
                _input = InputRing.OpenReader(
                    Path.Combine(InstanceRegistry.RootDir, _name, "input.bin"));
            }
            catch (IOException)
            {
                return false;
            }
            catch (InvalidDataException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                // A protocol mismatch. The host will not have adopted us either, so there is
                // nothing to do but keep rendering into a buffer nobody reads.
                return false;
            }
        }

        Active = _input.Active;
        Focused = _input.Focused;
        MouseX = _input.MouseX;
        MouseY = _input.MouseY;

        var resized = false;
        var scale = _input.Scale > 0f ? _input.Scale : 1f;

        if (_input.Width > 0 && _input.Height > 0
            && (_input.Width != LogicalWidth
                || _input.Height != LogicalHeight
                || MathF.Abs(scale - Scale) > 0.01f))
        {
            LogicalWidth = _input.Width;
            LogicalHeight = _input.Height;
            Scale = scale;
            resized = true;
        }

        var drained = new List<InputEvent>();

        while (_input.TryDequeue(out var next))
        {
            drained.Add(next);
        }

        events = drained;
        return resized;
    }

    /// <summary>Publishes a rendered frame. Newest wins; an unread frame is overwritten.</summary>
    /// <param name="pixels">Tightly packed BGRA, no row padding.</param>
    /// <param name="width">The frame's own width.</param>
    /// <param name="height">The frame's own height.</param>
    public void Publish(ReadOnlySpan<byte> pixels, int width, int height) =>
        _frames.Write(pixels, width, height, FrameFormat.Bgra8Unorm);

    /// <summary>
    /// Whether the compositing host responsible for this tab is still alive, checked at most once a
    /// second.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An unowned tab is not an orphaned one.</b> A tab that was torn off or merged carries an
    /// <c>owner.txt</c> naming its host; a plain-launch tab has none and is implicitly composited by
    /// the primary host — so with no marker the question is whether ANY tab-capable host is up, not
    /// whether one of a particular name is. Getting that backwards makes a renderer exit about a
    /// second after the host adopts it, which is how this was found: the host printed
    /// <c>1 tab(s)</c> and the tab closed anyway.
    /// </para>
    /// <para>
    /// <b>A named-but-absent owner gets a grace period rather than a verdict.</b> A tear-off
    /// reassigns the tab and then spawns the new host, so during the handoff the owner exists only
    /// as a marker; treating that as gone kills the renderer mid-handoff and the new window comes
    /// up empty. A genuinely dead owner's marker ages out.
    /// </para>
    /// <para>
    /// Returns true until a host has adopted this tab at all, because a renderer launched before
    /// its host must wait rather than exit.
    /// </para>
    /// </remarks>
    public bool OwningHostAlive()
    {
        if (_input is null)
        {
            return true;
        }

        // Throttled, because ListInstances walks a directory and prunes dead pids and this is asked
        // every frame -- but it returns the LAST ANSWER rather than 'true', which is the difference
        // between rate-limiting a question and lying about it.
        if (DateTime.UtcNow - _checked < s_liveness)
        {
            return _alive;
        }

        _checked = DateTime.UtcNow;
        _alive = Decide();

        return _alive;
    }

    private bool Decide()
    {
        var owner = TabOwnership.ReadOwner(_name);

        foreach (var instance in InstanceRegistry.ListInstances())
        {
            if (!InstanceRegistry.IsAlive(instance.Pid))
            {
                continue;
            }

            var matches = owner is not null
                ? string.Equals(instance.Name, owner, StringComparison.Ordinal)
                : instance.Capabilities.Contains("tab", StringComparer.Ordinal);

            if (matches)
            {
                return true;
            }
        }

        return owner is not null && TabOwnership.OwnerAssignedWithin(_name, s_handoff);
    }

    /// <summary>Deregisters the instance and removes its frame buffer.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _input?.Dispose();
        _frames.Dispose();

        InstanceRegistry.Deregister(_name);

        if (File.Exists(_framesPath))
        {
            File.Delete(_framesPath);
        }
    }

    // A frame may not exceed the slot the mapping was sized for, and a zero-sized target is not a
    // thing WebGPU will make.
    private static int Clamp(int value, int most) =>
        Math.Max(1, Math.Min(value, most));
}
