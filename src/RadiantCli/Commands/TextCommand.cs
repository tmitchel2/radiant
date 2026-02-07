using Radiant;
using Radiant.Graphics;

namespace RadiantCli.Commands
{
    public class TextCommand : ICommand
    {
        public void Execute(string[] args)
        {
            using var app = new RadiantApplication();
            app.Run("Radiant - Text Demo", 800, 600, Handedness.LeftHanded, renderer =>
            {
                // Title
                renderer.DrawText("RADIANT ENGINE", -200, -250, 4, Colors.Cyan500.WithAlpha(1f));

                // Different sizes
                renderer.DrawText("LARGE TEXT", -150, -150, 5, Colors.Rose500.WithAlpha(1f));
                renderer.DrawText("Medium Text", -140, -50, 3, Colors.Emerald500.WithAlpha(1f));
                renderer.DrawText("Small Text", -100, 20, 2, Colors.Amber500.WithAlpha(1f));

                // Numbers and symbols
                renderer.DrawText("0123456789", -150, 100, 3, Colors.Sky500.WithAlpha(1f));
                renderer.DrawText("HELLO WORLD!", -150, 150, 3, Colors.Purple500.WithAlpha(1f));
                renderer.DrawText("PIXEL FONT!", -130, 200, 3, Colors.Fuchsia500.WithAlpha(1f));

                // Different colors
                renderer.DrawText("RED", -350, -200, 3, Colors.Red500.WithAlpha(1f));
                renderer.DrawText("GREEN", -350, -150, 3, Colors.Green500.WithAlpha(1f));
                renderer.DrawText("BLUE", -350, -100, 3, Colors.Blue500.WithAlpha(1f));
                renderer.DrawText("YELLOW", -350, -50, 3, Colors.Yellow500.WithAlpha(1f));
                renderer.DrawText("ORANGE", -350, 0, 3, Colors.Orange500.WithAlpha(1f));
                renderer.DrawText("PINK", -350, 50, 3, Colors.Pink500.WithAlpha(1f));

                // Small info text
                renderer.DrawText("5X7 BITMAP FONT", 100, -250, 2, Colors.Gray400.WithAlpha(1f));
                renderer.DrawText("PIXEL SIZE: 2-5", 100, -220, 2, Colors.Gray400.WithAlpha(1f));
            }, Colors.Slate950.WithAlpha(1f)); // Very dark background
        }
    }
}
