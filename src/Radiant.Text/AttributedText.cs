using System;
using System.Collections.Generic;
using System.Text;

namespace Radiant.Text;

/// <summary>
/// Text with styles over ranges of it: the input to paragraph layout. Spans cover the text exactly,
/// in order (plain empty text has one empty span, to carry its style). Build it with <see cref="Plain"/> or <see cref="Builder"/>.
/// </summary>
public sealed class AttributedText
{
    private AttributedText(string text, TextSpan[] spans)
    {
        Text = text;
        Spans = spans;
    }

    /// <summary>The text.</summary>
    public string Text { get; }

    /// <summary>The styled ranges, in order, covering the text exactly.</summary>
    public IReadOnlyList<TextSpan> Spans { get; }

    /// <summary>Text in a single style. Empty text keeps the style, so its one empty line has the style's height.</summary>
    public static AttributedText Plain(string text, TextStyle? style = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new AttributedText(text, [new TextSpan(0, text.Length, style ?? TextStyle.Default)]);
    }

    /// <summary>The style at a UTF-16 index (the last span's at the end of the text).</summary>
    public TextStyle StyleAt(int index)
    {
        var spans = Spans;
        int lo = 0, hi = spans.Count - 1;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (spans[mid].End <= index)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }
        return spans.Count > 0 ? spans[lo].Style : TextStyle.Default;
    }

    /// <summary>Builds attributed text by appending runs of text, each in its own style.</summary>
    public sealed class Builder
    {
        private readonly StringBuilder _text = new();
        private readonly List<TextSpan> _spans = [];

        /// <summary>Appends text in a style; consecutive appends in an equal style merge.</summary>
        public Builder Append(string text, TextStyle style)
        {
            ArgumentNullException.ThrowIfNull(text);
            ArgumentNullException.ThrowIfNull(style);
            if (text.Length == 0)
            {
                return this;
            }
            if (_spans.Count > 0 && _spans[^1].Style == style)
            {
                _spans[^1] = _spans[^1] with { Length = _spans[^1].Length + text.Length };
            }
            else
            {
                _spans.Add(new TextSpan(_text.Length, text.Length, style));
            }
            _text.Append(text);
            return this;
        }

        /// <summary>The attributed text built so far.</summary>
        public AttributedText Build() => new(_text.ToString(), [.. _spans]);
    }
}
