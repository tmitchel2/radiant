using System;
using System.Collections.Generic;

namespace Radiant.Text.Unicode;

/// <summary>
/// Rules P2–I2 of the Unicode Bidirectional Algorithm (UAX #9): resolves the embedding level of
/// every code unit of a text, one paragraph (split at Bidi_Class B, rule P1) at a time. The
/// explicit formatting characters and BN that rule X9 removes stay in place and are skipped: each
/// isolating run sequence is a list of the indices it covers, so the weak, neutral and implicit
/// rules run over those indices without copying the text. The working arrays live only for one
/// resolution; <see cref="BidiParagraph"/> keeps just the classes and levels.
/// </summary>
internal sealed class BidiResolver
{
    /// <summary>The deepest explicit embedding level (BD2).</summary>
    public const int MaxDepth = 125;

    /// <summary>The bracket stack's capacity in BD16; deeper nesting stops pairing.</summary>
    private const int MaxBracketDepth = 63;

    private readonly string? _text;
    private readonly BidiClass[] _classes;
    private readonly BidiClass[] _types;

    // Levels from the explicit rules (X1–X8), and the resolved levels (I1–I2) that are the result.
    private readonly byte[] _explicitLevels;
    private readonly byte[] _levels;
    private readonly byte _paragraphLevel;

    // Isolate initiators map to their matching PDI and matched PDIs to their initiator (BD9); -1 otherwise.
    private readonly int[] _match;

    // The paragraph's characters that survive X9, in order, and the level runs (BD7) over them.
    private readonly int[] _kept;
    private readonly int[] _runStarts;
    private int _keptCount;
    private int _runCount;

    // The isolating run sequence (BD13) being resolved, as indices into the text; which of its
    // characters were NSM before W1 (N0 gives those after a bracket the bracket's direction); and
    // its bracket pairs, as positions in it.
    private readonly int[] _sequence;
    private readonly bool[] _wasNsm;
    private readonly List<(int Open, int Close)> _pairs = [];

    private BidiResolver(string? text, BidiClass[] classes, byte paragraphLevel)
    {
        var n = classes.Length;
        _text = text;
        _classes = classes;
        _paragraphLevel = paragraphLevel;
        _types = new BidiClass[n];
        _explicitLevels = new byte[n];
        _levels = new byte[n];
        _match = new int[n];
        _kept = new int[n];
        _runStarts = new int[n + 1];
        _sequence = new int[n];
        _wasNsm = new bool[n];
    }

    /// <summary>
    /// Resolves the levels of <paramref name="classes"/> (the Bidi_Class of each code unit; the
    /// second half of a surrogate pair is BN, so X9 hides it) at <paramref name="paragraphLevel"/>.
    /// <paramref name="text"/> supplies code points for bracket pairing (N0); without it no
    /// character is a bracket. Removed characters take the level of the character before them.
    /// </summary>
    public static byte[] Resolve(string? text, BidiClass[] classes, byte paragraphLevel)
    {
        var resolver = new BidiResolver(text, classes, paragraphLevel);
        var start = 0;
        while (start < classes.Length)
        {
            var end = start;
            while (end < classes.Length && classes[end] != BidiClass.B)
            {
                end++;
            }
            if (end < classes.Length)
            {
                end++;
            }
            resolver.ResolveParagraph(start, end);
            start = end;
        }
        return resolver._levels;
    }

    /// <summary>
    /// Rules P2 and P3 over <c>classes[start..end)</c>: 1 if the first strong character outside any
    /// isolate is R or AL, 0 if it is L, and -1 if there is none before the end or a paragraph separator.
    /// </summary>
    public static int FirstStrongLevel(ReadOnlySpan<BidiClass> classes, int start, int end)
    {
        var depth = 0;
        for (var i = start; i < end; i++)
        {
            switch (classes[i])
            {
                case BidiClass.L when depth == 0:
                    return 0;
                case BidiClass.R or BidiClass.AL when depth == 0:
                    return 1;
                case BidiClass.LRI or BidiClass.RLI or BidiClass.FSI:
                    depth++;
                    break;
                case BidiClass.PDI when depth > 0:
                    depth--;
                    break;
                case BidiClass.B:
                    return -1;
            }
        }
        return -1;
    }

    /// <summary>Whether rule X9 removes a character of this class.</summary>
    public static bool IsRemovedByX9(BidiClass c) =>
        c is BidiClass.RLE or BidiClass.LRE or BidiClass.RLO or BidiClass.LRO or BidiClass.PDF or BidiClass.BN;

