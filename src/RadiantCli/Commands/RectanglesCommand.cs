using Radiant;
using Radiant.Graphics;

namespace RadiantCli.Commands
{
    public class RectanglesCommand : ICommand
    {
        public void Execute(string[] args)
        {
            using var app = new RadiantApplication();
            app.Run("Radiant - Rectangles Demo", 800, 600, Handedness.LeftHanded, renderer =>
            {
                // Draw demo rectangles with Tailwind colors
                renderer.DrawRectangleFilled(-300, -200, 200, 150, Colors.Red500.WithAlpha(1f));
                renderer.DrawRectangleFilled(-50, -200, 200, 150, Colors.Green500.WithAlpha(1f));
                renderer.DrawRectangleFilled(200, -200, 200, 150, Colors.Blue500.WithAlpha(1f));

                renderer.DrawRectangleOutline(-300, 50, 200, 150, Colors.Yellow500.WithAlpha(1f));
                renderer.DrawRectangleOutline(-50, 50, 200, 150, Colors.Cyan500.WithAlpha(1f));
                renderer.DrawRectangleOutline(200, 50, 200, 150, Colors.Fuchsia500.WithAlpha(1f));

                // Mixed filled and outlined
                renderer.DrawRectangleFilled(-100, -50, 200, 100, Colors.Slate400.WithAlpha(1f));
                renderer.DrawRectangleOutline(-100, -50, 200, 100, Colors.White.WithAlpha(1f));
            }, Colors.Slate50.WithAlpha(1f));
        }
    }
}
