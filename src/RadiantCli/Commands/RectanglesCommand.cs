using System.Numerics;
using Radiant;
using Radiant.Graphics2D;

namespace RadiantCli.Commands
{
    public class RectanglesCommand : ICommand
    {
        public void Execute(string[] args)
        {
            using var app = new RadiantApplication();
            app.Run("Radiant - Rectangles Demo", 800, 600, renderer =>
            {
                // Draw demo rectangles
                renderer.DrawRectangleFilled(-300, -200, 200, 150, new Vector4(1, 0, 0, 1)); // Red
                renderer.DrawRectangleFilled(-50, -200, 200, 150, new Vector4(0, 1, 0, 1));  // Green
                renderer.DrawRectangleFilled(200, -200, 200, 150, new Vector4(0, 0, 1, 1));  // Blue

                renderer.DrawRectangleOutline(-300, 50, 200, 150, new Vector4(1, 1, 0, 1)); // Yellow outline
                renderer.DrawRectangleOutline(-50, 50, 200, 150, new Vector4(0, 1, 1, 1));  // Cyan outline
                renderer.DrawRectangleOutline(200, 50, 200, 150, new Vector4(1, 0, 1, 1));  // Magenta outline

                // Mixed filled and outlined
                renderer.DrawRectangleFilled(-100, -50, 200, 100, new Vector4(0.5f, 0.5f, 0.5f, 0.5f));
                renderer.DrawRectangleOutline(-100, -50, 200, 100, new Vector4(1, 1, 1, 1));
            });
        }
    }
}
