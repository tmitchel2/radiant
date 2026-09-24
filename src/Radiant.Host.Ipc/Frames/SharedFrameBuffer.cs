using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;

namespace Radiant.Host.Ipc.Frames;

/// <summary>
/// Lock-free, single-producer/single-consumer transport for full rendered frames between two
/// processes (a headless renderer and the compositing host) backed by a memory-mapped file.
///
/// <para>File-backed (not an OS-named mapping) on purpose: .NET named shared memory
/// (<c>MemoryMappedFile.CreateNew(name, ...)</c>) throws on macOS/Linux. Backing the map with a
/// real file under the application's data directory works on every platform and matches the existing
/// filesystem-IPC convention.</para>
///
/// <para>Three slots are used so the writer never overwrites the slot the reader is currently
/// copying (provably tear-free for one producer + one consumer). At any instant the three slot
/// indices partition into: the writer's slot, the reader's slot, and the "ready" slot published
/// in the header. Publishing and acquiring are single interlocked exchanges on the header's ready
/// word, which double as the release/acquire barriers for the per-slot metadata.</para>
/// </summary>
public sealed unsafe class SharedFrameBuffer : IDisposable
{
    private const int Magic = 0x52414431; // "RAD1"
    private const int Version = 1;
    private const int SlotCount = 3;
    private const int HeaderSize = 256;

    // Header field byte offsets.
    private const int OffMagic = 0;
    private const int OffVersion = 4;
    private const int OffCapacity = 8;
    private const int OffSlotCount = 12;
    private const int OffReady = 16;       // atomic: bits 0..1 = slot index, bit 2 = dirty
    private const int OffReaderIndex = 20; // last slot owned by a reader (for clean reattach)
    private const int OffPublishCount = 24; // int64
    private const int OffMeta = 32;        // SlotCount * (w:int, h:int, fmt:int)

    private const int IndexMask = 0x3;
    private const int DirtyBit = 0x4;

    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly byte* _base;
    private readonly bool _ownsFile;
    private readonly string? _filePath;

    private int _writerSlot; // writer-owned slot (writer instances only)
    private int _readerSlot; // reader-owned slot (reader instances only)
    private bool _disposed;

    /// <summary>Bytes available per slot. A frame's <c>width*height*4</c> must not exceed this.</summary>
    public int Capacity { get; }

    private SharedFrameBuffer(MemoryMappedFile mmf, MemoryMappedViewAccessor accessor, byte* basePtr,
        int capacity, bool ownsFile, string? filePath, int writerSlot, int readerSlot)
    {
        _mmf = mmf;
        _accessor = accessor;
        _base = basePtr;
        Capacity = capacity;
        _ownsFile = ownsFile;
        _filePath = filePath;
        _writerSlot = writerSlot;
        _readerSlot = readerSlot;
    }

