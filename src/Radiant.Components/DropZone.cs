using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Radiant.Layout;
using Radiant.Platform;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A place to drop files from the Finder (or another app), with a Browse button that opens the
/// platform's file panel for the same thing. Dropped files outside <see cref="Filters"/> are left
/// out, and the zone says how many it refused.
/// </summary>
/// <param name="OnFiles">Called with the files dropped or chosen.</param>
public sealed partial record DropZone(Action<IReadOnlyList<string>> OnFiles) : Component
{
    [TestId<SurfaceButton>] public static partial string BrowseButton { get; }

    /// <summary>What it asks for.</summary>
    public string Title { get; init; } = "Drop files here";

    /// <summary>A line under the title (what kinds, how large).</summary>
    public string? Description { get; init; }

    /// <summary>The kinds of file accepted, dropped or chosen; any if empty.</summary>
    public IReadOnlyList<FileFilter> Filters { get; init; } = [];

    /// <summary>Whether several files can be chosen at once.</summary>
    public bool AllowMultiple { get; init; } = true;

    /// <summary>The zone's size and placement.</summary>
    public LayoutStyle? Layout { get; init; }

    /// <summary>
    /// The files among <paramref name="paths"/> that <paramref name="filters"/> accept (all of them
    /// when there are no filters), by extension, ignoring case.
    /// </summary>
    public static IReadOnlyList<string> Accepted(IReadOnlyList<string> paths, IReadOnlyList<FileFilter> filters)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(filters);
        if (filters.Count == 0)
        {
            return paths;
        }
        var extensions = filters.SelectMany(f => f.Extensions).Select(e => e.TrimStart('.')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return [.. paths.Where(p => extensions.Contains(Path.GetExtension(p).TrimStart('.')))];
    }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var platform = context.UsePlatform();
        var refused = context.UseState(0);
        var latest = context.UseRef(this);
        latest.Value = this;

        void Take(IReadOnlyList<string> paths)
        {
            var props = latest.Value;
            var accepted = Accepted(paths, props.Filters);
            refused.Set(paths.Count - accepted.Count);
            if (accepted.Count > 0)
            {
                props.OnFiles(accepted);
            }
        }

        async Task Browse()
        {
            var props = latest.Value;
            var chosen = await platform.Dialogs.OpenAsync(new OpenFileOptions { AllowMultiple = props.AllowMultiple, Filters = props.Filters }).ConfigureAwait(true);
            if (chosen.Count > 0)
            {
                Take(chosen);
            }
        }

        return new Surface
        {
            SurfaceColor = SurfaceName.SurfaceContainerLow,
            ShowOutline = true,
            OutlineVariant = true,
            OutlineWidth = 2,
            CornerShape = CornerShapeRole.Large,
            Semantics = new Semantics { Role = SemanticsRole.Group, Label = Title, Description = Description },
            Layout = new LayoutStyle { AlignItems = Align.Center, JustifyContent = Justify.Center, RowGap = 8, MinHeight = 160, Padding = Edges.All(24), AlignSelf = Align.Stretch }.Merge(Layout ?? default),
            Children =
            [
                new Box
                {
                    // The drop target is the whole zone, over its contents.
                    Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = Edges.All(0) },
                    OnFileDrop = e =>
                    {
                        Take(e.Paths);
                        e.Handled = true;
                    },
                },
                new SurfaceIcon("cloud_upload") { IconSize = 32, Legibility = Legibility.Medium },
                new SurfaceText(Title) { TextType = TextType.TitleMedium },
                Description is null ? null : new SurfaceText(Description) { Legibility = Legibility.Medium, Alignment = Radiant.Text.TextAlignment.Center },
                new SurfaceButton("Browse", ButtonVariant.Tonal) { TestId = BrowseButton, OnPress = () => _ = Browse(), Layout = new LayoutStyle { Margin = new Edges(0, 4, 0, 0) } },
                refused.Value == 0 ? null : new SurfaceText(refused.Value == 1 ? "1 file wasn't a kind this takes" : $"{refused.Value} files weren't a kind this takes")
                {
                    TextType = TextType.BodySmall,
                    Legibility = Legibility.Medium,
                },
            ],
        };
    }
}