    private static bool IsIsolateInitiator(BidiClass c) => c is BidiClass.LRI or BidiClass.RLI or BidiClass.FSI;

    private void ResolveParagraph(int start, int end)
    {
        MatchIsolates(start, end);
        ResolveExplicitLevels(start, end);
        FindLevelRuns(start, end);
        ResolveIsolatingRunSequences(start, end);

        // Characters removed by X9 take the level of the character before them, so they never
        // start a new level run when the levels are reordered.
        for (var i = start; i < end; i++)
        {
            if (IsRemovedByX9(_classes[i]))
            {
                _levels[i] = i == start ? _paragraphLevel : _levels[i - 1];
            }
        }
    }

    /// <summary>BD9: pairs each isolate initiator with its matching PDI.</summary>
    private void MatchIsolates(int start, int end)
    {
        // _kept is free until FindLevelRuns; use it as the stack of open initiators.
        var stack = _kept;
        var depth = 0;
        for (var i = start; i < end; i++)
        {
            _match[i] = -1;
            var c = _classes[i];
            if (IsIsolateInitiator(c))
            {
                stack[depth++] = i;
            }
            else if (c == BidiClass.PDI && depth > 0)
            {
                var opener = stack[--depth];
                _match[opener] = i;
                _match[i] = opener;
            }
        }
    }

    /// <summary>Rules X1–X8: explicit embeddings, overrides and isolates.</summary>
    private void ResolveExplicitLevels(int start, int end)
    {
        Span<byte> stackLevel = stackalloc byte[MaxDepth + 2];
        Span<BidiClass> stackOverride = stackalloc BidiClass[MaxDepth + 2];
        Span<bool> stackIsolate = stackalloc bool[MaxDepth + 2];
        var top = 0;
        stackLevel[0] = _paragraphLevel;
        stackOverride[0] = BidiClass.ON;
        stackIsolate[0] = false;
        int overflowIsolates = 0, overflowEmbeddings = 0, validIsolates = 0;

        for (var i = start; i < end; i++)
        {
            var c = _classes[i];
            _types[i] = c;
            switch (c)
            {
                case BidiClass.RLE or BidiClass.LRE or BidiClass.RLO or BidiClass.LRO:
                {
                    // X2–X5.
                    _explicitLevels[i] = stackLevel[top];
                    var level = NextLevel(stackLevel[top], c is BidiClass.RLE or BidiClass.RLO);
                    if (level <= MaxDepth && overflowIsolates == 0 && overflowEmbeddings == 0)
                    {
                        top++;
                        stackLevel[top] = (byte)level;
                        stackOverride[top] = c switch
                        {
                            BidiClass.RLO => BidiClass.R,
                            BidiClass.LRO => BidiClass.L,
                            _ => BidiClass.ON,
                        };
                        stackIsolate[top] = false;
                    }
                    else if (overflowIsolates == 0)
                    {
                        overflowEmbeddings++;
                    }
                    break;
                }
                case BidiClass.RLI or BidiClass.LRI or BidiClass.FSI:
                {
                    // X5a–X5c.
                    _explicitLevels[i] = stackLevel[top];
                    if (stackOverride[top] != BidiClass.ON)
                    {
                        _types[i] = stackOverride[top];
                    }
                    var rtl = c == BidiClass.RLI
                        || (c == BidiClass.FSI && FirstStrongLevel(_classes, i + 1, _match[i] >= 0 ? _match[i] : end) == 1);
                    var level = NextLevel(stackLevel[top], rtl);
                    if (level <= MaxDepth && overflowIsolates == 0 && overflowEmbeddings == 0)
                    {
                        validIsolates++;
                        top++;
                        stackLevel[top] = (byte)level;
                        stackOverride[top] = BidiClass.ON;
                        stackIsolate[top] = true;
                    }
                    else
                    {
                        overflowIsolates++;
                    }
                    break;
                }
                case BidiClass.PDI:
                    // X6a.
                    if (overflowIsolates > 0)
                    {
                        overflowIsolates--;
                    }
                    else if (validIsolates > 0)
                    {
                        overflowEmbeddings = 0;
                        while (!stackIsolate[top])
                        {
                            top--;
                        }
                        top--;
                        validIsolates--;
                    }
                    _explicitLevels[i] = stackLevel[top];
                    if (stackOverride[top] != BidiClass.ON)
                    {
                        _types[i] = stackOverride[top];
                    }
                    break;
                case BidiClass.PDF:
                    // X7.
                    if (overflowIsolates == 0)
                    {
                        if (overflowEmbeddings > 0)
                        {
                            overflowEmbeddings--;
                        }
                        else if (!stackIsolate[top] && top > 0)
                        {
                            top--;
                        }
                    }
                    _explicitLevels[i] = stackLevel[top];
                    break;
                case BidiClass.B:
                    // X8: the paragraph separator ends every embedding, override and isolate.
                    _explicitLevels[i] = _paragraphLevel;
                    break;
                case BidiClass.BN:
                    _explicitLevels[i] = stackLevel[top];
                    break;
                default:
                    // X6.
                    _explicitLevels[i] = stackLevel[top];
                    if (stackOverride[top] != BidiClass.ON)
                    {
                        _types[i] = stackOverride[top];
                    }
                    break;
            }
        }
    }

