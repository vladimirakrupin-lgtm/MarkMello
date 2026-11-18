namespace MarkMello.Infrastructure.Markdown;

/// <summary>
/// Protects code contexts while <see cref="MarkdownLatexMathDelimiters"/>
/// hunts for math delimiters. The scanner only ever asks about increasing
/// offsets, so the walk is a single forward pass over the document.
/// </summary>
internal static partial class MarkdownLatexMathDelimiters
{
    private sealed class ScanState
    {
        private readonly string _source;
        private readonly string[] _lines;
        private readonly int[] _lineStarts;

        public ScanState(string source, string[] lines)
        {
            _source = source;
            _lines = lines;
            _lineStarts = new int[lines.Length];
            var total = 0;
            for (var i = 0; i < lines.Length; i++)
            {
                _lineStarts[i] = total;
                total += lines[i].Length + 1;
            }
        }

        public string Source => _source;

        /// <summary>
        /// True when <c>\[</c> at <paramref name="offset"/> may open a display
        /// block: only at the start of a line (after indentation), and never
        /// inside a table row — there it is LaTeX's line break.
        /// </summary>
        public bool IsDisplayOpener(int offset)
        {
            var line = LineAt(offset);
            if (IsTableRow(line))
            {
                return false;
            }

            var lineStart = LineStart(offset);
            for (var i = lineStart; i < offset; i++)
            {
                if (_source[i] != ' ' && _source[i] != '\t')
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// If <paramref name="offset"/> begins a protected region, returns its
        /// exclusive end. Fence and indented-code recognition backtracks to the
        /// start of the current line, so the scanner may ask from any offset;
        /// the jump then moves past that line (or off the fence, see
        /// <see cref="FindBlockEnd"/>).
        /// </summary>
        public bool TrySkip(int offset, out int end)
        {
            end = 0;

            if (_source.AsSpan(offset).StartsWith("<!--", StringComparison.Ordinal))
            {
                var commentEnd = _source.IndexOf("-->", offset + 4, StringComparison.Ordinal);
                end = commentEnd < 0 ? _source.Length : commentEnd + 3;
                return true;
            }

            if (TryGetProtectedLine(LineAt(offset), out end) && end > offset)
            {
                return true;
            }

            return _source[offset] == '`' && TrySkipCodeSpan(offset, out end);
        }

        /// <summary>
        /// True when <paramref name="line"/> is a fenced or indented code line.
        /// Used by the display parser, which must not look for a closing
        /// <c>\]</c> inside a code block that starts in the middle of a
        /// multibyte sequence and was never protected.
        /// </summary>
        public bool IsCodeLine(int line) => TryGetProtectedLine(line, out _);

        private bool TryGetProtectedLine(int line, out int end)
        {
            end = 0;
            if (line < 0 || line >= _lines.Length)
            {
                return false;
            }

            var text = _lines[line];
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (TryReadFence(text, out var fenceChar, out var fenceLength))
            {
                end = TryFindClosingFence(line + 1, fenceChar, fenceLength)
                    ?? _source.Length;
                return true;
            }

            if (IsIndentedCode(text))
            {
                end = Math.Min(_source.Length, _lineStarts[line] + text.Length + 1);
                return true;
            }

            return false;
        }

        private int? TryFindClosingFence(int fromLine, char fenceChar, int fenceLength)
        {
            for (var line = fromLine; line < _lines.Length; line++)
            {
                var candidate = _lines[line];
                if (TryReadFence(candidate, out var closeChar, out var closeLength)
                    && closeChar == fenceChar
                    && closeLength >= fenceLength)
                {
                    return Math.Min(_source.Length, _lineStarts[line] + candidate.Length + 1);
                }
            }

            return null;
        }

        /// <summary>
        /// CommonMark code span: a backtick run of length N closes at the next
        /// backtick run of exactly N. An unmatched run is plain text.
        /// </summary>
        private bool TrySkipCodeSpan(int offset, out int end)
        {
            end = 0;
            var runLength = CountRun(offset, '`');
            var search = offset + runLength;

            while (search < _source.Length && _source[search] != '\n')
            {
                if (_source[search] != '`')
                {
                    search++;
                    continue;
                }

                var closeLength = CountRun(search, '`');
                if (closeLength == runLength)
                {
                    end = search + closeLength;
                    return true;
                }

                search += closeLength;
            }

            return false;
        }

        private int CountRun(int offset, char token)
        {
            var i = offset;
            while (i < _source.Length && _source[i] == token)
            {
                i++;
            }

            return i - offset;
        }

        private int LineStart(int offset)
        {
            var newline = offset == 0 ? -1 : _source.LastIndexOf('\n', offset - 1);
            return newline + 1;
        }

        private int LineAt(int offset)
        {
            var lineStart = LineStart(offset);
            var index = Array.BinarySearch(_lineStarts, lineStart);
            return index < 0 ? Math.Max(0, ~index - 1) : index;
        }

        /// <summary>Zero-based line index of <paramref name="offset"/>.</summary>
        public int LineOf(int offset) => LineAt(offset);

        private bool IsTableRow(int line)
            => line >= 0 && line < _lines.Length && _lines[line].Contains('|');

        /// <summary>Character offset where <paramref name="line"/> starts.</summary>
        public int StartOfLine(int line) => _lineStarts[line];

        public int LineCount => _lines.Length;

        private static bool IsIndentedCode(string line)
            => line.StartsWith("    ", StringComparison.Ordinal) || line.StartsWith('\t');

        private static bool TryReadFence(string line, out char fenceChar, out int fenceLength)
        {
            fenceChar = default;
            fenceLength = 0;

            var start = 0;
            while (start < line.Length && start < 4 && line[start] == ' ')
            {
                start++;
            }

            if (start > 3 || start >= line.Length || (line[start] != '`' && line[start] != '~'))
            {
                return false;
            }

            fenceChar = line[start];
            var end = start;
            while (end < line.Length && line[end] == fenceChar)
            {
                end++;
            }

            fenceLength = end - start;
            return fenceLength >= 3;
        }
    }
}
