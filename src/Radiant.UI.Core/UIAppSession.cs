using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Radiant.Graphics2D;
using Radiant.Platform;
using Radiant.Text;

namespace Radiant.UI.Core;

/// <summary>How a <see cref="UIAppSession"/> tells time.</summary>
public enum UIClockMode
{
    /// <summary>The wall clock: each frame advances by the time since the last, capped so a stall doesn't skip an animation.</summary>
    Real,

    /// <summary>
    /// A virtual clock: each frame advances exactly <see cref="UIAppSession.FixedStep"/>, however long it
    /// took, so a run is the same every time and a transition takes the same number of frames.
    /// </summary>
    Fixed,
}

/// <summary>
/// Something that plugs into a running UI app (<see cref="UIAppOptions.Extensions"/>): an automation
/// server, a recorder. It's attached once the session exists and disposed when the app ends.
/// </summary>
public interface IUIAppExtension : IDisposable
{
    /// <summary>Starts working with <paramref name="session"/>, on the UI thread.</summary>
    void Attach(UIAppSession session);
}

/// <summary>
/// A UI app's live state, whoever runs its frames: a window (<see cref="RadiantUI.Run"/>), the headless
/// loop (<see cref="UIAppOptions.Headless"/>), or a test stepping it by hand (<see cref="CreateManual"/>).
/// It owns the <see cref="UIRoot"/> and its platform, keeps the clock and the frame count, and runs
/// each frame in order: work posted from other threads, <see cref="BeforeFrame"/>, the root's advance
/// and update, then <see cref="AfterUpdate"/>. Everything but <see cref="Post"/>,
/// <see cref="RequestFrame"/> and <see cref="Exit"/> is for the UI thread.
/// </summary>
public sealed class UIAppSession : IDisposable
{
    /// <summary>The longest step one frame takes on the real clock.</summary>
    public const double MaxStep = 1.0 / 20;

    /// <summary>The step each frame takes on the fixed clock.</summary>
    public const double FixedStep = 1.0 / 60;

    private readonly PlatformBinding _binding;
    private readonly Element _element;
    private readonly ConcurrentQueue<Action> _posted = new();
    private readonly List<Func<bool>> _workSources = [];
    private readonly List<IUIAppExtension> _extensions = [];
    private readonly Stopwatch _wall = Stopwatch.StartNew();
    private double _time;
    private bool _disposed;

    /// <summary>A session showing <paramref name="element"/> as <paramref name="options"/> say; no platform yet.</summary>
    public UIAppSession(Element element, UIAppOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(element);
        Options = options ?? new UIAppOptions();
        _element = element;
        ClockMode = Options.Clock;
        Size = new Vector2(Options.Width, Options.Height);
        PixelScale = Options.PixelScale ?? 1f;
        Root = new UIRoot(element, Options.Fonts);
        if (ClockMode == UIClockMode.Fixed)
        {
            Root.Clock = () => _time;
        }
        _binding = new PlatformBinding(Root);
    }

    /// <summary>How it was started.</summary>
    public UIAppOptions Options { get; }

    /// <summary>The UI.</summary>
    public UIRoot Root { get; }

    /// <summary>The platform attached, once it is.</summary>
    public IPlatform? Platform => _binding.Platform;

    /// <summary>How it tells time.</summary>
    public UIClockMode ClockMode { get; }

    /// <summary>Whether it runs without a window.</summary>
    public bool IsHeadless => Options.Headless;

    /// <summary>Seconds of UI time: the sum of every frame's step.</summary>
    public double Time => _time;

    /// <summary>How many frames it has run.</summary>
    public long Frame { get; private set; }

    /// <summary>The UI's size, in logical pixels: the window's content, or what the headless loop was given.</summary>
    public Vector2 Size { get; set; }

    /// <summary>Pixels per logical pixel: 2 on a Retina display.</summary>
    public float PixelScale { get; set; }

    /// <summary>Where the window's content is on the screen, in logical pixels; zero without a window.</summary>
    public Vector2 WindowPosition { get; set; }

    /// <summary>What shows behind the UI.</summary>
    public Vector4 Background => Options.Background;

    /// <summary>The fonts text is set in.</summary>
    public FontLibrary Fonts => Root.Fonts;

    /// <summary>Whether <see cref="Exit"/> was called.</summary>
    public bool ExitRequested { get; private set; }

    /// <summary>What wakes the loop running it; set by that loop. From any thread.</summary>
    public Action? WakeUp { get; set; }

    /// <summary>What closes the loop running it; set by that loop.</summary>
    public Action? CloseRequested { get; set; }

    /// <summary>Raised at the start of each frame, on the UI thread, with the step it will take.</summary>
    public event Action<double>? BeforeFrame;

    /// <summary>Raised each frame once the tree is updated and laid out, on the UI thread.</summary>
    public event Action? AfterUpdate;

    /// <summary>Raised when the app ends, before its extensions are disposed.</summary>
    public event Action? Exiting;

