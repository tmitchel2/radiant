using Radiant.Host.Ipc.Frames;

namespace Radiant.Host.Ipc.Tests;

[TestClass]
public sealed class SharedFrameBufferTests
{
    private string _path = null!;

    [TestInitialize]
    public void Setup()
    {
        var dir = Path.Combine(Path.GetTempPath(), "radiant-frame-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "frames.bin");
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
    public void WriteThenReadRoundTripsPixels()
    {
        var width = 8;
        var height = 4;
        using var writer = SharedFrameBuffer.CreateWriter(_path, width, height);
        using var reader = SharedFrameBuffer.OpenReader(_path);

        var frame = MakeFrame(width, height, seed: 7);
        writer.Write(frame, width, height, FrameFormat.Rgba8Unorm);

        var destination = new byte[width * height * 4];
        var ok = reader.TryRead(destination, out var info);

        Assert.IsTrue(ok);
        Assert.AreEqual(width, info.Width);
        Assert.AreEqual(height, info.Height);
        Assert.AreEqual(FrameFormat.Rgba8Unorm, info.Format);
        Assert.AreEqual(width * height * 4, info.ByteLength);
        CollectionAssert.AreEqual(frame, destination);
    }

    [TestMethod]
    public void TryReadReturnsFalseBeforeAnyFrameIsPublished()
    {
        using var writer = SharedFrameBuffer.CreateWriter(_path, 4, 4);
        using var reader = SharedFrameBuffer.OpenReader(_path);

        var ok = reader.TryRead(new byte[4 * 4 * 4], out _);

        Assert.IsFalse(ok);
    }

    [TestMethod]
    public void TryReadReturnsFalseWhenNoNewFrameSinceLastRead()
    {
        using var writer = SharedFrameBuffer.CreateWriter(_path, 4, 4);
        using var reader = SharedFrameBuffer.OpenReader(_path);

        writer.Write(MakeFrame(4, 4, seed: 1), 4, 4, FrameFormat.Rgba8Unorm);
        var dst = new byte[4 * 4 * 4];

        Assert.IsTrue(reader.TryRead(dst, out _), "first read should see the published frame");
        Assert.IsFalse(reader.TryRead(dst, out _), "second read with no new publish should be empty");
    }

    [TestMethod]
    public void ReaderGetsLatestFrameWhenWriterOutpacesReader()
    {
        var width = 4;
        var height = 4;
        using var writer = SharedFrameBuffer.CreateWriter(_path, width, height);
        using var reader = SharedFrameBuffer.OpenReader(_path);

        var latest = MakeFrame(width, height, seed: 99);
        writer.Write(MakeFrame(width, height, seed: 1), width, height, FrameFormat.Rgba8Unorm);
        writer.Write(MakeFrame(width, height, seed: 2), width, height, FrameFormat.Rgba8Unorm);
        writer.Write(latest, width, height, FrameFormat.Rgba8Unorm);

        var dst = new byte[width * height * 4];
        var ok = reader.TryRead(dst, out var info);

        Assert.IsTrue(ok);
        Assert.AreEqual(3L, info.FrameIndex);
        Assert.AreEqual(3L, writer.PublishedCount);
        CollectionAssert.AreEqual(latest, dst, "reader should receive the newest frame, intermediate frames dropped");
    }

    [TestMethod]
    public void VaryingDimensionsRoundTripThroughTheSameBuffer()
    {
        // Slot is sized for the max; smaller frames must report their own dimensions.
        using var writer = SharedFrameBuffer.CreateWriter(_path, 16, 16);
        using var reader = SharedFrameBuffer.OpenReader(_path);
        var dst = new byte[16 * 16 * 4];

        writer.Write(MakeFrame(16, 16, seed: 3), 16, 16, FrameFormat.Bgra8Unorm);
        Assert.IsTrue(reader.TryRead(dst, out var big));
        Assert.AreEqual(16, big.Width);
        Assert.AreEqual(16, big.Height);
        Assert.AreEqual(FrameFormat.Bgra8Unorm, big.Format);

        writer.Write(MakeFrame(5, 3, seed: 4), 5, 3, FrameFormat.Rgba8Unorm);
        Assert.IsTrue(reader.TryRead(dst, out var small));
        Assert.AreEqual(5, small.Width);
        Assert.AreEqual(3, small.Height);
        Assert.AreEqual(FrameFormat.Rgba8Unorm, small.Format);
    }

    [TestMethod]
    public void ReopeningReaderResumesCleanlyAfterPriorReaderLeaves()
    {
        // Exercises the reattach invariant: a fresh reader must resume from the departed reader's slot.
        var width = 8;
        var height = 8;
        using var writer = SharedFrameBuffer.CreateWriter(_path, width, height);
        var dst = new byte[width * height * 4];

        using (var first = SharedFrameBuffer.OpenReader(_path))
        {
            writer.Write(MakeFrame(width, height, seed: 10), width, height, FrameFormat.Rgba8Unorm);
            Assert.IsTrue(first.TryRead(dst, out _));
            writer.Write(MakeFrame(width, height, seed: 11), width, height, FrameFormat.Rgba8Unorm);
            Assert.IsTrue(first.TryRead(dst, out _));
        }

        using var second = SharedFrameBuffer.OpenReader(_path);
        var expected = MakeFrame(width, height, seed: 12);
        writer.Write(expected, width, height, FrameFormat.Rgba8Unorm);

        Assert.IsTrue(second.TryRead(dst, out _));
        CollectionAssert.AreEqual(expected, dst);
    }

    [TestMethod]
    public void WriteThrowsWhenFrameExceedsSlotCapacity()
    {
        using var writer = SharedFrameBuffer.CreateWriter(_path, 4, 4);
        var tooBig = new byte[8 * 8 * 4];

        Assert.ThrowsExactly<ArgumentException>(() => writer.Write(tooBig, 8, 8, FrameFormat.Rgba8Unorm));
    }

    [TestMethod]
    public void WriteOnAReaderThrows()
    {
        using var writer = SharedFrameBuffer.CreateWriter(_path, 4, 4);
        using var reader = SharedFrameBuffer.OpenReader(_path);

        Assert.ThrowsExactly<InvalidOperationException>(() => reader.Write(new byte[4 * 4 * 4], 4, 4, FrameFormat.Rgba8Unorm));
    }

    [TestMethod]
    public void TryReadOnAWriterThrows()
    {
        using var writer = SharedFrameBuffer.CreateWriter(_path, 4, 4);

        Assert.ThrowsExactly<InvalidOperationException>(() => writer.TryRead(new byte[4 * 4 * 4], out _));
    }

    [TestMethod]
    public void OpenReaderThrowsOnANonFrameBufferFile()
    {
        File.WriteAllBytes(_path, new byte[1024]);

        Assert.ThrowsExactly<InvalidDataException>(() => SharedFrameBuffer.OpenReader(_path));
    }

    [TestMethod]
    public void OpenReaderThrowsIoExceptionOnAnEmptyFile()
    {
        // A writer creates the file then sizes it, so a reader can briefly observe it empty. Opening that
        // must surface as a (catchable) IOException — "not ready, retry" — not an uncaught ArgumentException
        // from mapping a zero-length file.
        File.WriteAllBytes(_path, []);

        Assert.ThrowsExactly<IOException>(() => SharedFrameBuffer.OpenReader(_path));
    }

    [TestMethod]
    public void OpenReaderThrowsIoExceptionOnAFileShorterThanTheHeader()
    {
        File.WriteAllBytes(_path, new byte[16]); // present but smaller than the 256-byte header

        Assert.ThrowsExactly<IOException>(() => SharedFrameBuffer.OpenReader(_path));
    }

    [TestMethod]
    public void ConcurrentWriterAndReaderNeverObserveATornFrame()
    {
        // Each frame is filled with a single sentinel byte. A torn frame (writer overwriting a slot
        // the reader is copying) would surface as a frame containing more than one distinct value.
        var width = 96;
        var height = 96;
        var frames = 5000;
        using var writer = SharedFrameBuffer.CreateWriter(_path, width, height);
        using var reader = SharedFrameBuffer.OpenReader(_path);

        var torn = false;
        var framesObserved = 0;
        var done = false;

        var readerThread = new Thread(() =>
        {
            var dst = new byte[width * height * 4];
            while (!Volatile.Read(ref done))
            {
                if (!reader.TryRead(dst, out var info))
                {
                    continue;
                }
                framesObserved++;
                var sentinel = dst[0];
                for (var i = 1; i < info.ByteLength; i++)
                {
                    if (dst[i] != sentinel)
                    {
                        torn = true;
                        return;
                    }
                }
            }
        });
        readerThread.Start();

        var buffer = new byte[width * height * 4];
        for (var f = 0; f < frames; f++)
        {
            var sentinel = (byte)((f % 255) + 1); // 1..255, never 0 (uninitialised slots read as 0)
            Array.Fill(buffer, sentinel);
            writer.Write(buffer, width, height, FrameFormat.Rgba8Unorm);
        }

        Volatile.Write(ref done, true);
        readerThread.Join(TimeSpan.FromSeconds(30));

        Assert.IsFalse(torn, "reader observed a torn frame — triple-buffer isolation is broken");
        Assert.AreEqual(frames, (int)writer.PublishedCount);
        Assert.IsTrue(framesObserved > 0, "reader should have observed at least one frame");
    }

    private static byte[] MakeFrame(int width, int height, int seed)
    {
        var bytes = new byte[width * height * 4];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)((i * 31 + seed * 17) & 0xFF);
        }
        return bytes;
    }
}
