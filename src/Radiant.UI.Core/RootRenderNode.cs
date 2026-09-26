namespace Radiant.UI.Core;

/// <summary>The viewport: sized by <see cref="UIRoot.Update"/>, holding the app's element.</summary>
internal sealed class RootRenderNode : RenderNode
{
    public override bool IsHitTestVisible => false;

    public override void Update(HostElement element, HostElement? previous)
    {
    }
}
