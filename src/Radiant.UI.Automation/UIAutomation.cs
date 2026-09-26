using Radiant.Host.AgentControlProtocol;
using Radiant.UI.Core;

namespace Radiant.UI.Automation;

/// <summary>How a <see cref="UIAutomation"/> works.</summary>
public sealed record UIAutomationOptions
{
    /// <summary>The app's name, for <c>app.info</c> and the log.</summary>
    public string? AppName { get; init; }

    /// <summary>The instance's name, when it's served.</summary>
    public string? InstanceName { get; init; }

    /// <summary>The log to record to; one in memory if null.</summary>
    public InteractionLog? Log { get; init; }
}

/// <summary>
/// Puts a running UI app under automation: an <see cref="AgentDispatcher"/> whose <c>ui.*</c>,
/// <c>app.*</c> and <c>log.*</c> actions find elements, act on them as a person would (waiting for the
/// app to settle, bringing them into view, pressing where they can be hit), inspect them, wait for them
/// and take screenshots. Commands are run at the start of each frame, on the UI thread; transports (or a
/// test, directly) queue them on <see cref="Dispatcher"/>.
/// </summary>
public sealed class UIAutomation : IDisposable
{
    private FrameCapture? _capture;
    private int _shots;

    /// <summary>Automates <paramref name="session"/>'s app.</summary>
    public UIAutomation(UIAppSession session, UIAutomationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        options ??= new UIAutomationOptions();
        Session = session;
        AppName = options.AppName;
        InstanceName = options.InstanceName;
        Dispatcher = new AgentDispatcher(session.ClockMode == UIClockMode.Fixed ? () => session.Time : null)
        {
            FrameSource = () => session.Frame,
        };
        Dispatcher.WorkArrived += session.RequestFrame;
        session.BeforeFrame += Pump;
        session.AddWorkSource(() => Dispatcher.HasPending);
        Log = options.Log ?? new InteractionLog();
        Log.Attach(this);
        UIActions.Register(this);
    }

    /// <summary>The app.</summary>
    public UIAppSession Session { get; }

    /// <summary>Where commands are queued.</summary>
    public AgentDispatcher Dispatcher { get; }

    /// <summary>What's been done to the app.</summary>
    public InteractionLog Log { get; }

    /// <summary>The app's name.</summary>
    public string? AppName { get; }

    /// <summary>The instance's name, when it's served.</summary>
    public string? InstanceName { get; set; }

    /// <summary>True while the automation is giving the app input, so the log knows it's not a person's.</summary>
    internal bool IsSynthesizing { get; private set; }

    internal void Synthesize(Action input)
    {
        Log.ActingNow();
        IsSynthesizing = true;
        try
        {
            input();
        }
        finally
        {
            IsSynthesizing = false;
        }
    }

    /// <summary>A path for the next screenshot, beside the log.</summary>
    internal string NextShotPath() =>
        Path.Combine(Log.ShotsDirectory, (++_shots).ToString("0000", System.Globalization.CultureInfo.InvariantCulture) + ".png");

    internal FrameCapture? Capture()
    {
        _capture ??= new FrameCapture();
        return _capture.TryStart() ? _capture : null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Session.BeforeFrame -= Pump;
        Dispatcher.WorkArrived -= Session.RequestFrame;
        Log.Detach();
        _capture?.Dispose();
    }

    private void Pump(double step) => Dispatcher.Pump();
}
