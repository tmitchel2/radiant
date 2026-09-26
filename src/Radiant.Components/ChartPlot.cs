using System.Numerics;
using Radiant.Graphics2D;

namespace Radiant.Components;

/// <summary>What a chart paints its plot with: the renderer, the plot's place, each series' colour and the category under the pointer.</summary>
internal readonly record struct ChartPlot(Renderer2D Renderer, Vector2 Origin, Vector2 Size, Vector4[] Colors, int? Hover);
