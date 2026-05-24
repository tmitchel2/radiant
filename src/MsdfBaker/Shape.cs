using System.Collections.Generic;

namespace Radiant.MsdfBaker
{
    public sealed class Shape
    {
        public List<Contour> Contours { get; } = [];

        /// <summary>
        /// Whether contours are oriented so that the inside is on the left of
        /// each edge (TrueType / PostScript convention). MSDF rasterisation
        /// assumes this; OpenType uses the opposite convention so we flip the
        /// sign at the end.
        /// </summary>
        public bool InverseYAxis { get; set; }
    }
}