    /// <summary>The least odd (right to left) or even level above <paramref name="level"/>.</summary>
    private static int NextLevel(int level, bool rtl) => rtl ? (level + 1) | 1 : (level + 2) & ~1;

    /// <summary>
    /// X9 and BD7: collects the characters X9 keeps and splits them into runs of equal level.
    /// </summary>
    private void FindLevelRuns(int start, int end)
    {
        var count = 0;
        for (var i = start; i < end; i++)
        {
            if (!IsRemovedByX9(_classes[i]))
            {
                _kept[count++] = i;
            }
        }
        var runs = 0;
        for (var k = 0; k < count; k++)
        {
            if (k == 0 || _explicitLevels[_kept[k]] != _explicitLevels[_kept[k - 1]])
            {
                _runStarts[runs++] = k;
            }
        }
        _runStarts[runs] = count;
        _keptCount = count;
        _runCount = runs;
    }

    /// <summary>
    /// X10 and BD13: chains level runs across isolates into isolating run sequences and resolves
    /// each one in turn.
    /// </summary>
    private void ResolveIsolatingRunSequences(int start, int end)
    {
        for (var run = 0; run < _runCount; run++)
        {
            var first = _kept[_runStarts[run]];
            if (_classes[first] == BidiClass.PDI && _match[first] >= 0)
            {
                // Continues the sequence of its isolate initiator, already resolved.
                continue;
            }

            var length = 0;
            var current = run;
            while (true)
            {
                for (var k = _runStarts[current]; k < _runStarts[current + 1]; k++)
                {
                    _sequence[length++] = _kept[k];
                }
                var last = _sequence[length - 1];
                if (!IsIsolateInitiator(_classes[last]) || _match[last] < 0)
                {
                    break;
                }
                current = RunOf(_match[last]);
            }
            ResolveSequence(_sequence.AsSpan(0, length), start, end);
        }
    }

    /// <summary>The level run holding a kept character; only isolates need it, so it is searched.</summary>
    private int RunOf(int index)
    {
        var position = _kept.AsSpan(0, _keptCount).BinarySearch(index);
        var run = _runStarts.AsSpan(0, _runCount).BinarySearch(position);
        return run >= 0 ? run : ~run - 1;
    }

    private void ResolveSequence(ReadOnlySpan<int> sequence, int start, int end)
    {
        var level = _explicitLevels[sequence[0]];

        // X10: sos and eos from the higher of this sequence's level and its neighbours', skipping
        // removed characters. A sequence ending in an isolate initiator (necessarily unmatched)
        // compares with the paragraph level instead of the isolate's contents.
        var before = sequence[0] - 1;
        while (before >= start && IsRemovedByX9(_classes[before]))
        {
            before--;
        }
        var beforeLevel = before >= start ? _explicitLevels[before] : _paragraphLevel;
        var sos = DirectionOf(Math.Max(beforeLevel, level));

        var last = sequence[^1];
        var afterLevel = _paragraphLevel;
        if (!IsIsolateInitiator(_classes[last]))
        {
            var after = last + 1;
            while (after < end && IsRemovedByX9(_classes[after]))
            {
                after++;
            }
            if (after < end)
            {
                afterLevel = _explicitLevels[after];
            }
        }
        var eos = DirectionOf(Math.Max(afterLevel, level));

        ResolveWeakTypes(sequence, sos);
        if (_text is not null)
        {
            ResolveBracketPairs(sequence, sos, level);
        }
        ResolveNeutralTypes(sequence, sos, eos, level);
        ResolveImplicitLevels(sequence, level);
    }

    private static BidiClass DirectionOf(int level) => (level & 1) == 0 ? BidiClass.L : BidiClass.R;

