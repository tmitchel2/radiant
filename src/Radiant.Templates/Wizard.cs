using System;
using System.Collections.Generic;
using System.Globalization;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>
/// A step-by-step flow (an installer, a new-project assistant): the steps listed down the left,
/// done ones ticked and revisitable, the current step's title and content, and Cancel, Back and
/// Next (Finish on the last step) along the bottom. Controlled: shows <paramref name="Current"/>
/// and reports moves through <paramref name="OnStep"/>.
/// </summary>
/// <param name="Steps">The steps.</param>
/// <param name="Current">The current step's index.</param>
/// <param name="OnStep">Called with the step to go to.</param>
public sealed partial record Wizard(IReadOnlyList<WizardStep> Steps, int Current, Action<int>? OnStep) : Component
{
    [TestId<SurfaceButton>] public static partial string Back { get; }
    [TestId<SurfaceButton>] public static partial string Next { get; }
    [TestId<SurfaceButton>] public static partial string Cancel { get; }

    /// <summary>The flow's name, over the list of steps.</summary>
    public string? Title { get; init; }

    /// <summary>What Finish does.</summary>
    public Action? OnFinish { get; init; }

    /// <summary>What Cancel does; null for no Cancel button.</summary>
    public Action? OnCancel { get; init; }

    /// <summary>The last step's button.</summary>
    public string FinishText { get; init; } = "Finish";

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var current = Math.Clamp(Current, 0, Math.Max(0, Steps.Count - 1));
        var step = Steps.Count == 0 ? null : Steps[current];
        var last = current == Steps.Count - 1;
        var go = OnStep;
        var finish = OnFinish;

        var list = new List<Element?>();
        for (var i = 0; i < Steps.Count; i++)
        {
            var index = i;
            list.Add(new StepRow(Steps[i].Title, i + 1, i < current, i == current, i < current ? () => go?.Invoke(index) : null));
        }

        return new Surface
        {
            SurfaceColor = SurfaceName.Surface,
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, FlexGrow = 1 },
            Children =
            [
                new Surface
                {
                    SurfaceColor = SurfaceName.SurfaceContainerLow,
                    Semantics = new Semantics { Role = SemanticsRole.List, Label = "Steps" },
                    Layout = new LayoutStyle { Width = 240, Padding = new Edges(12, 20, 12, 20), RowGap = 4, FlexShrink = 0 },
                    Children =
                    [
                        Title is null ? null : new SurfaceText(Title) { TextType = TextType.TitleMedium, Layout = new LayoutStyle { Padding = new Edges(8, 0, 8, 12) } },
                        .. list,
                    ],
                },
                new Box
                {
                    Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 },
                    Children =
                    [
                        new ScrollArea
                        {
                            Layout = new LayoutStyle { FlexGrow = 1 },
                            ContentLayout = new LayoutStyle { Padding = new Edges(32, 28, 32, 24), RowGap = 20 },
                            Children =
                            [
                                step is null ? null : new Box
                                {
                                    Layout = new LayoutStyle { RowGap = 4 },
                                    Children =
                                    [
                                        new SurfaceText(string.Format(CultureInfo.InvariantCulture, "Step {0} of {1}", current + 1, Steps.Count)) { TextType = TextType.LabelLarge, Legibility = Legibility.Medium },
                                        new SurfaceText(step.Title) { TextType = TextType.HeadlineSmall, HeadingLevel = 1 },
                                        step.Description is null ? null : new SurfaceText(step.Description) { Legibility = Legibility.Medium },
                                    ],
                                },
                                step?.Content,
                            ],
                        },
                        new Divider(),
                        new Box
                        {
                            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 8, Padding = Edges.Symmetric(24, 12) },
                            Children =
                            [
                                OnCancel is null ? null : new SurfaceButton("Cancel", ButtonVariant.Text) { TestId = Cancel, OnPress = OnCancel },
                                new Box { Layout = new LayoutStyle { FlexGrow = 1 } },
                                new SurfaceButton("Back", ButtonVariant.Outlined) { TestId = Back, OnPress = () => go?.Invoke(current - 1), ShowDisabled = current == 0 ? true : null },
                                new SurfaceButton(last ? FinishText : "Next")
                                {
                                    TestId = Next,
                                    OnPress = last ? () => finish?.Invoke() : () => go?.Invoke(current + 1),
                                    ShowDisabled = step is { CanContinue: false } ? true : null,
                                },
                            ],
                        },
                    ],
                },
            ],
        };
    }

    /// <summary>A step in the list: its number (a tick once done), its title, and a fill when current.</summary>
    private sealed record StepRow(string Title, int Number, bool Done, bool IsCurrent, Action? Revisit) : Component
    {
        public override Element? Build(BuildContext context)
        {
            Element?[] children =
            [
                new Surface
                {
                    SurfaceColor = Done || IsCurrent ? SurfaceName.Primary : SurfaceName.SurfaceContainerHighest,
                    CornerShape = CornerShapeRole.Full,
                    Layout = new LayoutStyle { Width = 24, Height = 24, AlignItems = Align.Center, JustifyContent = Justify.Center },
                    Children =
                    [
                        Done
                            ? new SurfaceIcon("check") { IconSize = 16 }
                            : new SurfaceText(Number.ToString(CultureInfo.InvariantCulture)) { TextType = TextType.LabelMedium },
                    ],
                },
                new SurfaceText(Title) { TextType = TextType.LabelLarge, Legibility = Done || IsCurrent ? null : Legibility.Medium, MaxLines = 1 },
            ];
            var layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 12, Height = 40, Padding = Edges.Symmetric(8, 0) };
            return Revisit is null
                ? new Surface
                {
                    SurfaceColor = IsCurrent ? SurfaceName.Secondary : null,
                    SurfaceContainerToggle = IsCurrent ? true : null,
                    CornerShape = CornerShapeRole.Medium,
                    Semantics = new Semantics { Role = SemanticsRole.ListItem, Label = Title, Selected = IsCurrent },
                    Layout = layout,
                    Children = children,
                }
                : new PressableSurface { Role = SemanticsRole.ListItem, Label = Title, CornerShape = CornerShapeRole.Medium, OnPress = Revisit, Layout = layout, Children = children };
        }
    }
}
