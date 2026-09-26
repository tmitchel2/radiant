namespace Radiant.Graphics2D.Shaders
{
    public static class ShaderLibrary
    {
        public const string FilledShapeShader = @"
struct VertexInput {
    @location(0) position: vec2<f32>,
    @location(1) color: vec4<f32>,
}

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) color: vec4<f32>,
}

struct Uniforms {
    view_projection: mat4x4<f32>,
}

@group(0) @binding(0)
var<uniform> uniforms: Uniforms;

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
    return vec4<f32>(input.color.rgb * input.color.a, input.color.a);
}";

        public const string LineShader = @"
struct VertexInput {
    @location(0) position: vec2<f32>,
    @location(1) color: vec4<f32>,
}

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) color: vec4<f32>,
}

struct Uniforms {
    view_projection: mat4x4<f32>,
}

@group(0) @binding(0)
var<uniform> uniforms: Uniforms;

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
    return vec4<f32>(input.color.rgb * input.color.a, input.color.a);
}";

        public const string MsdfTextShader = @"
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

struct Uniforms {
    view_projection: mat4x4<f32>,
}

@group(0) @binding(0)
var<uniform> uniforms: Uniforms;

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
    return vec4<f32>(input.color.rgb * alpha, alpha);
}";

        public const string SdfShapeShader = @"
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

struct Uniforms {
    view_projection: mat4x4<f32>,
}

@group(0) @binding(0)
var<uniform> uniforms: Uniforms;

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
    return select(shape, fill * shadow, is_shadow);
}";

        public const string TexturedShader = @"
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

struct Uniforms {
    view_projection: mat4x4<f32>,
}

@group(0) @binding(0)
var<uniform> uniforms: Uniforms;

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
    return texColor * tint;
}";
    }
}
