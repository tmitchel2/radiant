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
                renderer.DrawRectangleFilled(-300, -200, 200, 150, Colors.Red500);
                renderer.DrawRectangleFilled(-50, -200, 200, 150, Colors.Green500);
                renderer.DrawRectangleFilled(200, -200, 200, 150, Colors.Blue500);

                renderer.DrawRectangleOutline(-300, 50, 200, 150, Colors.Yellow500);
                renderer.DrawRectangleOutline(-50, 50, 200, 150, Colors.Cyan500);
                renderer.DrawRectangleOutline(200, 50, 200, 150, Colors.Fuchsia500);

                // Mixed filled and outlined
                renderer.DrawRectangleFilled(-100, -50, 200, 100, Colors.Slate400);
                renderer.DrawRectangleOutline(-100, -50, 200, 100, Colors.White);
            }, Colors.Slate50);
        }
    }
}
