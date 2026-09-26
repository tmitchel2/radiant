namespace Radiant.UI.Core;

/// <summary>What a piece of UI is, for assistive technology (as ARIA roles and platform accessibility roles).</summary>
public enum SemanticsRole
{
    /// <summary>Nothing in particular: its children are reported in its place.</summary>
    None,

    /// <summary>A group of related items.</summary>
    Group,

    /// <summary>Text.</summary>
    Text,

    /// <summary>A heading.</summary>
    Heading,

    /// <summary>Something that acts when pressed.</summary>
    Button,

    /// <summary>A link to somewhere.</summary>
    Link,

    /// <summary>A two-state (or mixed) check box.</summary>
    CheckBox,

    /// <summary>One choice of several.</summary>
    RadioButton,

    /// <summary>An on/off switch.</summary>
    Switch,

    /// <summary>A value chosen along a range.</summary>
    Slider,

    /// <summary>Editable text.</summary>
    TextField,

    /// <summary>A list.</summary>
    List,

    /// <summary>An item in a list.</summary>
    ListItem,

    /// <summary>A tab.</summary>
    Tab,

    /// <summary>The tabs of a tab set.</summary>
    TabList,

    /// <summary>A menu.</summary>
    Menu,

    /// <summary>An item in a menu.</summary>
    MenuItem,

    /// <summary>A dialog window within the app.</summary>
    Dialog,

    /// <summary>A picture.</summary>
    Image,

    /// <summary>A progress indicator.</summary>
    ProgressIndicator,

    /// <summary>A scrolling region.</summary>
    ScrollArea,

    /// <summary>A short-lived notice that doesn't take focus.</summary>
    Alert,

    /// <summary>A pop-up tip.</summary>
    Tooltip,

    /// <summary>A movable divider between panes (a splitter); its value is the sized pane's size.</summary>
    Separator,
}
