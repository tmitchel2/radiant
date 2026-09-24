using Radiant.Host.Ipc.Input;

namespace Radiant.Host.Ipc.Tests;

[TestClass]
public sealed class InputRingTests
{
    private string _path = null!;

    [TestInitialize]
    public void Setup()
    {
        var dir = Path.Combine(Path.GetTempPath(), "radiant-inputring-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "input.bin");
    }

    [TestCleanup]
    public void Cleanup()
    {
        var dir = Path.GetDirectoryName(_path)!;
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public void StateValuesRoundTrip()
    {
        using var writer = InputRing.CreateWriter(_path);
        using var reader = InputRing.OpenReader(_path);

        writer.SetMousePosition(12.5f, 34.25f);
        writer.SetSize(1280, 720);
        writer.SetFocus(false);

        Assert.AreEqual(12.5f, reader.MouseX);
        Assert.AreEqual(34.25f, reader.MouseY);
        Assert.AreEqual(1280, reader.Width);
        Assert.AreEqual(720, reader.Height);
        Assert.IsFalse(reader.Focused);
    }

    [TestMethod]
    public void HostPresentDefaultsToZeroBeforeAnyWrite()
    {
        using var writer = InputRing.CreateWriter(_path);
        using var reader = InputRing.OpenReader(_path);

        Assert.AreEqual(0f, reader.HostPresentMs);
        Assert.AreEqual(0, reader.HostPresentFrame);
    }

    [TestMethod]
    public void HostPresentRoundTrips()
    {
        using var writer = InputRing.CreateWriter(_path);
        using var reader = InputRing.OpenReader(_path);

        writer.SetHostPresentMs(3.5f, 42);

        Assert.AreEqual(3.5f, reader.HostPresentMs);
        Assert.AreEqual(42, reader.HostPresentFrame);
    }

    [TestMethod]
    public void ActiveDefaultsTrueAndRoundTrips()
    {
        using var writer = InputRing.CreateWriter(_path);
        using var reader = InputRing.OpenReader(_path);

        Assert.IsTrue(reader.Active); // a freshly-created ring errs toward rendering

        writer.SetActive(false);
        Assert.IsFalse(reader.Active);

        writer.SetActive(true);
        Assert.IsTrue(reader.Active);
    }

    [TestMethod]
    public void EventsDequeueInFifoOrder()
    {
        using var writer = InputRing.CreateWriter(_path);
        using var reader = InputRing.OpenReader(_path);

        writer.Push(new InputEvent(InputEventKind.MouseDown, 0, 1f, 2f));
        writer.Push(new InputEvent(InputEventKind.KeyDown, 65, 0f, 0f));
        writer.Push(new InputEvent(InputEventKind.Scroll, 0, 0f, -1.5f));

        Assert.IsTrue(reader.TryDequeue(out var e0));
        Assert.AreEqual(InputEventKind.MouseDown, e0.Kind);
        Assert.AreEqual(0, e0.Code);
        Assert.AreEqual(1f, e0.X);
        Assert.AreEqual(2f, e0.Y);

        Assert.IsTrue(reader.TryDequeue(out var e1));
        Assert.AreEqual(InputEventKind.KeyDown, e1.Kind);
        Assert.AreEqual(65, e1.Code);

        Assert.IsTrue(reader.TryDequeue(out var e2));
        Assert.AreEqual(InputEventKind.Scroll, e2.Kind);
        Assert.AreEqual(-1.5f, e2.Y);

        Assert.IsFalse(reader.TryDequeue(out _));
    }

    [TestMethod]
    public void TryDequeueIsFalseWhenEmpty()
    {
        using var writer = InputRing.CreateWriter(_path);
        using var reader = InputRing.OpenReader(_path);
        Assert.IsFalse(reader.TryDequeue(out _));
    }

    [TestMethod]
    public void RingFullDropsNewestWithoutCorruptingOlder()
    {
        using var writer = InputRing.CreateWriter(_path, capacity: 4);
        using var reader = InputRing.OpenReader(_path);

        // Push 6 into a 4-slot ring; last 2 should be dropped, first 4 intact.
        for (var i = 0; i < 6; i++)
        {
            writer.Push(new InputEvent(InputEventKind.KeyDown, i, 0f, 0f));
        }

        for (var i = 0; i < 4; i++)
        {
            Assert.IsTrue(reader.TryDequeue(out var e), $"event {i} should be present");
            Assert.AreEqual(i, e.Code);
        }
        Assert.IsFalse(reader.TryDequeue(out _), "dropped events must not appear");
    }

    [TestMethod]
    public void RingHoldsAtMostCapacityWhenNeverDrained()
    {
        using var writer = InputRing.CreateWriter(_path, capacity: 4);
        using var reader = InputRing.OpenReader(_path);

        // Drained nothing; push 6 codes 100..105 into a 4-slot ring → last 2 drop, 100..103 remain.
        for (var i = 100; i < 106; i++)
        {
            writer.Push(new InputEvent(InputEventKind.KeyUp, i, 0f, 0f));
        }

        var seen = new List<int>();
        while (reader.TryDequeue(out var e))
        {
            seen.Add(e.Code);
        }
        int[] expected = [100, 101, 102, 103];
        CollectionAssert.AreEqual(expected, seen);
    }

    [TestMethod]
    public void OpenReaderRejectsNonRingFile()
    {
        File.WriteAllBytes(_path, new byte[2048]);
        Assert.ThrowsExactly<InvalidDataException>(() => InputRing.OpenReader(_path));
    }

    [TestMethod]
    public void ConcurrentProducerConsumerPreservesOrdering()
    {
        var total = 20000;
        using var writer = InputRing.CreateWriter(_path, capacity: 256);
        using var reader = InputRing.OpenReader(_path);

        var received = new List<int>(total);
        var done = false;
        var readerThread = new Thread(() =>
        {
            while (!Volatile.Read(ref done) || received.Count < total)
            {
                if (reader.TryDequeue(out var e))
                {
                    received.Add(e.Code);
                }
            }
        });
        readerThread.Start();

        for (var i = 0; i < total; i++)
        {
            writer.Push(new InputEvent(InputEventKind.KeyDown, i, 0f, 0f));
        }
        Volatile.Write(ref done, true);
        readerThread.Join(TimeSpan.FromSeconds(30));

        // Codes received must be strictly increasing (FIFO, no reordering). Drops are allowed under
        // contention, but order must hold.
        for (var i = 1; i < received.Count; i++)
        {
            Assert.IsTrue(received[i] > received[i - 1], $"out-of-order at {i}: {received[i - 1]} then {received[i]}");
        }
        Assert.IsTrue(received.Count > 0);
    }

    [TestMethod]
    public void WriterDoesNotDeleteBackingFileOnDispose()
    {
        // The host writer is the transient party; the file lives in the renderer's instance dir and is
        // cleaned with it. Deleting on dispose churned the inode across a tab handoff (the original bug).
        using (var writer = InputRing.CreateWriter(_path))
        {
            writer.SetMousePosition(1f, 2f);
        }
        Assert.IsTrue(File.Exists(_path), "writer dispose must leave the backing file in place");
    }

    [TestMethod]
    public void HandoffToNewWriterKeepsExistingReaderLive()
    {
        // Reproduces the tear-off / merge bug: a renderer opens its reader once, then ownership of the
        // writer moves from one host process to another. The new host's CreateWriter must reuse the file
        // in place (same inode) so the already-open reader keeps receiving input — not strand it on an
        // orphaned mapping. We model the long-lived reader and the two successive host writers.
        using var reader = InputRing.OpenReader(InitFile());

        // First host writes, then disposes (handoff begins) WITHOUT deleting the file.
        using (var host1 = InputRing.CreateWriter(_path))
        {
            host1.SetMousePosition(10f, 20f);
            host1.Push(new InputEvent(InputEventKind.MouseDown, 0, 10f, 20f));
        }
        Assert.AreEqual(10f, reader.MouseX);
        Assert.IsTrue(reader.TryDequeue(out var down) && down.Kind == InputEventKind.MouseDown);

        // Second host takes over: reuse-in-place must NOT allocate a new inode, so the same reader sees it.
        using var host2 = InputRing.CreateWriter(_path);
        host2.SetMousePosition(99f, 88f);
        host2.Push(new InputEvent(InputEventKind.Scroll, 0, 0f, -3f));

        Assert.AreEqual(99f, reader.MouseX, "reader must see the new host's mouse position after handoff");
        Assert.AreEqual(88f, reader.MouseY);
        Assert.IsTrue(reader.TryDequeue(out var scroll), "reader must drain the new host's events after handoff");
        Assert.AreEqual(InputEventKind.Scroll, scroll.Kind);
        Assert.AreEqual(-3f, scroll.Y);
    }

    // Create the ring (as the host would the first time) so a reader can open it, then dispose that first
    // writer's handle — leaving the file on disk for the reader and for the successive writers under test.
    private string InitFile()
    {
        InputRing.CreateWriter(_path).Dispose();
        return _path;
    }
}
