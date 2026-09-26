using Facebook.Yoga;
using Radiant.Text;
using static Facebook.Yoga.YGNodeLayoutAPI;

namespace Radiant.UI.Core;

/// <summary>The direction layout resolved for a node, as text should take it.</summary>
internal static class LayoutDirection
{
    /// <summary>
    /// Right to left where the node was laid out right to left; otherwise null, so text finds its
    /// own direction from its first strong character (Arabic in a left-to-right UI still reads
    /// right to left). Yoga resolves a node's direction before it measures it.
    /// </summary>
    public static TextDirection? Of(Node node) => YGNodeLayoutGetDirection(node) == YGDirection.RTL ? TextDirection.RightToLeft : null;
}
