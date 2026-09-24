using Radiant.Host.AgentControlProtocol;
using Radiant.Host.Ipc.Frames;
using Radiant.Host.Ipc.Input;

namespace Radiant.Host.Tests;

/// <summary>
/// The renderer half of the tab protocol, which until now existed only inside InteractiveDemo and
/// so could not be used by any other binary. Drives it against a real frame buffer and
/// a real input ring, with this test standing in for the host.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class TabSessionTests
{
    private string _root = null!;
    private string _previousRoot = null!;

    [TestInitialize]
    public void Setup()
    {
        _previousRoot = InstanceRegistry.RootDir;
        _root = Path.Combine(Path.GetTempPath(), $"radiant-tab-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        InstanceRegistry.RootDir = _root;
    }

    [TestCleanup]
    public void Cleanup()
    {
        InstanceRegistry.RootDir = _previousRoot;

        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    public void AttachRegistersAnInstanceAndCreatesAFrameBuffer()
    {
        using var session = TabSession.Attach(Options("tab-one"));

        var instance = InstanceRegistry.GetInstance("tab-one");

        Assert.IsNotNull(instance);
        Assert.AreEqual(Environment.ProcessId, instance.Pid);
        Assert.AreEqual(Ipc.TabProtocol.Version, instance.ProtocolVersion);
        Assert.IsTrue(File.Exists(Path.Combine(_root, "tab-one", "frames.bin")));
    }

    /// <summary>
    /// "tab" marks a compositing host, not a tab. A launcher decides whether to spawn a host by
    /// asking whether any instance advertises it, so a renderer claiming it suppresses the very
    /// host it is waiting for.
    /// </summary>
    [TestMethod]
    public void ARendererMayNotAdvertiseTheTabCapability()
    {
        var options = Options("tab-two");
        options.Capabilities = ["scene", "tab"];

        _ = Assert.ThrowsExactly<ArgumentException>(() => TabSession.Attach(options));
    }

    [TestMethod]
    public void APublishedFrameIsReadableByTheHost()
    {
        using var session = TabSession.Attach(Options("tab-three"));

        var pixels = new byte[4 * 2 * 4];
        Array.Fill(pixels, (byte)0x7F);

        session.Publish(pixels, 4, 2);

        using var reader = SharedFrameBuffer.OpenReader(
            Path.Combine(_root, "tab-three", "frames.bin"));

        var destination = new byte[pixels.Length];

        Assert.IsTrue(reader.TryRead(destination, out var info));
        Assert.AreEqual(4, info.Width);
        Assert.AreEqual(2, info.Height);
        Assert.AreEqual(FrameFormat.Bgra8Unorm, info.Format);
        CollectionAssert.AreEqual(pixels, destination);
    }

    /// <summary>The ring is the host's to create, so a tab nobody has adopted simply has no input.</summary>
    [TestMethod]
    public void PumpBeforeAdoptionReportsNothingAndDoesNotThrow()
    {
        using var session = TabSession.Attach(Options("tab-four"));

        Assert.IsFalse(session.Pump(out var events));
        Assert.AreEqual(0, events.Count);
        Assert.IsFalse(session.Adopted);
    }

    [TestMethod]
    public void PumpReadsTheSizeScaleAndEventsTheHostWrote()
    {
        using var session = TabSession.Attach(Options("tab-five"));

        using var host = InputRing.CreateWriter(Path.Combine(_root, "tab-five", "input.bin"));

        host.SetSize(800, 480);
        host.SetScale(2f);
        host.SetFocus(true);
        host.SetActive(true);
        host.SetMousePosition(12f, 34f);
        host.Push(new InputEvent(InputEventKind.MouseDown, 0, 12f, 34f));
        host.Push(new InputEvent(InputEventKind.Char, 'x', 0f, 0f));

        Assert.IsTrue(session.Pump(out var events), "the size and scale changed");

        Assert.IsTrue(session.Adopted);
        Assert.AreEqual(800, session.LogicalWidth);
        Assert.AreEqual(480, session.LogicalHeight);
        Assert.AreEqual(2f, session.Scale, 0.001f);
        Assert.IsTrue(session.Active);
        Assert.IsTrue(session.Focused);
        Assert.AreEqual(12f, session.MouseX, 0.001f);

        Assert.AreEqual(2, events.Count);
        Assert.AreEqual(InputEventKind.MouseDown, events[0].Kind);
        Assert.AreEqual(InputEventKind.Char, events[1].Kind);

        // Physical is logical times the ratio: the target is sized in real pixels. Asked of a
        // session whose buffer is big enough to hold it -- the earlier version of this assertion
        // expected 1600 from a 64-pixel buffer, which passed only because the clamp was comparing
        // pixels against bytes. A test that encodes the defect is worse than no test.
        using var roomy = TabSession.Attach(
            new TabAttachOptions
            {
                InstanceName = "tab-five-roomy",
                MaxWidth = 2048,
                MaxHeight = 1024,
                Capabilities = ["scene"],
                StateDirectory = Path.GetTempPath(),
            });

        using var writer = InputRing.CreateWriter(
            Path.Combine(_root, "tab-five-roomy", "input.bin"));

        writer.SetSize(800, 480);
        writer.SetScale(2f);

        _ = roomy.Pump(out _);

        Assert.AreEqual(1600, roomy.PhysicalWidth);
        Assert.AreEqual(960, roomy.PhysicalHeight);
    }

    [TestMethod]
    public void ASecondPumpWithNoChangeReportsNoResize()
    {
        using var session = TabSession.Attach(Options("tab-six"));
        using var host = InputRing.CreateWriter(Path.Combine(_root, "tab-six", "input.bin"));

        host.SetSize(640, 400);
        _ = session.Pump(out _);

        Assert.IsFalse(session.Pump(out var events), "nothing changed between the two calls");
        Assert.AreEqual(0, events.Count);
    }

    [TestMethod]
    public void DisposeDeregistersAndRemovesTheFrameBuffer()
    {
        var session = TabSession.Attach(Options("tab-seven"));
        var frames = Path.Combine(_root, "tab-seven", "frames.bin");

        Assert.IsTrue(File.Exists(frames));

        session.Dispose();

        Assert.IsNull(InstanceRegistry.GetInstance("tab-seven"));
        Assert.IsFalse(File.Exists(frames));
    }

    /// <summary>A renderer launched before its host waits rather than exiting.</summary>
    [TestMethod]
    public void AnUnadoptedTabConsidersItsHostAlive()
    {
        using var session = TabSession.Attach(Options("tab-eight"));

        Assert.IsTrue(session.OwningHostAlive());
    }

    /// <summary>
    /// An adopted tab with no owner marker survives while any tab-capable host is up.
    /// </summary>
    /// <remarks>
    /// <b>The test that would have caught it.</b> A plain-launch tab is composited by the primary
    /// host and carries no owner.txt; matching on a host NAME rather than on the "tab" capability
    /// makes the renderer exit about a second after it is adopted. Measured that way first: the
    /// host printed "1 tab(s): ergon-tab-probe" and the tab closed anyway.
    /// </remarks>
    [TestMethod]
    public void AnAdoptedTabWithNoOwnerSurvivesWhileATabCapableHostIsUp()
    {
        using var session = TabSession.Attach(Options("tab-nine"));
        using var host = InputRing.CreateWriter(Path.Combine(_root, "tab-nine", "input.bin"));

        _ = session.Pump(out _);
        Assert.IsTrue(session.Adopted);

        // A host, registered the way LiveHost registers itself: alive, and advertising "tab".
        InstanceRegistry.Register(new InstanceInfo
        {
            Name = "a-host",
            Pid = Environment.ProcessId,
            StartTime = DateTime.UtcNow.ToString("o"),
            Capabilities = ["tab"],
            ProtocolVersion = Ipc.TabProtocol.Version,
        });

        Assert.IsTrue(session.OwningHostAlive());
    }

    /// <summary>And it does not survive when the only instances up are other tabs.</summary>
    /// <remarks>
    /// The other half, so the assertion above is a claim rather than a coincidence: a registry with
    /// something in it that is not a host must still read as "no host".
    /// </remarks>
    [TestMethod]
    public void AnAdoptedTabDoesNotSurviveWhenNoHostIsUp()
    {
        using var session = TabSession.Attach(Options("tab-ten"));
        using var host = InputRing.CreateWriter(Path.Combine(_root, "tab-ten", "input.bin"));

        _ = session.Pump(out _);

        InstanceRegistry.Register(new InstanceInfo
        {
            Name = "another-tab",
            Pid = Environment.ProcessId,
            StartTime = DateTime.UtcNow.ToString("o"),
            Capabilities = ["scene"],
            ProtocolVersion = Ipc.TabProtocol.Version,
        });

        Assert.IsFalse(session.OwningHostAlive());
    }

    /// <summary>A frame never exceeds the buffer it was sized for, however large the host asks.</summary>
    /// <remarks>
    /// <b>The test that would have caught it.</b> The clamp compared a PIXEL COUNT against
    /// <c>SharedFrameBuffer.Capacity</c>, which is a slot's size in BYTES — so 3024 was measured
    /// against 16,384,000, never fired, and a retina display taken full screen published a frame
    /// bigger than the slot. The renderer died with "Frame 3024x1842 exceeds slot capacity".
    /// A dimension is clamped against a dimension.
    /// </remarks>
    [TestMethod]
    public void APhysicalFrameNeverExceedsTheBufferItWasSizedFor()
    {
        using var session = TabSession.Attach(Options("tab-eleven"));
        using var host = InputRing.CreateWriter(Path.Combine(_root, "tab-eleven", "input.bin"));

        // Far more than Options' 64x64, at the scale a retina display reports.
        host.SetSize(1512, 921);
        host.SetScale(2f);

        _ = session.Pump(out _);

        Assert.AreEqual(3024, session.LogicalWidth * 2, "the host asked for more than the slot holds");
        Assert.AreEqual(session.MaxWidth, session.PhysicalWidth);
        Assert.AreEqual(session.MaxHeight, session.PhysicalHeight);

        // And the frame that size actually fits, which is the property the clamp exists for.
        session.Publish(
            new byte[session.PhysicalWidth * session.PhysicalHeight * 4],
            session.PhysicalWidth,
            session.PhysicalHeight);
    }

    private static TabAttachOptions Options(string name) =>
        new()
        {
            InstanceName = name,
            Width = 320,
            Height = 200,

            // Small, so the mapping is kilobytes rather than the 100 MB a 4K triple buffer is.
            MaxWidth = 64,
            MaxHeight = 64,
            Capabilities = ["scene"],
            StateDirectory = Path.GetTempPath(),
        };
}
