using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;

namespace Radiant.Host.Ipc.Input;

/// <summary>
/// Single-producer (compositing host) / single-consumer (headless renderer) shared-memory channel
/// carrying input from the host to the active tab's renderer: a discrete-event ring (button/key/
/// scroll/char) plus a latest-value state block (mouse position, desired render size, focus).
///
/// <para>High-frequency mouse-move is latest-value (the renderer only needs the current position);
/// presses, releases, scroll and chars are queued so none are lost. File-backed memory map (named OS
/// mappings throw on macOS/Linux), matching <see cref="Frames.SharedFrameBuffer"/>. Publish/consume
/// use interlocked write/read indices polled each frame — AOT-safe, no native semaphores.</para>
/// </summary>
public sealed unsafe class InputRing : IDisposable
{
    private const int Magic = 0x52494E31; // "RIN1"
    private const int Version = 2;
    private const int HeaderSize = 256;
    private const int EventSize = 16; // kind(int) code(int) x(float) y(float)
    private const int DefaultCapacity = 1024;

    // Header offsets.
    private const int OffMagic = 0;
    private const int OffVersion = 4;
    private const int OffCapacity = 8;
    private const int OffMouseX = 16;
    private const int OffMouseY = 20;
    private const int OffWidth = 24;
    private const int OffHeight = 28;
    private const int OffFocus = 32;
    private const int OffScale = 36;       // float: device pixel ratio (logical→physical) for the active tab
    private const int OffWriteIndex = 40; // int64
    private const int OffReadIndex = 48;  // int64
    private const int OffActive = 56;     // int: 1 = active tab (render), 0 = background (pause render)
    private const int OffHostPresentMs = 60;    // float: host window-present cost (ms) for the active tab
    private const int OffHostPresentFrame = 64; // int: host frame counter, so the renderer sees fresh values

    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly byte* _base;
    private readonly int _capacity;
    private bool _disposed;

    private InputRing(MemoryMappedFile mmf, MemoryMappedViewAccessor accessor, byte* basePtr, int capacity)
    {
        _mmf = mmf;
        _accessor = accessor;
        _base = basePtr;
        _capacity = capacity;
    }

    /// <summary>
    /// Open the ring as the writer (compositing host). When a compatible ring already exists at
    /// <paramref name="filePath"/> (same size + magic + version) it is mapped <b>in place</b>,
    /// preserving its inode so that a renderer which already opened the reader keeps a live mapping
    /// across a host <i>handoff</i> — a tear-off / merge, where ownership of the writer moves from one
    /// host process to another. Otherwise a fresh ring is created.
    ///
    /// <para>The writer never owns the file's lifetime: <c>input.bin</c> lives in the renderer's
    /// instance directory and is removed when the renderer deregisters (recursively deleting that dir),
    /// exactly as the renderer-written <see cref="Frames.SharedFrameBuffer"/> frames.bin is. Earlier the
    /// writer <c>File.Delete</c>d the file on dispose, so a handoff went delete (old host) → create (new
    /// host) and allocated a <i>new inode</i> — stranding the torn-off tab's renderer on the old,
    /// unlinked mapping (mouse input silently dead). Reuse-in-place removes that churn.</para>
    /// </summary>
    public static InputRing CreateWriter(string filePath, int capacity = DefaultCapacity)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var totalSize = HeaderSize + (long)capacity * EventSize;

        // Reuse a compatible existing ring in place (same inode) so a renderer's already-open reader
        // survives this host taking over the writer. The live header (indices + latest-value block) is
        // left intact: the host overwrites the latest-value fields each frame and pushes events from the
        // current write index, so the reader draining the same ring sees a continuous stream.
#pragma warning disable CA2000 // Ownership of the reused ring transfers to the caller (a factory return).
        if (TryReuseExisting(filePath, totalSize) is { } reused)
        {
            return reused;
        }
#pragma warning restore CA2000

