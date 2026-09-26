using System;
using System.Collections.Generic;
using System.Linq;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// An inspector's properties: sections that open and close, each a column of names beside the
/// controls that edit them, the names all one width so the controls line up. With
/// <see cref="Filterable"/>, a field above narrows them to those whose name matches, opening every
/// section while it does.
/// </summary>
/// <param name="Sections">The sections, in order.</param>
public sealed record PropertyGrid(IReadOnlyList<PropertySection> Sections) : Component
{
    /// <summary>The names' column width.</summary>
    public float NameWidth { get; init; } = 120f;

    /// <summary>Whether a filter field narrows the properties by name.</summary>
    public bool Filterable { get; init; }

    /// <summary>What assistive technology calls the grid.</summary>
    public string? Label { get; init; }

    /// <summary>The grid's own layout, added to its default (stretching across its parent).</summary>
    public LayoutStyle? Layout { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var filter = context.UseState(TextEditState.From(""));
        var query = filter.Value.Text.Trim();

        var sections = new List<Element?>();
        foreach (var section in Sections)
        {
            var shown = query.Length == 0 ? section.Properties : [.. section.Properties.Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase))];
            if (shown.Count == 0)
            {
                continue;
            }
            var rows = shown.Select(p => (Element?)Row(p)).ToList();
            sections.Add(new Collapsible(section.Title, new Box { Layout = new LayoutStyle { RowGap = 4 }, Children = rows })
            {
                // Filtering opens every section, so a match is never hidden.
                Key = section.Title,
                Open = query.Length > 0 ? true : null,
                InitiallyOpen = section.InitiallyOpen,
            });
            sections.Add(new Box { Layout = new LayoutStyle { Height = 1, Margin = Edges.Symmetric(0, 4) }, Background = theme.OutlineVariant });
        }
        if (sections.Count > 0)
        {
            sections.RemoveAt(sections.Count - 1);
        }

        return new Box
        {
            Semantics = new Semantics { Role = SemanticsRole.Group, Label = Label },
            Layout = new LayoutStyle { AlignSelf = Align.Stretch, RowGap = 4 }.Merge(Layout ?? default),
            Children =
            [
                Filterable ? new SearchField("Filter properties") { Value = filter.Value, OnChange = filter.Set, Layout = new LayoutStyle { Margin = new Edges(0, 0, 0, 4) } } : null,
                .. sections,
                sections.Count == 0 && query.Length > 0 ? new SurfaceText($"No properties match “{query}”") { Legibility = Legibility.Medium, Layout = new LayoutStyle { Padding = Edges.All(8) } } : null,
            ],
        };

        // Undoes Collapsible's content indent, so names start under the section title's chevron.
        Element Row(PropertyItem property) => new Box
        {
            Semantics = new Semantics { Role = SemanticsRole.Group, Label = property.Name, Description = property.Description },
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 12, MinHeight = 36 + theme.DensityOffset, Margin = new Edges(-24, 0, 0, 0) },
            Children =
            [
                new Box
                {
                    Layout = new LayoutStyle { Width = NameWidth, FlexShrink = 0, RowGap = 2 },
                    Children =
                    [
                        new SurfaceText(property.Name) { TextType = TextType.BodyMedium, Legibility = Legibility.Medium, MaxLines = 1 },
                        property.Description is null ? null : new SurfaceText(property.Description) { TextType = TextType.BodySmall, Legibility = Legibility.Low, MaxLines = 2 },
                    ],
                },
                new Box { Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1, AlignItems = Align.FlexStart }, Children = [property.Editor] },
            ],
        };
    }
}
