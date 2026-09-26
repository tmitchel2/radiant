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
}

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) localPos: vec2<f32>,
    @location(1) color: vec4<f32>,
    @location(2) borderColor: vec4<f32>,
    @location(3) misc: vec4<f32>,
    @location(4) params: vec4<f32>,
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
    return output;
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

@fragment
fn fs_main(input: VertexOutput) -> @location(0) vec4<f32> {
    let half_size = input.misc.xy;
    let border_width = input.misc.z;
    let shape_kind = input.misc.w;

    var dist: f32;
    if (shape_kind < 0.5) {
        dist = sd_round_box(input.localPos, half_size, input.params);
    } else {
        dist = sd_annulus(input.localPos, input.params.x, input.params.y);
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
    let fill = vec4<f32>(input.color.rgb * input.color.a, input.color.a);
    let border = vec4<f32>(input.borderColor.rgb * input.borderColor.a, input.borderColor.a);
    return mix(fill, border, border_factor) * coverage;
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