    /// <summary>Rules W1–W7.</summary>
    private void ResolveWeakTypes(ReadOnlySpan<int> sequence, BidiClass sos)
    {
        var types = _types;
        var length = sequence.Length;

        // W1: non-spacing marks take the type of what they follow.
        var previous = sos;
        foreach (var i in sequence)
        {
            var t = types[i];
            _wasNsm[i] = t == BidiClass.NSM;
            if (t == BidiClass.NSM)
            {
                types[i] = t = IsIsolateInitiator(previous) || previous == BidiClass.PDI ? BidiClass.ON : previous;
            }
            previous = t;
        }

        // W2 and W3: European numbers after Arabic letters are Arabic numbers; AL becomes R.
        var strong = sos;
        foreach (var i in sequence)
        {
            var t = types[i];
            if (t is BidiClass.L or BidiClass.R or BidiClass.AL)
            {
                strong = t;
                if (t == BidiClass.AL)
                {
                    types[i] = BidiClass.R;
                }
            }
            else if (t == BidiClass.EN && strong == BidiClass.AL)
            {
                types[i] = BidiClass.AN;
            }
        }

        // W4: a single separator between two numbers of the same kind joins them.
        for (var k = 1; k < length - 1; k++)
        {
            var t = types[sequence[k]];
            if (t is not (BidiClass.ES or BidiClass.CS))
            {
                continue;
            }
            var prev = types[sequence[k - 1]];
            var next = types[sequence[k + 1]];
            if (prev == BidiClass.EN && next == BidiClass.EN)
            {
                types[sequence[k]] = BidiClass.EN;
            }
            else if (t == BidiClass.CS && prev == BidiClass.AN && next == BidiClass.AN)
            {
                types[sequence[k]] = BidiClass.AN;
            }
        }

        // W5: terminators next to European numbers are European numbers.
        for (var k = 0; k < length; k++)
        {
            if (types[sequence[k]] != BidiClass.ET)
            {
                continue;
            }
            var runEnd = k + 1;
            while (runEnd < length && types[sequence[runEnd]] == BidiClass.ET)
            {
                runEnd++;
            }
            var touchesNumber = (k > 0 && types[sequence[k - 1]] == BidiClass.EN)
                || (runEnd < length && types[sequence[runEnd]] == BidiClass.EN);
            if (touchesNumber)
            {
                for (var j = k; j < runEnd; j++)
                {
                    types[sequence[j]] = BidiClass.EN;
                }
            }
            k = runEnd - 1;
        }

        // W6: remaining separators and terminators are neutral.
        foreach (var i in sequence)
        {
            if (types[i] is BidiClass.ES or BidiClass.ET or BidiClass.CS)
            {
                types[i] = BidiClass.ON;
            }
        }

        // W7: European numbers in left-to-right context are L.
        strong = sos;
        foreach (var i in sequence)
        {
            var t = types[i];
            if (t is BidiClass.L or BidiClass.R)
            {
                strong = t;
            }
            else if (t == BidiClass.EN && strong == BidiClass.L)
            {
                types[i] = BidiClass.L;
            }
        }
    }

    /// <summary>The strong direction a type counts as for N0 and N1: numbers count as R.</summary>
    private static BidiClass StrongDirection(BidiClass t) => t switch
    {
        BidiClass.L => BidiClass.L,
        BidiClass.R or BidiClass.AL or BidiClass.EN or BidiClass.AN => BidiClass.R,
        _ => BidiClass.ON,
    };

    /// <summary>Rule N0 with BD16: paired brackets take the direction of what they enclose.</summary>
    private void ResolveBracketPairs(ReadOnlySpan<int> sequence, BidiClass sos, byte level)
    {
        FindBracketPairs(sequence);
        if (_pairs.Count == 0)
        {
            return;
        }
        _pairs.Sort(static (a, b) => a.Open.CompareTo(b.Open));

        var types = _types;
        var embedding = DirectionOf(level);
        var opposite = embedding == BidiClass.L ? BidiClass.R : BidiClass.L;
        foreach (var (open, close) in _pairs)
        {
            var foundEmbedding = false;
            var foundOpposite = false;
            for (var k = open + 1; k < close; k++)
            {
                var d = StrongDirection(types[sequence[k]]);
                if (d == embedding)
                {
                    foundEmbedding = true;
                    break;
                }
                foundOpposite |= d == opposite;
            }

            BidiClass direction;
            if (foundEmbedding)
            {
                // N0 b.
                direction = embedding;
            }
            else if (foundOpposite)
            {
                // N0 c: the opposite direction only if the context before the pair agrees.
                var context = sos;
                for (var k = open - 1; k >= 0; k--)
                {
                    var d = StrongDirection(types[sequence[k]]);
                    if (d != BidiClass.ON)
                    {
                        context = d;
                        break;
                    }
                }
                direction = context == opposite ? opposite : embedding;
            }
            else
            {
                // N0 d: nothing strong inside; N1 and N2 decide.
                continue;
            }

            SetBracket(sequence, open, direction);
            SetBracket(sequence, close, direction);
        }
    }

