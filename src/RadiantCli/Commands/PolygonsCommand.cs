using System.Numerics;
using Radiant;
using Radiant.Graphics2D;

namespace RadiantCli.Commands
{
    public class PolygonsCommand : ICommand
    {
        public void Execute(string[] args)
        {
            using var app = new RadiantApplication();
            app.Run("Radiant - Polygons Demo", 800, 600, renderer =>
            {
                // Row 1: Filled polygons
                renderer.DrawPolygonFilled(-300, -150, 70, 3, new Vector4(1, 0, 0, 1));     // Triangle (red)
                renderer.DrawPolygonFilled(-150, -150, 70, 4, new Vector4(0, 1, 0, 1));     // Square (green)
                renderer.DrawPolygonFilled(0, -150, 70, 5, new Vector4(0, 0, 1, 1));        // Pentagon (blue)
                renderer.DrawPolygonFilled(150, -150, 70, 6, new Vector4(1, 1, 0, 1));      // Hexagon (yellow)
                renderer.DrawPolygonFilled(300, -150, 70, 8, new Vector4(1, 0, 1, 1));      // Octagon (magenta)

                // Row 2: Outlined polygons
                renderer.DrawPolygonOutline(-300, 100, 70, 3, new Vector4(1, 0.5f, 0, 1));  // Triangle outline (orange)
                renderer.DrawPolygonOutline(-150, 100, 70, 4, new Vector4(0.5f, 1, 0, 1));  // Square outline (lime)
                renderer.DrawPolygonOutline(0, 100, 70, 5, new Vector4(0, 0.5f, 1, 1));     // Pentagon outline (sky blue)
                renderer.DrawPolygonOutline(150, 100, 70, 6, new Vector4(1, 1, 0.5f, 1));   // Hexagon outline (light yellow)
                renderer.DrawPolygonOutline(300, 100, 70, 8, new Vector4(1, 0.5f, 1, 1));   // Octagon outline (light magenta)

                // Center: mixed filled and outlined
                renderer.DrawPolygonFilled(0, -25, 50, 7, new Vector4(0.5f, 0.5f, 0.5f, 0.5f)); // Heptagon (semi-transparent gray)
                renderer.DrawPolygonOutline(0, -25, 50, 7, new Vector4(1, 1, 1, 1));            // White outline
            });
        }
    }
}
