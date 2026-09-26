namespace Radiant.Graphics2D.Shaders
{
    public static class ShaderLibrary
    {
        /// <summary>
        /// Shared by every shader: the per-draw uniforms (the projection, and the rounded clip in
        /// force) and the rounded clip's coverage. Each fragment shader multiplies its premultiplied
        /// output by <c>clip_coverage</c>.
        /// </summary>
        public const string Common = @"
struct Uniforms {
    view_projection: mat4x4<f32>,
    // The innermost rounded clip, in device pixels: rect = (left, top, right, bottom),
    // radii = (TopLeft, TopRight, BottomRight, BottomLeft); flags.x > 0.5 while one is in force.
    clip_rect: vec4<f32>,
    clip_radii: vec4<f32>,
    clip_flags: vec4<f32>,
}

@group(0) @binding(0)
var<uniform> uniforms: Uniforms;

// How much of the fragment at device position p is inside the rounded clip: 1 inside, 0 outside,
// with a one-pixel anti-aliased edge. Rectangular clipping is the scissor's job; this only shapes
// the corners (and does nothing when no rounded clip is in force).
fn clip_coverage(p: vec2<f32>) -> f32 {
    if (uniforms.clip_flags.x < 0.5) {
        return 1.0;
    }
    let half_size = (uniforms.clip_rect.zw - uniforms.clip_rect.xy) * 0.5;
    let local = p - (uniforms.clip_rect.xy + half_size);
    let radii = uniforms.clip_radii;
    let lr = select(vec2<f32>(radii.w, radii.z), vec2<f32>(radii.x, radii.y), local.y < 0.0);
    let r = select(lr.x, lr.y, local.x > 0.0);
    let q = abs(local) - half_size + vec2<f32>(r);
    let d = min(max(q.x, q.y), 0.0) + length(max(q, vec2<f32>(0.0))) - r;
    return clamp(0.5 - d, 0.0, 1.0);
}
";

        public const string FilledShapeShader = Common + @"
struct VertexInput {
    @location(0) position: vec2<f32>,
    @location(1) color: vec4<f32>,
}

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) color: vec4<f32>,
}


@vertex
fn vs_main(input: VertexInput) -> VertexOutput {
    var output: VertexOutput;
    output.position = uniforms.view_projection * vec4<f32>(input.position, 0.0, 1.0);
    output.color = input.color;
    return output;
}

// Vertex colours are straight alpha; the pipeline blends premultiplied.
@fragment
fn fs_main(input: VertexOutput) -> @location(0) vec4<f32> {
    return vec4<f32>(input.color.rgb * input.color.a, input.color.a) * clip_coverage(input.position.xy);
}";

        public const string LineShader = Common + @"
struct VertexInput {
    @location(0) position: vec2<f32>,
    @location(1) color: vec4<f32>,
}

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) color: vec4<f32>,
}


@vertex
fn vs_main(input: VertexInput) -> VertexOutput {
    var output: VertexOutput;
    output.position = uniforms.view_projection * vec4<f32>(input.position, 0.0, 1.0);
    output.color = input.color;
    return output;
}

// Vertex colours are straight alpha; the pipeline blends premultiplied.
@fragment
fn fs_main(input: VertexOutput) -> @location(0) vec4<f32> {
    return vec4<f32>(input.color.rgb * input.color.a, input.color.a) * clip_coverage(input.position.xy);
}";

        public const string MsdfTextShader = Common + @"
struct VertexInput {
    @location(0) position: vec2<f32>,
    @location(1) color: vec4<f32>,
    @location(2) texCoord: vec2<f32>,
}

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) color: vec4<f32>,
    @location(1) texCoord: vec2<f32>,
}


@group(1) @binding(0)
var atlasSampler: sampler;

@group(1) @binding(1)
var atlasTexture: texture_2d<f32>;

// x = the distance range the atlas was baked with, in atlas pixels (MsdfFont.DistanceRangePx).
// Per font, because atlases are baked with different ranges.
@group(1) @binding(2)
var<uniform> atlasParams: vec4<f32>;

@vertex
fn vs_main(input: VertexInput) -> VertexOutput {
    var output: VertexOutput;
    output.position = uniforms.view_projection * vec4<f32>(input.position, 0.0, 1.0);
    output.color = input.color;
    output.texCoord = input.texCoord;
    return output;
}

fn median3(a: f32, b: f32, c: f32) -> f32 {
    return max(min(a, b), min(max(a, b), c));
}

