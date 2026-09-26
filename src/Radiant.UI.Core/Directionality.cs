using Radiant.Layout;
using Radiant.Text;

namespace Radiant.UI.Core;

/// <summary>
/// Sets which way everything inside reads: right to left for Arabic and Hebrew interfaces. Rows
/// run from the right, edges' starts are on the right, and text starts there, so a layout written
/// once in start and end terms mirrors as a whole; components that measure the pointer or take
/// arrow keys ask <see cref="DirectionalityHooks.UseDirection"/> which way is forward.
/// </summary>
/// <param name="Direction">Which way it reads.</param>
/// <param name="Child">What it applies to.</param>
public sealed record Directionality(TextDirection Direction, Element? Child) : Component
{
    /// <summary>The direction in force, or null where nothing set one (text then takes its own).</summary>
    public static Context<TextDirection?> Context { get; } = new(null);

    /// <inheritdoc/>
    public override Element? Build(BuildContext context) => Context.Provide(Direction, new Box
    {
        Layout = new LayoutStyle { Direction = Direction, FlexGrow = 1, FlexShrink = 1, AlignSelf = Align.Stretch },
        Children = [Child],
    });
}
