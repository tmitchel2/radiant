using Radiant;
using Radiant.Graphics;
using Radiant.Graphics2D;

namespace RadiantCli.Commands
{
    public class TextCommand : ICommand
    {
        public void Execute(string[] args)
        {
            using var app = new RadiantApplication();
            var font = MsdfFont.LoadEmbedded("default");
            app.Run("Radiant - Text Demo", 800, 600, Handedness.LeftHanded, renderer =>
            {
                // Title
                renderer.DrawText(font, "RADIANT ENGINE", -200, -250, 28, Colors.Cyan500.WithAlpha(1f));

                // Different sizes
                renderer.DrawText(font, "LARGE TEXT", -150, -150, 35, Colors.Rose500.WithAlpha(1f));
                renderer.DrawText(font, "Medium Text", -140, -50, 21, Colors.Emerald500.WithAlpha(1f));
                renderer.DrawText(font, "Small Text", -100, 20, 14, Colors.Amber500.WithAlpha(1f));

                // Numbers and symbols
                renderer.DrawText(font, "0123456789", -150, 100, 21, Colors.Sky500.WithAlpha(1f));
                renderer.DrawText(font, "HELLO WORLD!", -150, 150, 21, Colors.Purple500.WithAlpha(1f));
                renderer.DrawText(font, "PIXEL FONT!", -130, 200, 21, Colors.Fuchsia500.WithAlpha(1f));

                // Different colors
                renderer.DrawText(font, "RED", -350, -200, 21, Colors.Red500.WithAlpha(1f));
                renderer.DrawText(font, "GREEN", -350, -150, 21, Colors.Green500.WithAlpha(1f));
                renderer.DrawText(font, "BLUE", -350, -100, 21, Colors.Blue500.WithAlpha(1f));
                renderer.DrawText(font, "YELLOW", -350, -50, 21, Colors.Yellow500.WithAlpha(1f));
                renderer.DrawText(font, "ORANGE", -350, 0, 21, Colors.Orange500.WithAlpha(1f));
                renderer.DrawText(font, "PINK", -350, 50, 21, Colors.Pink500.WithAlpha(1f));

                // Small info text
                renderer.DrawText(font, "MSDF FONT", 100, -250, 14, Colors.Gray400.WithAlpha(1f));
                renderer.DrawText(font, "PIXEL SIZE: 14-35", 100, -220, 14, Colors.Gray400.WithAlpha(1f));
            }, Colors.Slate950.WithAlpha(1f)); // Very dark background
        }
    }
}