@fragment
fn fs_main(input: VertexOutput) -> @location(0) vec4<f32> {
    let sample = textureSample(atlasTexture, atlasSampler, input.texCoord);
    let sd = median3(sample.r, sample.g, sample.b);
    // Convert UV derivative to atlas-pixel derivative, then to screen-pixel
    // distance per distance-field unit. Doing this per fragment makes the
    // edge AA stay one pixel wide at any scale.
    let atlasDims = vec2<f32>(textureDimensions(atlasTexture, 0));
    let derivAtlasPx = fwidth(input.texCoord) * atlasDims;
    let avgDerivAtlasPx = 0.5 * (derivAtlasPx.x + derivAtlasPx.y);
    let screenPxRange = max(atlasParams.x / max(avgDerivAtlasPx, 1e-4), 1.0);
    // Baker emits sd_stored > 0.5 for interior pixels (after scanline sign
    // correction), so standard msdfgen mapping applies directly.
    let screenPxDist = screenPxRange * (sd - 0.5);
    let coverage = clamp(screenPxDist + 0.5, 0.0, 1.0);
    let alpha = input.color.a * coverage;
    return vec4<f32>(input.color.rgb * alpha, alpha) * clip_coverage(input.position.xy);
}";

        public const string SdfShapeShader = Common + @"
struct VertexInput {
    @location(0) position: vec2<f32>,
    @location(1) localPos: vec2<f32>,
    @location(2) color: vec4<f32>,
    @location(3) borderColor: vec4<f32>,
    @location(4) misc: vec4<f32>,
    @location(5) params: vec4<f32>,
    @location(6) color1: vec4<f32>,
    @location(7) color2: vec4<f32>,
    @location(8) color3: vec4<f32>,
    @location(9) stopOffsets: vec4<f32>,
    @location(10) gradientGeometry: vec4<f32>,
    @location(11) gradientInfo: vec4<f32>,
}

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) localPos: vec2<f32>,
    @location(1) color: vec4<f32>,
    @location(2) borderColor: vec4<f32>,
    @location(3) misc: vec4<f32>,
    @location(4) params: vec4<f32>,
    @location(5) color1: vec4<f32>,
    @location(6) color2: vec4<f32>,
    @location(7) color3: vec4<f32>,
    @location(8) stopOffsets: vec4<f32>,
    @location(9) gradientGeometry: vec4<f32>,
    @location(10) gradientInfo: vec4<f32>,
}


@vertex
fn vs_main(input: VertexInput) -> VertexOutput {
    var output: VertexOutput;
    output.position = uniforms.view_projection * vec4<f32>(input.position, 0.0, 1.0);
    output.localPos = input.localPos;
    output.color = input.color;
    output.borderColor = input.borderColor;
    output.misc = input.misc;
    output.params = input.params;
    output.color1 = input.color1;
    output.color2 = input.color2;
    output.color3 = input.color3;
    output.stopOffsets = input.stopOffsets;
    output.gradientGeometry = input.gradientGeometry;
    output.gradientInfo = input.gradientInfo;
    return output;
}

// ---- Gradients -----------------------------------------------------------------------------------
// Stops are straight-alpha linear-light colors. They are premultiplied, converted to the
// interpolation space (0 sRGB, 1 linear, 2 OKLab), blended there, and converted back.

fn srgb_encode(c: vec3<f32>) -> vec3<f32> {
    let lo = c * 12.92;
    let hi = 1.055 * pow(max(c, vec3<f32>(0.0)), vec3<f32>(1.0 / 2.4)) - 0.055;
    return select(hi, lo, c <= vec3<f32>(0.0031308));
}

fn srgb_decode(c: vec3<f32>) -> vec3<f32> {
    let lo = c / 12.92;
    let hi = pow(max((c + 0.055) / 1.055, vec3<f32>(0.0)), vec3<f32>(2.4));
    return select(hi, lo, c <= vec3<f32>(0.04045));
}

fn cbrt3(v: vec3<f32>) -> vec3<f32> {
    return sign(v) * pow(abs(v), vec3<f32>(1.0 / 3.0));
}

