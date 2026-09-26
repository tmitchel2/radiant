namespace Radiant.Theming;

/// <summary>
/// The colours in force at a point in the tree: the surface, content on it, and focused content.
/// Inherited down the tree, each surface component applying its <see cref="SurfaceChange"/> to
/// its parent's state (<see cref="With"/>), so a button on a primary card knows to be readable on
/// primary without being told. Ported from Destash's <c>withBackgroundColor</c>.
/// </summary>
/// <param name="Surface">The background.</param>
/// <param name="Content">Text and icons on it.</param>
/// <param name="ContentFocused">Content when focused.</param>
public sealed record SurfaceState(SurfaceRoleState Surface, SurfaceRoleState Content, SurfaceRoleState ContentFocused)
{
    /// <summary>
    /// The root state: the plain surface (Material's page background), with high-legibility
    /// content on it. Destash's root was the surface container; Radiant's pages sit on the surface
    /// and put containers (cards, sheets) on it.
    /// </summary>
    public static SurfaceState Default { get; } = new(
        new SurfaceRoleState(SurfaceName.Surface, On: false, Container: false),
        new SurfaceRoleState(SurfaceName.Surface, On: true, Container: false, Legibility.High),
        new SurfaceRoleState(SurfaceName.Surface, On: true, Container: false, Legibility.High));

    /// <summary>
    /// The state inside a component that makes <paramref name="change"/>, applying its parts in
    /// Destash's order: surface family, surface legibility, surface toggles, content family,
    /// content legibility, content toggles, focused content, error, disabled.
    /// </summary>
    public SurfaceState With(SurfaceChange change)
    {
        System.ArgumentNullException.ThrowIfNull(change);
        var state = this;
        if (change.Surface is { } surface)
        {
            state = WithSurface(surface);
        }
        if (change.SurfaceLegibility is { } surfaceLegibility)
        {
            state = state with { Surface = state.Surface with { Opacity = Clamp(surfaceLegibility) } };
        }
        if (change.ToggleSurfaceOn)
        {
            state = new SurfaceState(
                state.Surface with { On = !state.Surface.On },
                state.Content with { On = !state.Content.On },
                state.ContentFocused with { On = !state.ContentFocused.On });
        }
        if (change.ToggleSurfaceContainer)
        {
            state = new SurfaceState(
                state.Surface with { Container = !state.Surface.Container },
                state.Content with { Container = !state.Content.Container },
                state.ContentFocused with { Container = !state.ContentFocused.Container });
        }
        if (change.Content is { } content)
        {
            // Content in the surface's own family is its "on" colour (on a container, the container's
            // "on" colour); in another family, the plain colour.
            var same = state.Surface.Name == content;
            var role = state.Surface with { Name = content, On = same, Container = same && state.Surface.Container, Opacity = Legibility.High };
            state = state with { Content = role, ContentFocused = role };
        }
        if (change.ContentLegibility is { } contentLegibility)
        {
            state = state with
            {
                Content = state.Content with { Opacity = Clamp(contentLegibility) },
                ContentFocused = state.ContentFocused with { Opacity = Clamp(contentLegibility) },
            };
        }
        if (change.ToggleContentOn)
        {
            state = state with
            {
                Content = state.Content with { On = !state.Content.On },
                ContentFocused = state.ContentFocused with { On = !state.ContentFocused.On },
            };
        }
        if (change.ToggleContentContainer)
        {
            state = state with
            {
                Content = state.Content with { Container = !state.Content.Container },
                ContentFocused = state.ContentFocused with { Container = !state.ContentFocused.Container },
            };
        }
        if (change.ContentFocused is { } focused)
        {
            state = state with
            {
                ContentFocused = state.Surface with
                {
                    Name = focused,
                    On = state.Surface.Name == focused ? !state.Surface.On : state.Surface.On,
                    Container = false,
                    Opacity = Legibility.High,
                },
            };
        }
        if (change.ShowError)
        {
            state = state.WithError();
        }
        if (change.ShowDisabled)
        {
            state = state.WithDisabled();
        }
        return state;
    }

    private static SurfaceState WithSurface(SurfaceName name)
    {
        var surface = new SurfaceRoleState(name, On: false, Container: false);
        var content = surface with { On = true, Opacity = Legibility.High };
        return new SurfaceState(surface, content, content);
    }

    // On the plain surface, only the content turns to error; on a coloured surface, the whole
    // surface becomes the error family.
    private SurfaceState WithError()
    {
        if (Surface.Name == SurfaceName.Surface)
        {
            var errorContent = new SurfaceRoleState(SurfaceName.Error, On: false, Container: false, Content.Opacity);
            return new SurfaceState(Surface, errorContent, errorContent);
        }
        var surface = Surface with { Name = SurfaceName.Error };
        return new SurfaceState(
            surface,
            surface with { On = !surface.On, Opacity = Content.Opacity },
            surface with { On = !surface.On, Opacity = ContentFocused.Opacity });
    }

    // Disabled content is the surface's content colour at low legibility; a coloured surface
    // becomes the plain one's "on" colour at very low legibility, as Material's disabled containers.
    private SurfaceState WithDisabled()
    {
        if (Surface.Name == SurfaceName.Surface)
        {
            var content = new SurfaceRoleState(SurfaceName.Surface, !Surface.On, Surface.Container, Legibility.Low);
            return this with { Content = content, ContentFocused = content };
        }
        var surface = new SurfaceRoleState(SurfaceName.Surface, On: true, Container: false, Legibility.VeryLow);
        var disabledContent = surface with { Opacity = Legibility.Low };
        return new SurfaceState(surface, disabledContent, disabledContent);
    }

    private static float Clamp(float opacity) => System.Math.Clamp(opacity, 0f, 1f);
}