    /// <summary>
    /// Create (or recreate) the shared buffer as the writer. <paramref name="maxWidth"/> ×
    /// <paramref name="maxHeight"/> sizes each slot; frames up to that size can be published.
    /// </summary>
    public static SharedFrameBuffer CreateWriter(string filePath, int maxWidth, int maxHeight)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        if (maxWidth <= 0 || maxHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxWidth), "Frame dimensions must be positive.");
        }

        var capacity = checked(maxWidth * maxHeight * 4);
        var totalSize = HeaderSize + (long)capacity * SlotCount;

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Create, mapName: null, totalSize, MemoryMappedFileAccess.ReadWrite);
        var accessor = MapView(mmf, out var basePtr);

        // Initial partition: writer owns slot 0, ready holds slot 1 (not dirty), reader owns slot 2.
        WriteInt(basePtr, OffMagic, Magic);
        WriteInt(basePtr, OffVersion, Version);
        WriteInt(basePtr, OffCapacity, capacity);
        WriteInt(basePtr, OffSlotCount, SlotCount);
        WriteInt(basePtr, OffReaderIndex, 2);
        WriteLong(basePtr, OffPublishCount, 0);
        Volatile.Write(ref Ready(basePtr), 1); // ready = slot 1, clean — published last so readers see a valid header

        return new SharedFrameBuffer(mmf, accessor, basePtr, capacity, ownsFile: true, filePath, writerSlot: 0, readerSlot: -1);
    }

    /// <summary>Open an existing shared buffer as the reader.</summary>
    public static SharedFrameBuffer OpenReader(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        // A writer creates the file then sizes it (FileMode.Create truncates to 0 first), so a reader can
        // briefly observe it present but empty / shorter than the header — opening that with capacity 0
        // (use-file-length) would throw ArgumentException ("positive capacity … empty file"). Surface that
        // race as an IOException, which callers already treat as "not ready, retry next frame" (the same as
        // a locked / mid-creation file), instead of an uncaught crash.
        var info = new FileInfo(filePath);
        if (!info.Exists || info.Length < HeaderSize)
        {
            throw new IOException(
                $"Frame buffer '{filePath}' is not ready (length {(info.Exists ? info.Length : 0)} < header {HeaderSize}).");
        }

        var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, mapName: null, 0, MemoryMappedFileAccess.ReadWrite);
        var accessor = MapView(mmf, out var basePtr);

        var magic = ReadInt(basePtr, OffMagic);
        var version = ReadInt(basePtr, OffVersion);
        if (magic != Magic)
        {
            accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            accessor.Dispose();
            mmf.Dispose();
            throw new InvalidDataException($"'{filePath}' is not a Radiant frame buffer (magic 0x{magic:X8}).");
        }
        if (version != Version)
        {
            accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            accessor.Dispose();
            mmf.Dispose();
            throw new NotSupportedException($"Frame-buffer protocol version {version} is not supported (expected {Version}).");
        }

        var capacity = ReadInt(basePtr, OffCapacity);
        // The departed reader's owned slot is exactly the free one — safe to resume from it.
        var readerSlot = ReadInt(basePtr, OffReaderIndex) & IndexMask;
        return new SharedFrameBuffer(mmf, accessor, basePtr, capacity, ownsFile: false, filePath, writerSlot: -1, readerSlot: readerSlot);
    }

    /// <summary>
    /// Writer: copy a tightly packed (no row padding) frame into a free slot and publish it as the
    /// latest. Cheap — one bulk copy plus an interlocked exchange. Overwrites any unread frame
    /// (the reader always gets the newest, intermediate frames are dropped).
    /// </summary>
    public void Write(ReadOnlySpan<byte> pixels, int width, int height, FrameFormat format)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_writerSlot < 0)
        {
            throw new InvalidOperationException("This SharedFrameBuffer was opened as a reader.");
        }
        var length = checked(width * height * 4);
        if (length > Capacity)
        {
            throw new ArgumentException($"Frame {width}x{height} ({length} bytes) exceeds slot capacity ({Capacity} bytes).");
        }
        if (pixels.Length < length)
        {
            throw new ArgumentException($"Pixel span ({pixels.Length} bytes) is smaller than {width}x{height} ({length} bytes).", nameof(pixels));
        }

        pixels[..length].CopyTo(new Span<byte>(SlotPtr(_writerSlot), length));
        WriteMeta(_writerSlot, width, height, format);

        // Publish: swap our slot into ready (dirty). The interlocked exchange is a full barrier, so
        // the metadata + pixels written above are visible to a reader that subsequently acquires.
        var old = Interlocked.Exchange(ref Ready(_base), _writerSlot | DirtyBit);
        _writerSlot = old & IndexMask;
        Interlocked.Increment(ref PublishCount(_base));
    }

    /// <summary>
    /// Reader: if a new frame has been published since the last read, copy it into
    /// <paramref name="destination"/> and return true. Returns false (leaving <paramref name="info"/>
    /// default) when no new frame is available.
    /// </summary>
    public bool TryRead(Span<byte> destination, out FrameInfo info)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_readerSlot < 0)
        {
            throw new InvalidOperationException("This SharedFrameBuffer was created as a writer.");
        }

        var current = Volatile.Read(ref Ready(_base));
        if ((current & DirtyBit) == 0)
        {
            info = default;
            return false;
        }

        // Acquire: hand our (clean) slot back and take the freshly published one.
        var old = Interlocked.Exchange(ref Ready(_base), _readerSlot);
        _readerSlot = old & IndexMask;
        Volatile.Write(ref ReaderIndexField(_base), _readerSlot); // record for clean reattach

        ReadMeta(_readerSlot, out var width, out var height, out var format);
        var length = checked(width * height * 4);
        if (destination.Length < length)
        {
            throw new ArgumentException($"Destination ({destination.Length} bytes) is smaller than the frame {width}x{height} ({length} bytes).", nameof(destination));
        }

        new ReadOnlySpan<byte>(SlotPtr(_readerSlot), length).CopyTo(destination);
        info = new FrameInfo(width, height, format, Volatile.Read(ref PublishCount(_base)));
        return true;
    }

    /// <summary>Total number of frames published by the writer so far (observability / tests).</summary>
    public long PublishedCount => Volatile.Read(ref PublishCount(_base));

    private byte* SlotPtr(int slot) => _base + HeaderSize + (long)slot * Capacity;

    private void WriteMeta(int slot, int width, int height, FrameFormat format)
    {
        var off = OffMeta + slot * 12;
        WriteInt(_base, off, width);
        WriteInt(_base, off + 4, height);
        WriteInt(_base, off + 8, (int)format);
    }

    private void ReadMeta(int slot, out int width, out int height, out FrameFormat format)
    {
        var off = OffMeta + slot * 12;
        width = ReadInt(_base, off);
        height = ReadInt(_base, off + 4);
        format = (FrameFormat)ReadInt(_base, off + 8);
    }

    private static ref int Ready(byte* basePtr) => ref Unsafe.AsRef<int>(basePtr + OffReady);
    private static ref int ReaderIndexField(byte* basePtr) => ref Unsafe.AsRef<int>(basePtr + OffReaderIndex);
    private static ref long PublishCount(byte* basePtr) => ref Unsafe.AsRef<long>(basePtr + OffPublishCount);

    private static int ReadInt(byte* basePtr, int offset) => *(int*)(basePtr + offset);
    private static void WriteInt(byte* basePtr, int offset, int value) => *(int*)(basePtr + offset) = value;
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

        if (_ownsFile && _filePath is not null)
        {
            try
            {
                File.Delete(_filePath);
            }
            catch (IOException)
            {
                // Best-effort cleanup; a reader may still hold the file open briefly.
            }
        }
    }
}
