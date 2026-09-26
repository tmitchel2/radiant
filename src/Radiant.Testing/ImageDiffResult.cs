namespace Radiant.Testing;

/// <summary>What <see cref="ImageDiff.Compare"/> found.</summary>
/// <param name="DifferentPixels">How many pixels differ beyond the threshold.</param>
/// <param name="Diff">The first image faded, with the differing pixels in red.</param>
public sealed record ImageDiffResult(int DifferentPixels, Snapshot Diff);
