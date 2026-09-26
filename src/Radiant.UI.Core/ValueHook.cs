namespace Radiant.UI.Core;

/// <summary>A hook that just holds a value: state, refs.</summary>
internal sealed class ValueHook(object value) : IHook
{
    public object Value { get; } = value;

    public void Release()
    {
    }
}