    /// <summary>
    /// Sets a bracket's type, and the non-spacing marks that followed it before W1 with it.
    /// </summary>
    private void SetBracket(ReadOnlySpan<int> sequence, int position, BidiClass direction)
    {
        _types[sequence[position]] = direction;
        for (var k = position + 1; k < sequence.Length && _wasNsm[sequence[k]]; k++)
        {
            _types[sequence[k]] = direction;
        }
    }

    /// <summary>
    /// BD16: fills <see cref="_pairs"/> with the bracket pairs of the sequence, as positions in it.
    /// A bracket is a character whose current type is ON; brackets match under canonical equivalence.
    /// </summary>
    private void FindBracketPairs(ReadOnlySpan<int> sequence)
    {
        _pairs.Clear();
        Span<int> openPosition = stackalloc int[MaxBracketDepth];
        Span<int> openPair = stackalloc int[MaxBracketDepth];
        var depth = 0;
        var brackets = BidiBracketsData.CodePoints;
        for (var k = 0; k < sequence.Length; k++)
        {
            var i = sequence[k];
            if (_types[i] != BidiClass.ON)
            {
                continue;
            }
            var b = brackets.BinarySearch(CodePointAt(_text!, i));
            if (b < 0)
            {
                continue;
            }
            if (BidiBracketsData.Opens[b])
            {
                if (depth == MaxBracketDepth)
                {
                    // Too deep: BD16 stops looking for pairs in this sequence.
                    return;
                }
                openPosition[depth] = k;
                openPair[depth] = BidiBracketsData.CanonicalPair[b];
                depth++;
            }
            else
            {
                var canonical = BidiBracketsData.Canonical[b];
                for (var s = depth - 1; s >= 0; s--)
                {
                    if (openPair[s] == canonical)
                    {
                        _pairs.Add((openPosition[s], k));
                        depth = s;
                        break;
                    }
                }
            }
        }
    }

    /// <summary>The code point at a UTF-16 index; a lone surrogate is its own code point.</summary>
    public static int CodePointAt(string text, int index)
    {
        var c = text[index];
        if (char.IsHighSurrogate(c) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
        {
            return char.ConvertToUtf32(c, text[index + 1]);
        }
        if (char.IsLowSurrogate(c) && index > 0 && char.IsHighSurrogate(text[index - 1]))
        {
            return char.ConvertToUtf32(text[index - 1], c);
        }
        return c;
    }

    private static bool IsNeutralOrIsolate(BidiClass t) =>
        t is BidiClass.B or BidiClass.S or BidiClass.WS or BidiClass.ON
            or BidiClass.LRI or BidiClass.RLI or BidiClass.FSI or BidiClass.PDI;

    /// <summary>
    /// Rules N1 and N2: neutrals between two runs of the same direction take it; the rest take the
    /// embedding direction.
    /// </summary>
    private void ResolveNeutralTypes(ReadOnlySpan<int> sequence, BidiClass sos, BidiClass eos, byte level)
    {
        var types = _types;
        var embedding = DirectionOf(level);
        var length = sequence.Length;
        for (var k = 0; k < length; k++)
        {
            if (!IsNeutralOrIsolate(types[sequence[k]]))
            {
                continue;
            }
            var runEnd = k + 1;
            while (runEnd < length && IsNeutralOrIsolate(types[sequence[runEnd]]))
            {
                runEnd++;
            }
            var leading = k == 0 ? sos : StrongDirection(types[sequence[k - 1]]);
            var trailing = runEnd == length ? eos : StrongDirection(types[sequence[runEnd]]);
            var direction = leading == trailing ? leading : embedding;
            for (var j = k; j < runEnd; j++)
            {
                types[sequence[j]] = direction;
            }
            k = runEnd - 1;
        }
    }

    /// <summary>Rules I1 and I2.</summary>
    private void ResolveImplicitLevels(ReadOnlySpan<int> sequence, byte level)
    {
        var even = (level & 1) == 0;
        foreach (var i in sequence)
        {
            var t = _types[i];
            var raise = even
                ? t switch
                {
                    BidiClass.R => 1,
                    BidiClass.AN or BidiClass.EN => 2,
                    _ => 0,
                }
                : t is BidiClass.L or BidiClass.EN or BidiClass.AN ? 1 : 0;
            _levels[i] = (byte)(level + raise);
        }
    }
}
