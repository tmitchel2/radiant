using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Templates;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Gallery;

/// <summary>A hundred thousand generated people in a sortable, selectable table.</summary>
internal sealed partial record TablePage : Component
{
    [TestId<SurfaceButton>] public static partial string Invite { get; }

    private static readonly string[] s_first = ["Ada", "Grace", "Alan", "Katherine", "Edsger", "Barbara", "Donald", "Frances", "Linus", "Margaret"];
    private static readonly string[] s_last = ["Lovelace", "Hopper", "Turing", "Johnson", "Dijkstra", "Liskov", "Knuth", "Allen", "Torvalds", "Hamilton"];
    private static readonly string[] s_cities = ["London", "Paris", "Berlin", "Tokyo", "Lagos", "Lima", "Oslo", "Seoul"];

    private const int Count = 100_000;

    private static string Name(int i) => $"{s_first[i % 10]} {s_last[i / 10 % 10]} {(i / 100).ToString(CultureInfo.InvariantCulture)}";

    private static int Age(int i) => 18 + (int)((uint)(i * 2654435761u) % 60u);

    private static string City(int i) => s_cities[(i * 7 + i / 3) % s_cities.Length];

    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var sort = context.UseState(((int Column, SortDirection Direction)?)null);
        var selection = context.UseState<IReadOnlySet<int>>(new HashSet<int>());
        var order = context.UseMemo(() =>
        {
            var rows = Enumerable.Range(0, Count).ToArray();
            if (sort.Value is { } s)
            {
                Comparison<int> compare = s.Column switch
                {
                    0 => (a, b) => string.CompareOrdinal(Name(a), Name(b)),
                    1 => (a, b) => Age(a).CompareTo(Age(b)),
                    _ => (a, b) => string.CompareOrdinal(City(a), City(b)),
                };
                Array.Sort(rows, s.Direction == SortDirection.Ascending ? compare : (a, b) => compare(b, a));
            }
            return rows;
        }, sort.Value);
        DataColumn[] columns =
        [
            new("Name", r => new SurfaceText(Name(order[r])) { MaxLines = 1 }) { Width = 240, Sortable = true },
            new("Age", r => new SurfaceText(Age(order[r]).ToString(CultureInfo.InvariantCulture))) { Width = 90, Sortable = true, Alignment = Radiant.Text.TextAlignment.End },
            new("City", r => new SurfaceText(City(order[r]))) { Width = 160, Sortable = true },
            new("Status", r => order[r] % 5 == 0 ? new Chip("Invited") : new SurfaceText("Active") { Legibility = Legibility.Medium }) { Grow = true },
        ];
        return new Box
        {
            Layout = new LayoutStyle { RowGap = 24 },
            Children =
            [
                new PageHeading("People")
                {
                    Description = $"{Count.ToString("N0", CultureInfo.InvariantCulture)} rows, {selection.Value.Count.ToString("N0", CultureInfo.InvariantCulture)} selected",
                    Actions = [new SurfaceButton("Invite") { TestId = Invite, Icon = "add" }],
                },
                new Surface
                {
                    ShowOutline = true,
                    CornerShape = CornerShapeRole.Medium,
                    ClipContent = true,
                    Layout = new LayoutStyle { Height = 560, Padding = Edges.All(1) },
                    Children =
                    [
                        new DataTable(columns, Count)
                        {
                            Label = "People",
                            MultiSelect = true,
                            ShowCheckboxes = true,
                            SortColumn = sort.Value?.Column,
                            SortDirection = sort.Value?.Direction ?? SortDirection.Ascending,
                            OnSort = (c, d) => sort.Set((c, d)),
                            Selection = selection.Value,
                            OnSelectionChange = selection.Set,
                        },
                    ],
                },
            ],
        };
    }
}