// Björn Ottosson's OKLab, from and to linear sRGB.
fn linear_to_oklab(c: vec3<f32>) -> vec3<f32> {
    let lms = vec3<f32>(
        0.4122214708 * c.r + 0.5363325363 * c.g + 0.0514459929 * c.b,
        0.2119034982 * c.r + 0.6806995451 * c.g + 0.1073969566 * c.b,
        0.0883024619 * c.r + 0.2817188376 * c.g + 0.6299787005 * c.b);
    let l = cbrt3(lms);
    return vec3<f32>(
        0.2104542553 * l.x + 0.7936177850 * l.y - 0.0040720468 * l.z,
        1.9779984951 * l.x - 2.4285922050 * l.y + 0.4505937099 * l.z,
        0.0259040371 * l.x + 0.7827717662 * l.y - 0.8086757660 * l.z);
}

fn oklab_to_linear(c: vec3<f32>) -> vec3<f32> {
    let l = vec3<f32>(
        c.x + 0.3963377774 * c.y + 0.2158037573 * c.z,
        c.x - 0.1055613458 * c.y - 0.0638541728 * c.z,
        c.x - 0.0894841775 * c.y - 1.2914855480 * c.z);
    let lms = l * l * l;
    return vec3<f32>(
        4.0767416621 * lms.x - 3.3077115913 * lms.y + 0.2309699292 * lms.z,
        -1.2684380046 * lms.x + 2.6097574011 * lms.y - 0.3413193965 * lms.z,
        -0.0041960863 * lms.x - 0.7034186147 * lms.y + 1.7076147010 * lms.z);
}

// A straight-alpha linear color, premultiplied in the interpolation space.
fn to_space(c: vec4<f32>, space: f32) -> vec4<f32> {
    var rgb = c.rgb;
    if (space < 0.5) {
        rgb = srgb_encode(rgb);
    } else if (space > 1.5) {
        rgb = linear_to_oklab(rgb);
    }
    return vec4<f32>(rgb * c.a, c.a);
}

// Back from the interpolation space to a straight-alpha linear color.
fn from_space(c: vec4<f32>, space: f32) -> vec4<f32> {
    if (c.a <= 0.0) {
        return vec4<f32>(0.0);
    }
    var rgb = c.rgb / c.a;
    if (space < 0.5) {
        rgb = srgb_decode(rgb);
    } else if (space > 1.5) {
        rgb = oklab_to_linear(rgb);
    }
    return vec4<f32>(clamp(rgb, vec3<f32>(0.0), vec3<f32>(1.0)), c.a);
}

// The gradient's straight-alpha linear color at a point in the shape's local frame.
fn gradient_color(input: VertexOutput) -> vec4<f32> {
    let geometry = input.gradientGeometry;
    let space = input.gradientInfo.z;
    var t: f32;
    if (input.gradientInfo.x < 1.5) {
        let axis = geometry.zw - geometry.xy;
        t = dot(input.localPos - geometry.xy, axis) / max(dot(axis, axis), 1e-6);
    } else {
        t = length(input.localPos - geometry.xy) / max(geometry.z, 1e-6);
    }

    let count = i32(input.gradientInfo.y + 0.5);
    var colors = array<vec4<f32>, 4>(input.color, input.color1, input.color2, input.color3);
    let offsets = input.stopOffsets;
    var result = colors[0];
    if (t >= offsets[count - 1]) {
        result = colors[count - 1];
    } else if (t > offsets[0]) {
        for (var i = 1; i < 4; i = i + 1) {
            if (i < count && t <= offsets[i]) {
                let span = max(offsets[i] - offsets[i - 1], 1e-6);
                let f = clamp((t - offsets[i - 1]) / span, 0.0, 1.0);
                let a = to_space(colors[i - 1], space);
                let b = to_space(colors[i], space);
                return from_space(mix(a, b, f), space);
            }
        }
    }
    return result;
}

// Signed distance to a rounded box (per-corner radii) centred at the origin. Screen space: y grows
// downward, so p.y < 0 is the top half. radii = (TopLeft, TopRight, BottomRight, BottomLeft).
fn sd_round_box(p: vec2<f32>, half_size: vec2<f32>, radii: vec4<f32>) -> f32 {
    let top_lr = vec2<f32>(radii.x, radii.y);  // (left = TL, right = TR)
    let bot_lr = vec2<f32>(radii.w, radii.z);  // (left = BL, right = BR)
    let lr = select(bot_lr, top_lr, p.y < 0.0);
    let r = select(lr.x, lr.y, p.x > 0.0);
    let q = abs(p) - half_size + vec2<f32>(r);
    return min(max(q.x, q.y), 0.0) + length(max(q, vec2<f32>(0.0))) - r;
}

