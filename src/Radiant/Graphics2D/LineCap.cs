namespace Radiant.Graphics2D
{
    /// <summary>How a stroked path is finished at its two free ends.</summary>
    public enum LineCap
    {
        /// <summary>Stops exactly at the endpoint. The cheapest, and what a bare quad gives.</summary>
        Butt,

        /// <summary>A half-disc of the stroke's own radius, centred on the endpoint.</summary>
        Round,

        /// <summary>A square extending half a width past the endpoint, along the direction.</summary>
        Square,
    }
}