    /// <summary>
    /// Whether the loop should run a frame: the tree has work, something was posted, or a work source
    /// (<see cref="AddWorkSource"/>) says so.
    /// </summary>
    public bool NeedsFrame
    {
        get
        {
            if (Root.NeedsUpdate || !_posted.IsEmpty)
            {
                return true;
            }
            foreach (var source in _workSources)
            {
                if (source())
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Whether frames are needed for anything but continuous tickers: the headless loop on a fixed
    /// clock runs only while this is so, and otherwise leaves spinners where they are.
    /// </summary>
    public bool HasWork
    {
        get
        {
            if (!Root.IsIdle || !_posted.IsEmpty)
            {
                return true;
            }
            foreach (var source in _workSources)
            {
                if (source())
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>A session driven by hand with <see cref="Step"/>, headless and on the fixed clock: for tests.</summary>
    public static UIAppSession CreateManual(Element element, Vector2 size, float pixelScale = 1f, IPlatform? platform = null, FontLibrary? fonts = null)
    {
        var session = new UIAppSession(element, new UIAppOptions
        {
            Width = (int)size.X,
            Height = (int)size.Y,
            PixelScale = pixelScale,
            Fonts = fonts,
            Headless = true,
            Clock = UIClockMode.Fixed,
        });
        session.Size = size;
        session.AttachPlatform(platform ?? new HeadlessPlatform());
        return session;
    }

    /// <summary>Attaches the platform: the root is given it (<see cref="PlatformContext"/>), and the extensions start.</summary>
    public void AttachPlatform(IPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(platform);
        _binding.Attach(platform);
        Root.SetRoot(PlatformContext.Platform.Provide(platform, _element));
        foreach (var extension in Options.Extensions)
        {
            AddExtension(extension);
        }
    }

    /// <summary>Attaches an extension now, disposing it with the session.</summary>
    public void AddExtension(IUIAppExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        _extensions.Add(extension);
        extension.Attach(this);
    }

    /// <summary>Keeps frames coming while <paramref name="hasWork"/> is true: an extension waiting on the UI.</summary>
    public void AddWorkSource(Func<bool> hasWork)
    {
        ArgumentNullException.ThrowIfNull(hasWork);
        _workSources.Add(hasWork);
    }

    /// <summary>Runs <paramref name="action"/> on the UI thread at the start of the next frame, and asks for one. From any thread.</summary>
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _posted.Enqueue(action);
        RequestFrame();
    }

    /// <summary>Asks the loop for a frame. From any thread.</summary>
    public void RequestFrame() => WakeUp?.Invoke();

    /// <summary>Asks the loop to end the app. From any thread.</summary>
    public void Exit()
    {
        ExitRequested = true;
        CloseRequested?.Invoke();
        RequestFrame();
    }

    /// <summary>
    /// The step the next frame takes: on the fixed clock <see cref="FixedStep"/>, on the real one the time
    /// since the last frame, capped at <see cref="MaxStep"/>.
    /// </summary>
    public double NextStep(double elapsedSeconds) => ClockMode == UIClockMode.Fixed ? FixedStep : Math.Min(Math.Max(elapsedSeconds, 0), MaxStep);

    /// <summary>
    /// Runs one frame on the UI thread, taking <paramref name="step"/> seconds: <see cref="BeginFrame"/>
    /// then <see cref="EndFrame"/>. Painting is the loop's to do after.
    /// </summary>
    public void RunFrame(double step)
    {
        BeginFrame(step);
        EndFrame();
    }

    /// <summary>
    /// The first half of a frame: posted work, <see cref="BeforeFrame"/>, and the root's advance by
    /// <paramref name="step"/> seconds. A window runs it on each tick of its loop.
    /// </summary>
    public void BeginFrame(double step)
    {
        while (_posted.TryDequeue(out var action))
        {
            action();
        }
        BeforeFrame?.Invoke(step);
        _time += step;
        Root.Advance(step);
    }

    /// <summary>
    /// The second half: the root's update at <see cref="Size"/>, the platform's catch-up, then
    /// <see cref="AfterUpdate"/>. A window runs it when it draws.
    /// </summary>
    public void EndFrame()
    {
        Root.Update(Size);
        _binding.AfterUpdate();
        Frame++;
        AfterUpdate?.Invoke();
    }

    /// <summary>Runs <paramref name="frames"/> frames on the fixed step: for tests driving it by hand.</summary>
    public void Step(int frames = 1)
    {
        for (var i = 0; i < frames; i++)
        {
            RunFrame(ClockMode == UIClockMode.Fixed ? FixedStep : NextStep(_wall.Elapsed.TotalSeconds));
        }
    }

    /// <summary>Draws the UI with <paramref name="renderer"/>, whose frame has begun.</summary>
    public void Paint(Renderer2D renderer) => Root.Paint(renderer);

    /// <summary>Ends the app: <see cref="Exiting"/>, then the extensions, the root and the platform go.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Exiting?.Invoke();
        for (var i = _extensions.Count - 1; i >= 0; i--)
        {
            _extensions[i].Dispose();
        }
        _binding.Dispose();
        Root.Dispose();
    }
}