// Signed distance to a disc (inner_radius = 0) or annulus/ring. Negative inside, 0 on the edge.
fn sd_annulus(p: vec2<f32>, outer_radius: f32, inner_radius: f32) -> f32 {
    let d = length(p);
    let d_outer = d - outer_radius;
    if (inner_radius > 0.0) {
        return max(d_outer, inner_radius - d);
    }
    return d_outer;
}

// ---- Soft shadows -------------------------------------------------------------------------------
// A Gaussian-blurred rounded rectangle, analytically: the blur is separable, so it is integrated in
// closed form (via erf) along x, and sampled four times along y, where the Gaussian is narrow enough
// that four samples suffice. The technique is Evan Wallace's 'Fast Rounded Rectangle Shadows' (2020).

fn gaussian(x: f32, sigma: f32) -> f32 {
    return exp(-(x * x) / (2.0 * sigma * sigma)) / (2.5066282746 * sigma);
}

// An approximation of the error function, good to about 5e-4.
fn erf2(v: vec2<f32>) -> vec2<f32> {
    let s = sign(v);
    let a = abs(v);
    var x = 1.0 + (0.278393 + (0.230389 + 0.078108 * (a * a)) * a) * a;
    x = x * x;
    return s - s / (x * x);
}

// The blurred coverage along x of the box's horizontal slice at height y.
fn shadow_x(x: f32, y: f32, sigma: f32, corner: f32, half_size: vec2<f32>) -> f32 {
    let delta = min(half_size.y - corner - abs(y), 0.0);
    let curved = half_size.x - corner + sqrt(max(0.0, corner * corner - delta * delta));
    let integral = 0.5 + 0.5 * erf2((x + vec2<f32>(-curved, curved)) * (0.7071067812 / sigma));
    return integral.y - integral.x;
}

// Coverage, 0 to 1, of a rounded box centred at the origin, blurred with standard deviation sigma.
// radii = (TopLeft, TopRight, BottomRight, BottomLeft); the one for the point's quadrant is used.
fn shadow_mask(p: vec2<f32>, half_size: vec2<f32>, radii: vec4<f32>, sigma: f32) -> f32 {
    let top_lr = vec2<f32>(radii.x, radii.y);
    let bot_lr = vec2<f32>(radii.w, radii.z);
    let lr = select(bot_lr, top_lr, p.y < 0.0);
    let corner = min(select(lr.x, lr.y, p.x > 0.0), min(half_size.x, half_size.y));

    // The integrand is only non-zero within 3 sigma, and within the box.
    let low = p.y - half_size.y;
    let high = p.y + half_size.y;
    let start = clamp(-3.0 * sigma, low, high);
    let end = clamp(3.0 * sigma, low, high);
    let step = (end - start) / 4.0;
    var y = start + step * 0.5;
    var value = 0.0;
    for (var i = 0; i < 4; i = i + 1) {
        value = value + shadow_x(p.x, p.y - y, sigma, corner, half_size) * gaussian(y, sigma) * step;
        y = y + step;
    }
    return value;
}

@fragment
fn fs_main(input: VertexOutput) -> @location(0) vec4<f32> {
    let half_size = input.misc.xy;
    let border_width = input.misc.z;
    let shape_kind = input.misc.w;
    let is_shadow = shape_kind > 1.5;

    // Every branch only computes values: fwidth below must run in uniform control flow, so no
    // shape may return early.
    var dist: f32 = 0.0;
    var shadow: f32 = 0.0;
    if (shape_kind < 0.5) {
        dist = sd_round_box(input.localPos, half_size, input.params);
    } else if (shape_kind < 1.5) {
        dist = sd_annulus(input.localPos, input.params.x, input.params.y);
    } else {
        // For shadows, misc.z carries sigma rather than a border width.
        shadow = shadow_mask(input.localPos, half_size, input.params, max(border_width, 1e-3));
    }

    // fwidth(dist) is the per-screen-pixel change in distance, so the transitions below stay one
    // pixel wide at any scale (analytic AA — relies on the unit-gradient property of a true SDF).
    let aa = max(fwidth(dist), 1e-4);

    // Outer coverage: 1 well inside the shape, fading to 0 across the edge at dist = 0.
    let coverage = clamp(0.5 - dist / aa, 0.0, 1.0);

    // Border band occupies dist in [-border_width, 0]; fill is deeper than -border_width.
    let border_factor = select(
        clamp(0.5 + (dist + border_width) / aa, 0.0, 1.0),
        0.0,
        border_width <= 0.0);

    // Mix in premultiplied space: a transparent fill contributes nothing, so a border-only stroke's
    // inner edge fades to clear rather than through the fill's (meaningless) RGB towards black.
    let fill_color = select(input.color, gradient_color(input), input.gradientInfo.x > 0.5);
    let fill = vec4<f32>(fill_color.rgb * fill_color.a, fill_color.a);
    let border = vec4<f32>(input.borderColor.rgb * input.borderColor.a, input.borderColor.a);
    let shape = mix(fill, border, border_factor) * coverage;
    return select(shape, fill * shadow, is_shadow) * clip_coverage(input.position.xy);
}";

        // Text drawn from a coverage atlas (GlyphAtlas): one byte of coverage per texel, tinted by
        // the vertex colour.
        public const string CoverageTextShader = Common + @"
