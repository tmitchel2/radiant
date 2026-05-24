namespace Radiant.MsdfBaker
{
    /// <summary>
    /// Simple shelf packer: glyphs flow left-to-right; when a glyph won't fit
    /// on the current shelf, we close it and open a new one above. Fine for
    /// monotonic-growth offline baking where atlas size is chosen once.
    /// </summary>
    public sealed class ShelfPacker
    {
        public int Width { get; }
        public int Height { get; }
        public int Padding { get; }

        private int _cursorX;
        private int _shelfY;
        private int _shelfHeight;

        public ShelfPacker(int width, int height, int padding = 2)
        {
            Width = width;
            Height = height;
            Padding = padding;
        }

        public bool TryPack(int w, int h, out int x, out int y)
        {
            var advance = w + Padding;
            var lineAdvance = h + Padding;
            if (_cursorX + advance > Width)
            {
                _cursorX = 0;
                _shelfY += _shelfHeight;
                _shelfHeight = 0;
            }
            if (_shelfY + lineAdvance > Height)
            {
                x = y = 0;
                return false;
            }
            x = _cursorX;
            y = _shelfY;
            _cursorX += advance;
            if (lineAdvance > _shelfHeight) _shelfHeight = lineAdvance;
            return true;
        }
    }
}