        var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Create, mapName: null, totalSize, MemoryMappedFileAccess.ReadWrite);
        var accessor = MapView(mmf, out var basePtr);

        WriteInt(basePtr, OffCapacity, capacity);
        WriteLong(basePtr, OffWriteIndex, 0);
        WriteLong(basePtr, OffReadIndex, 0);
        WriteInt(basePtr, OffFocus, 1);
        WriteInt(basePtr, OffActive, 1); // default active until the host marks the tab background
        WriteFloat(basePtr, OffScale, 1f); // sane default until the host writes the real pixel ratio
        WriteInt(basePtr, OffVersion, Version);
        Volatile.Write(ref MagicRef(basePtr), Magic); // published last so a reader that sees magic sees a valid header

        return new InputRing(mmf, accessor, basePtr, capacity);
    }

    /// <summary>
    /// Map an existing ring of the expected size + magic + version in place as a writer (preserving its
    /// inode), or null if it is absent / the wrong size / incompatible / locked mid-creation — in which
    /// case <see cref="CreateWriter"/> falls back to creating a fresh one.
    /// </summary>
    private static InputRing? TryReuseExisting(string filePath, long totalSize)
    {
        var info = new FileInfo(filePath);
        if (!info.Exists || info.Length != totalSize)
        {
            return null;
        }
        try
        {
            var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, mapName: null, 0, MemoryMappedFileAccess.ReadWrite);
            var accessor = MapView(mmf, out var basePtr);
            if (Volatile.Read(ref MagicRef(basePtr)) != Magic || ReadInt(basePtr, OffVersion) != Version)
            {
                accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                accessor.Dispose();
                mmf.Dispose();
                return null;
            }
            var capacity = ReadInt(basePtr, OffCapacity);
            return new InputRing(mmf, accessor, basePtr, capacity);
        }
        catch (IOException)
        {
            // Locked / mid-creation by another process — not reusable this frame; create fresh / retry.
            return null;
        }
    }

    /// <summary>Open an existing ring as the reader (renderer).</summary>
    public static InputRing OpenReader(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, mapName: null, 0, MemoryMappedFileAccess.ReadWrite);
        var accessor = MapView(mmf, out var basePtr);

        var magic = Volatile.Read(ref MagicRef(basePtr));
        var version = ReadInt(basePtr, OffVersion);
        if (magic != Magic)
        {
            accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            accessor.Dispose();
            mmf.Dispose();
            throw new InvalidDataException($"'{filePath}' is not a Radiant input ring (magic 0x{magic:X8}).");
        }
        if (version != Version)
        {
            accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            accessor.Dispose();
            mmf.Dispose();
            throw new NotSupportedException($"Input-ring protocol version {version} is not supported (expected {Version}).");
        }

        var capacity = ReadInt(basePtr, OffCapacity);
        return new InputRing(mmf, accessor, basePtr, capacity);
    }

    // ---- Writer (host) API ----

    /// <summary>Set the latest mouse position (renderer-content pixels).</summary>
    public void SetMousePosition(float x, float y)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        WriteFloat(_base, OffMouseX, x);
        WriteFloat(_base, OffMouseY, y);
    }

    /// <summary>Set the desired render size for the active renderer (logical content pixels).</summary>
    public void SetSize(int width, int height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        WriteInt(_base, OffWidth, width);
        WriteInt(_base, OffHeight, height);
    }

    /// <summary>
    /// Set the device pixel ratio (logical→physical) of the host window. The renderer renders its
    /// offscreen frame at <c>size × scale</c> physical pixels so the host can blit it 1:1 into a
    /// high-DPI (e.g. Retina) window, while keeping UI layout and mouse coordinates in logical pixels.
    /// </summary>
    public void SetScale(float scale)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        WriteFloat(_base, OffScale, scale);
    }

    /// <summary>Set whether the host window currently has focus.</summary>
    public void SetFocus(bool focused)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        WriteInt(_base, OffFocus, focused ? 1 : 0);
    }

    /// <summary>
    /// Publish the host's most recent window-present cost (ms) for the active tab, tagged with the host
    /// frame counter so the renderer can tell a fresh value from a repeat. Only the visible (active) tab
    /// is presented, so only it receives a value; background tabs read 0. Used by the renderer's
    /// <c>--profile</c> report to attribute real vsync cost that its own offscreen "present" can't see.
    /// </summary>
    public void SetHostPresentMs(float ms, int frame)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        WriteFloat(_base, OffHostPresentMs, ms);
        // Write the frame tag last (release) so a reader that sees the new tag also sees the new ms.
        Volatile.Write(ref HostPresentFrameRef(_base), frame);
    }

    /// <summary>
    /// Set whether this tab is the active (foreground) tab. A background tab's renderer pauses its
    /// viewport render + frame publish (while keeping CFD solving); the host keeps showing its last
    /// published frame. Distinct from <see cref="SetFocus"/>, which tracks host-window focus.
    /// </summary>
    public void SetActive(bool active)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        WriteInt(_base, OffActive, active ? 1 : 0);
    }

    /// <summary>Enqueue a discrete input event. Drops the event if the ring is full (reader stalled).</summary>
    public void Push(InputEvent evt)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var write = Volatile.Read(ref WriteIndexRef(_base));
        var read = Volatile.Read(ref ReadIndexRef(_base));
        if (write - read >= _capacity)
        {
            return; // ring full — drop (renderer not draining fast enough)
        }

        var slot = (int)(write % _capacity);
        var p = EventPtr(slot);
        *(int*)(p + 0) = (int)evt.Kind;
        *(int*)(p + 4) = evt.Code;
        *(float*)(p + 8) = evt.X;
        *(float*)(p + 12) = evt.Y;

        // Publish: bump write index (release) so a reader that reads it sees the slot contents above.
        Volatile.Write(ref WriteIndexRef(_base), write + 1);
    }

    // ---- Reader (renderer) API ----

    public float MouseX => ReadFloat(_base, OffMouseX);
    public float MouseY => ReadFloat(_base, OffMouseY);
    public int Width => ReadInt(_base, OffWidth);
    public int Height => ReadInt(_base, OffHeight);
    /// <summary>Device pixel ratio written by the host; ≤0 (old host that never wrote it) reads as 1.</summary>
    public float Scale
    {
        get
        {
            var s = ReadFloat(_base, OffScale);
            return s > 0f ? s : 1f;
        }
    }
    public bool Focused => ReadInt(_base, OffFocus) != 0;
    /// <summary>Host window-present cost (ms) for the active tab; 0 if the host never published one.</summary>
    public float HostPresentMs => ReadFloat(_base, OffHostPresentMs);
    /// <summary>Host frame counter tagging the latest <see cref="HostPresentMs"/>; advances each presented host frame.</summary>
    public int HostPresentFrame => Volatile.Read(ref HostPresentFrameRef(_base));

    /// <summary>
    /// Whether this tab is the active (foreground) tab. The renderer renders + publishes only when
    /// active; a background tab pauses its viewport while continuing to solve. Defaults to active.
    /// </summary>
    public bool Active => ReadInt(_base, OffActive) != 0;

    /// <summary>Dequeue the next queued event, or false if none pending.</summary>
    public bool TryDequeue(out InputEvent evt)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var read = Volatile.Read(ref ReadIndexRef(_base));
        var write = Volatile.Read(ref WriteIndexRef(_base)); // acquire
        if (read >= write)
        {
            evt = default;
            return false;
        }

        var slot = (int)(read % _capacity);
        var p = EventPtr(slot);
        evt = new InputEvent((InputEventKind)(*(int*)(p + 0)), *(int*)(p + 4), *(float*)(p + 8), *(float*)(p + 12));

        Volatile.Write(ref ReadIndexRef(_base), read + 1);
        return true;
    }

    private byte* EventPtr(int slot) => _base + HeaderSize + (long)slot * EventSize;

    private static ref int MagicRef(byte* basePtr) => ref Unsafe.AsRef<int>(basePtr + OffMagic);
    private static ref long WriteIndexRef(byte* basePtr) => ref Unsafe.AsRef<long>(basePtr + OffWriteIndex);
    private static ref long ReadIndexRef(byte* basePtr) => ref Unsafe.AsRef<long>(basePtr + OffReadIndex);
    private static ref int HostPresentFrameRef(byte* basePtr) => ref Unsafe.AsRef<int>(basePtr + OffHostPresentFrame);

    private static int ReadInt(byte* basePtr, int offset) => *(int*)(basePtr + offset);
    private static float ReadFloat(byte* basePtr, int offset) => *(float*)(basePtr + offset);
    private static void WriteInt(byte* basePtr, int offset, int value) => *(int*)(basePtr + offset) = value;
    private static void WriteFloat(byte* basePtr, int offset, float value) => *(float*)(basePtr + offset) = value;
    private static void WriteLong(byte* basePtr, int offset, long value) => *(long*)(basePtr + offset) = value;

    private static MemoryMappedViewAccessor MapView(MemoryMappedFile mmf, out byte* basePtr)
    {
        var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
        byte* ptr = null;
        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
        basePtr = ptr + accessor.PointerOffset;
        return accessor;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        _accessor.Dispose();
        _mmf.Dispose();
        // No File.Delete: the ring's backing file lives in the renderer's instance directory and is
        // removed when the renderer deregisters (InstanceRegistry recursively deletes that dir). Deleting
        // it here — on the transient host writer's dispose — churned the inode across a tab handoff and
        // stranded the renderer's reader on an orphaned mapping (see CreateWriter).
    }
}