struct VertexInput {
    @location(0) position: vec2<f32>,
    @location(1) color: vec4<f32>,
    @location(2) texCoord: vec2<f32>,
}

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) color: vec4<f32>,
    @location(1) texCoord: vec2<f32>,
}

@group(1) @binding(0)
var atlasSampler: sampler;

@group(1) @binding(1)
var atlasTexture: texture_2d<f32>;

// x = the gamma edge coverage is corrected for (Renderer2D.TextGamma; 1 leaves it as is).
@group(1) @binding(2)
var<uniform> atlasParams: vec4<f32>;

@vertex
fn vs_main(input: VertexInput) -> VertexOutput {
    var output: VertexOutput;
    output.position = uniforms.view_projection * vec4<f32>(input.position, 0.0, 1.0);
    output.color = input.color;
    output.texCoord = input.texCoord;
    return output;
}

// Edge pixels blended in linear light make dark text on a light ground look lighter and thinner,
// and light text on a dark ground heavier, than type is designed to look: it is drawn and hinted
// on screens that blend in gamma space. So coverage is corrected as if blending in a gamma-g space
// against the contrasting ground: dark text (on white) takes 1 - (1 - a)^g, light text (on black)
// a^g, and colours between mix the two by luminance.
fn correct_coverage(a: f32, color: vec3<f32>, gamma: f32) -> f32 {
    let luma = dot(color, vec3<f32>(0.2126, 0.7152, 0.0722));
    let dark = 1.0 - pow(max(1.0 - a, 0.0), gamma);
    let light = pow(max(a, 0.0), gamma);
    return mix(dark, light, luma);
}

@fragment
fn fs_main(input: VertexOutput) -> @location(0) vec4<f32> {
    let coverage = textureSample(atlasTexture, atlasSampler, input.texCoord).r;
    let alpha = correct_coverage(coverage, input.color.rgb, atlasParams.x) * input.color.a;
    return vec4<f32>(input.color.rgb * alpha, alpha) * clip_coverage(input.position.xy);
}";

        // Text drawn from its outlines with Slug (Renderer2DSlugText.cs). The coverage calculation
        // follows Eric Lengyel's reference pixel shader (github.com/EricLengyel/Slug, MIT, Copyright
        // 2017 Eric Lengyel) and is mirrored step for step by Radiant.Text.Slug.SlugCoverage.
        public const string SlugTextShader = Common + @"
struct VertexInput {
    @location(0) position: vec2<f32>,
    @location(1) em: vec2<f32>,
    @location(2) color: vec4<f32>,
    @location(3) glyph: vec2<u32>,
}

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) em: vec2<f32>,
    @location(1) color: vec4<f32>,
    // (first band word, first curve texel), the same for the whole glyph.
    @location(2) @interpolate(flat) glyph: vec2<u32>,
}

// Curve texels: a curve at i has p1, p2 in texel i and p3 in texel i + 1 (see SlugGlyph).
@group(1) @binding(0)
var<storage, read> slugCurves: array<vec4<f32>>;

// Band words: per glyph, a header, band headers and curve lists (see SlugGlyph).
@group(1) @binding(1)
var<storage, read> slugBands: array<u32>;

// x = the gamma edge coverage is corrected for (Renderer2D.TextGamma), as coverage text does.
@group(1) @binding(2)
var<uniform> slugParams: vec4<f32>;

