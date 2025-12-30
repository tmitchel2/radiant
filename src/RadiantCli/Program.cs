using System;
using RadiantCli.Commands;

namespace RadiantCli
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                ShowHelp();
                return;
            }

            var commandName = args[0].ToLowerInvariant();
            ICommand? command = commandName switch
            {
                "rectangles" => new RectanglesCommand(),
                "circles" => new CirclesCommand(),
                "lines" => new LinesCommand(),
                "polygons" => new PolygonsCommand(),
                "text" => new TextCommand(),
                _ => null
            };

            if (command == null)
            {
                Console.WriteLine($"Unknown command: {commandName}");
                ShowHelp();
                return;
            }

            command.Execute(args[1..]);
        }

        private static void ShowHelp()
        {
            Console.WriteLine("RadiantCli - 2D Graphics Demonstrations");
            Console.WriteLine();
            Console.WriteLine("Usage: RadiantCli <command>");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  rectangles  - Draw filled and outlined rectangles");
            Console.WriteLine("  circles     - Draw circles and ellipses");
            Console.WriteLine("  lines       - Draw lines and polylines");
            Console.WriteLine("  polygons    - Draw triangles and polygons");
            Console.WriteLine("  text        - Draw text using bitmap font");
        }
    }
}
