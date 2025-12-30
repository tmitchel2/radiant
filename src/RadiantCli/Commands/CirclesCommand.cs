using System.Numerics;
using Radiant;

namespace RadiantCli.Commands
{
    public class CirclesCommand : ICommand
    {
        public void Execute(string[] args)
        {
            using var app = new RadiantApplication();
            app.Run("Radiant - Circles Demo", 800, 600, renderer =>
            {
                // Draw filled circles
                renderer.DrawCircleFilled(-250, -150, 80, new Vector4(1, 0, 0, 1), 32);     // Red circle
                renderer.DrawCircleFilled(0, -150, 80, new Vector4(0, 1, 0, 1), 32);        // Green circle
                renderer.DrawCircleFilled(250, -150, 80, new Vector4(0, 0, 1, 1), 32);      // Blue circle

                // Draw circle outlines
                renderer.DrawCircleOutline(-250, 100, 80, new Vector4(1, 1, 0, 1), 32);    // Yellow outline
                renderer.DrawCircleOutline(0, 100, 80, new Vector4(0, 1, 1, 1), 32);       // Cyan outline
                renderer.DrawCircleOutline(250, 100, 80, new Vector4(1, 0, 1, 1), 32);     // Magenta outline

                // Draw ellipses
                renderer.DrawEllipseFilled(-150, -25, 120, 60, new Vector4(1, 0.5f, 0, 0.7f), 32);  // Orange ellipse
                renderer.DrawEllipseOutline(150, -25, 120, 60, new Vector4(0.5f, 0, 1, 1), 32);     // Purple ellipse outline
            });
        }
    }
}
