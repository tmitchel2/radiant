using System;
using System.IO;
using System.Linq;

namespace Radiant.Testing;

/// <summary>
/// Checks images against goldens: PNGs checked in with the tests. A golden that doesn't exist yet
/// is written from the image (look at it before checking it in), and setting the environment
/// variable <c>UPDATE_GOLDEN_IMAGES=true</c> rewrites them all from the current rendering. Goldens
/// are made on the machine the tests run on: another GPU can differ by a few edge pixels.
/// </summary>
public static class Golden
{
    /// <summary>The environment variable that rewrites goldens instead of checking them.</summary>
    public const string UpdateVariable = "UPDATE_GOLDEN_IMAGES";

    /// <summary>
    /// Throws <see cref="GoldenMismatchException"/> unless <paramref name="actual"/> matches the
    /// golden called <paramref name="name"/>. On a mismatch, the actual, expected and diff images
    /// are written to the results directory.
    /// </summary>
    public static void AssertMatches(Snapshot actual, string name, GoldenOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        options ??= new GoldenOptions();
        var directory = options.Directory ?? Path.Combine(ProjectDirectory(), "TestData", "Golden");
        var results = options.ResultsDirectory ?? Path.Combine(ProjectDirectory(), "TestResults", "Golden");
        var path = Path.Combine(directory, name + ".png");

        if (!File.Exists(path) || Environment.GetEnvironmentVariable(UpdateVariable) == "true")
        {
            actual.SavePng(path);
            Console.WriteLine($"Wrote golden {path}");
            return;
        }

        var expected = Snapshot.LoadPng(path);
        var actualPath = Path.Combine(results, name + ".actual.png");
        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            actual.SavePng(actualPath);
            throw new GoldenMismatchException(
                $"{name}: the image is {actual.Width}×{actual.Height}, the golden {expected.Width}×{expected.Height}. See {actualPath}.");
        }
        var result = ImageDiff.Compare(actual, expected, options.Threshold);
        if (result.DifferentPixels > options.MaxDifferentPixels)
        {
            actual.SavePng(actualPath);
            expected.SavePng(Path.Combine(results, name + ".expected.png"));
            var diffPath = Path.Combine(results, name + ".diff.png");
            result.Diff.SavePng(diffPath);
            throw new GoldenMismatchException(
                $"{name}: {result.DifferentPixels} pixels differ from the golden (at most {options.MaxDifferentPixels} may). " +
                $"See {actualPath} and {diffPath}; if the change is right, run with {UpdateVariable}=true.");
        }
    }

    // The test project's directory: the nearest one up from the test assembly holding a project file.
    private static string ProjectDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (directory.EnumerateFiles("*.csproj").Any())
            {
                return directory.FullName;
            }
        }
        throw new InvalidOperationException($"No project directory above {AppContext.BaseDirectory}; set GoldenOptions.Directory.");
    }
}
