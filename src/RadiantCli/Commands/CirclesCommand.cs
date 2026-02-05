using Radiant;
using Radiant.Graphics;

namespace RadiantCli.Commands
{
    public class CirclesCommand : ICommand
    {
        public void Execute(string[] args)
        {
            using var app = new RadiantApplication();
            app.Run("Radiant - Circles Demo", 800, 600, Handedness.LeftHanded, renderer =>
            {
                // Draw filled circles with Tailwind colors
                renderer.DrawCircleFilled(-250, -150, 80, Colors.Rose500, 32);
                renderer.DrawCircleFilled(0, -150, 80, Colors.Emerald500, 32);
                renderer.DrawCircleFilled(250, -150, 80, Colors.Sky500, 32);

                // Draw circle outlines
                renderer.DrawCircleOutline(-250, 100, 80, Colors.Amber500, 32);
                renderer.DrawCircleOutline(0, 100, 80, Colors.Teal500, 32);
                renderer.DrawCircleOutline(250, 100, 80, Colors.Purple500, 32);

                // Draw ellipses
                renderer.DrawEllipseFilled(-150, -25, 120, 60, Colors.Orange400, 32);
                renderer.DrawEllipseOutline(150, -25, 120, 60, Colors.Violet600, 32);
            }, Colors.Slate50);
        }
    }
}
