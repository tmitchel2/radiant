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
                renderer.DrawCircleFilled(-250, -150, 80, Colors.Rose500.WithAlpha(1f), 32);
                renderer.DrawCircleFilled(0, -150, 80, Colors.Emerald500.WithAlpha(1f), 32);
                renderer.DrawCircleFilled(250, -150, 80, Colors.Sky500.WithAlpha(1f), 32);

                // Draw circle outlines
                renderer.DrawCircleOutline(-250, 100, 80, Colors.Amber500.WithAlpha(1f), 32);
                renderer.DrawCircleOutline(0, 100, 80, Colors.Teal500.WithAlpha(1f), 32);
                renderer.DrawCircleOutline(250, 100, 80, Colors.Purple500.WithAlpha(1f), 32);

                // Draw ellipses
                renderer.DrawEllipseFilled(-150, -25, 120, 60, Colors.Orange400.WithAlpha(1f), 32);
                renderer.DrawEllipseOutline(150, -25, 120, 60, Colors.Violet600.WithAlpha(1f), 32);
            }, Colors.Slate50.WithAlpha(1f));
        }
    }
}
