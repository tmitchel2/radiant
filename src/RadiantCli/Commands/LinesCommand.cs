using System;
using System.Numerics;
using Radiant;
using Radiant.Graphics;

namespace RadiantCli.Commands
{
    public class LinesCommand : ICommand
    {
        public void Execute(string[] args)
        {
            using var app = new RadiantApplication();
            app.Run("Radiant - Lines Demo", 800, 600, Handedness.LeftHanded, renderer =>
            {
                // Draw individual lines
                renderer.DrawLine(new Vector2(-350, -250), new Vector2(-150, -250), new Vector4(1, 0, 0, 1)); // Red horizontal
                renderer.DrawLine(new Vector2(-350, -200), new Vector2(-350, -100), new Vector4(0, 1, 0, 1)); // Green vertical
                renderer.DrawLine(new Vector2(-300, -150), new Vector2(-200, -200), new Vector4(0, 0, 1, 1)); // Blue diagonal

                // Draw a star pattern with lines
                var starCenter = new Vector2(0, -150);
                var starRadius = 100f;
                for (var i = 0; i < 8; i++)
                {
                    var angle = (i / 8f) * MathF.PI * 2;
                    var endpoint = new Vector2(
                        starCenter.X + MathF.Cos(angle) * starRadius,
                        starCenter.Y + MathF.Sin(angle) * starRadius);
                    renderer.DrawLine(starCenter, endpoint, new Vector4(1, 1, 0, 1)); // Yellow
                }

                // Draw a polyline (sine wave)
                var points = new Vector2[50];
                for (var i = 0; i < 50; i++)
                {
                    var x = -350 + (i * 14);
                    var y = 100 + MathF.Sin(i * 0.3f) * 50;
                    points[i] = new Vector2(x, y);
                }
                renderer.DrawPolyline(points, new Vector4(0, 1, 1, 1)); // Cyan

                // Draw a grid
                for (var x = -300; x <= 300; x += 50)
                {
                    renderer.DrawLine(new Vector2(x, 150), new Vector2(x, 250), new Vector4(0.5f, 0.5f, 0.5f, 1));
                }
                for (var y = 150; y <= 250; y += 50)
                {
                    renderer.DrawLine(new Vector2(-300, y), new Vector2(300, y), new Vector4(0.5f, 0.5f, 0.5f, 1));
                }
            }, Colors.Slate50);
        }
    }
}
