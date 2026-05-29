namespace Radiant.Layout;

/// <summary>Whether flex items wrap onto multiple lines (mirrors CSS <c>flex-wrap</c>).</summary>
public enum FlexWrap
{
    /// <summary>Single line; items may overflow (Yoga default).</summary>
    NoWrap,

    /// <summary>Wrap onto multiple lines.</summary>
    Wrap,

    /// <summary>Wrap onto multiple lines in reverse cross-axis order.</summary>
    WrapReverse,
}
