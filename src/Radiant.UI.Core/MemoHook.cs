namespace Radiant.UI.Core;

/// <summary>A value computed once per set of dependencies.</summary>
internal sealed class MemoHook : IHook
{
    public object? Dependencies { get; set; }

    public object? Value { get; set; }

    public void Release()
    {
    }
}