@vertex
fn vs_main(input: VertexInput) -> VertexOutput {
    var output: VertexOutput;
    output.position = uniforms.view_projection * vec4<f32>(input.position, 0.0, 1.0);
    output.em = input.em;
    output.color = input.color;
    output.glyph = input.glyph;
    return output;
}

// Which roots of a sample-relative curve count, from the sign bits of its control points across
// the ray: bit 0 the first (adds), bit 8 the second (subtracts). The paper's lookup table, 0x2E74.
fn slug_root_code(y1: f32, y2: f32, y3: f32) -> u32 {
    let i1 = bitcast<u32>(y1) >> 31u;
    let i2 = bitcast<u32>(y2) >> 30u;
    let i3 = bitcast<u32>(y3) >> 29u;
    var shift = (i2 & 2u) | (i1 & ~2u);
    shift = (i3 & 4u) | (shift & ~4u);
    return (0x2E74u >> shift) & 0x0101u;
}

// Where a sample-relative curve crosses y = 0 (the x of both roots). y(t) = a t^2 - 2 b t + p1.y;
// a negative discriminant clamps to a double root, and a nearly linear curve solves the line.
// Divisors are guarded so no infinity is made (Metal compiles with fast math).
fn slug_solve_horizontal(p12: vec4<f32>, p3: vec2<f32>) -> vec2<f32> {
    let a = p12.xy - p12.zw * 2.0 + p3;
    let b = p12.xy - p12.zw;
    let linear = abs(a.y) < 1.0 / 65536.0;
    let ra = 1.0 / select(a.y, 1.0, linear);
    let rb = 0.5 / select(b.y, 1.0, b.y == 0.0);
    let d = sqrt(max(b.y * b.y - a.y * p12.y, 0.0));
    let t1 = select((b.y - d) * ra, p12.y * rb, linear);
    let t2 = select((b.y + d) * ra, p12.y * rb, linear);
    return vec2<f32>((a.x * t1 - b.x * 2.0) * t1 + p12.x, (a.x * t2 - b.x * 2.0) * t2 + p12.x);
}

// The same with x and y swapped: the y where the curve crosses x = 0.
fn slug_solve_vertical(p12: vec4<f32>, p3: vec2<f32>) -> vec2<f32> {
    let a = p12.xy - p12.zw * 2.0 + p3;
    let b = p12.xy - p12.zw;
    let linear = abs(a.x) < 1.0 / 65536.0;
    let ra = 1.0 / select(a.x, 1.0, linear);
    let rb = 0.5 / select(b.x, 1.0, b.x == 0.0);
    let d = sqrt(max(b.x * b.x - a.x * p12.x, 0.0));
    let t1 = select((b.x - d) * ra, p12.x * rb, linear);
    let t2 = select((b.x + d) * ra, p12.x * rb, linear);
    return vec2<f32>((a.y * t1 - b.y * 2.0) * t1 + p12.y, (a.y * t2 - b.y * 2.0) * t2 + p12.y);
}

// How much of the pixel centred on em (em units, y up) the glyph covers. One ray right through
// the pixel's horizontal band and one up through its vertical band each sum, over the curves they
// cross, the fraction of the pixel before the crossing: a winding number blurred over one pixel.
fn slug_coverage(em: vec2<f32>, pixels_per_em: vec2<f32>, band_base: u32, curve_base: u32) -> f32 {
    let h_count = slugBands[band_base];
    let v_count = slugBands[band_base + 1u];
    let scale = vec2<f32>(bitcast<f32>(slugBands[band_base + 2u]), bitcast<f32>(slugBands[band_base + 3u]));
    let offset = vec2<f32>(bitcast<f32>(slugBands[band_base + 4u]), bitcast<f32>(slugBands[band_base + 5u]));
    let band = clamp(vec2<i32>(floor(em * scale + offset)), vec2<i32>(0), vec2<i32>(i32(v_count) - 1, i32(h_count) - 1));

    var xcov = 0.0;
    var xwgt = 0.0;
    let h_header = band_base + 6u + 2u * u32(band.y);
    let h_list = band_base + slugBands[h_header + 1u];
    for (var i = 0u; i < slugBands[h_header]; i++) {
        let at = curve_base + slugBands[h_list + i];
        let p12 = slugCurves[at] - vec4<f32>(em, em);
        let p3 = slugCurves[at + 1u].xy - em;
        // Sorted by greatest x: once a curve is wholly half a pixel behind, all the rest are.
        if (max(max(p12.x, p12.z), p3.x) * pixels_per_em.x < -0.5) {
            break;
        }
        let code = slug_root_code(p12.y, p12.w, p3.y);
        if (code != 0u) {
            let r = slug_solve_horizontal(p12, p3) * pixels_per_em.x;
            if ((code & 1u) != 0u) {
                xcov += saturate(r.x + 0.5);
                xwgt = max(xwgt, saturate(1.0 - abs(r.x) * 2.0));
            }
            if (code > 1u) {
                xcov -= saturate(r.y + 0.5);
                xwgt = max(xwgt, saturate(1.0 - abs(r.y) * 2.0));
            }
        }
    }

    var ycov = 0.0;
    var ywgt = 0.0;
    let v_header = band_base + 6u + 2u * h_count + 2u * u32(band.x);
    let v_list = band_base + slugBands[v_header + 1u];
    for (var i = 0u; i < slugBands[v_header]; i++) {
        let at = curve_base + slugBands[v_list + i];
        let p12 = slugCurves[at] - vec4<f32>(em, em);
        let p3 = slugCurves[at + 1u].xy - em;
        if (max(max(p12.y, p12.w), p3.y) * pixels_per_em.y < -0.5) {
            break;
        }
        let code = slug_root_code(p12.x, p12.z, p3.x);
        if (code != 0u) {
            let r = slug_solve_vertical(p12, p3) * pixels_per_em.y;
            if ((code & 1u) != 0u) {
                ycov -= saturate(r.x + 0.5);
                ywgt = max(ywgt, saturate(1.0 - abs(r.x) * 2.0));
            }
            if (code > 1u) {
                ycov += saturate(r.y + 0.5);
                ywgt = max(ywgt, saturate(1.0 - abs(r.y) * 2.0));
            }
        }
    }

    // Averaged by how near each ray's crossings came to the centre, never below the lesser of the
    // two; magnitudes so either winding direction fills, clamped for the non-zero rule.
    let weighted = abs(xcov * xwgt + ycov * ywgt) / max(xwgt + ywgt, 1.0 / 65536.0);
    return saturate(max(weighted, min(abs(xcov), abs(ycov))));
}

// As the coverage text shader: edges corrected as if blended in a gamma-g space against the
// contrasting ground, so Slug text has the weight coverage text has.
fn slug_correct_coverage(a: f32, color: vec3<f32>, gamma: f32) -> f32 {
    let luma = dot(color, vec3<f32>(0.2126, 0.7152, 0.0722));
    let dark = 1.0 - pow(max(1.0 - a, 0.0), gamma);
    let light = pow(max(a, 0.0), gamma);
    return mix(dark, light, luma);
}

@fragment
fn fs_main(input: VertexOutput) -> @location(0) vec4<f32> {
    // Pixels per em along each axis, from the em coordinate's screen derivatives. Taken first,
    // in uniform control flow, as derivatives must be.
    let pixels_per_em = 1.0 / max(fwidth(input.em), vec2<f32>(1.0e-7));
    let coverage = slug_coverage(input.em, pixels_per_em, input.glyph.x, input.glyph.y);
    let alpha = slug_correct_coverage(coverage, input.color.rgb, slugParams.x) * input.color.a;
    return vec4<f32>(input.color.rgb * alpha, alpha) * clip_coverage(input.position.xy);
}";

        public const string TexturedShader = Common + @"
struct VertexInput {
    @location(0) position: vec2<f32>,
    @location(1) color: vec4<f32>,
    @location(2) texCoord: vec2<f32>,
}

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) color: vec4<f32>,
    @location(1) texCoord: vec2<f32>,
}


@group(1) @binding(0)
var textureSampler: sampler;

@group(1) @binding(1)
var textureData: texture_2d<f32>;

@vertex
fn vs_main(input: VertexInput) -> VertexOutput {
    var output: VertexOutput;
    output.position = uniforms.view_projection * vec4<f32>(input.position, 0.0, 1.0);
    output.color = input.color;
    output.texCoord = input.texCoord;
    return output;
}

@fragment
fn fs_main(input: VertexOutput) -> @location(0) vec4<f32> {
    // Texels are premultiplied (Texture2D's contract); the tint is a straight-alpha colour.
    let texColor = textureSample(textureData, textureSampler, input.texCoord);
    let tint = vec4<f32>(input.color.rgb * input.color.a, input.color.a);
    return texColor * tint * clip_coverage(input.position.xy);
}";
    }
}
