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

@fragment
fn fs_main(input: VertexOutput) -> @location(0) vec4<f32> {
    return input.color;
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

@fragment
fn fs_main(input: VertexOutput) -> @location(0) vec4<f32> {
    return input.color;
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
    let screenPxRange = max(4.0 / max(avgDerivAtlasPx, 1e-4), 1.0);
    // Baker emits sd_stored > 0.5 for interior pixels (after scanline sign
    // correction), so standard msdfgen mapping applies directly.
    let screenPxDist = screenPxRange * (sd - 0.5);
    let alpha = clamp(screenPxDist + 0.5, 0.0, 1.0);
    return vec4<f32>(input.color.rgb, input.color.a * alpha);
}";

        public const string RoundedRectShader = @"
struct VertexInput {
    @location(0) position: vec2<f32>,
    @location(1) color: vec4<f32>,
    @location(2) borderColor: vec4<f32>,
    @location(3) localPos: vec2<f32>,
    @location(4) params: vec4<f32>,
}

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) color: vec4<f32>,
    @location(1) borderColor: vec4<f32>,
    @location(2) localPos: vec2<f32>,
    @location(3) params: vec4<f32>,
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
    output.borderColor = input.borderColor;
    output.localPos = input.localPos;
    output.params = input.params;
    return output;
}

// Signed distance to a rounded box centred at the origin. Negative inside, 0 on the edge.
fn sd_round_box(p: vec2<f32>, half_size: vec2<f32>, radius: f32) -> f32 {
    let q = abs(p) - (half_size - vec2<f32>(radius));
    return min(max(q.x, q.y), 0.0) + length(max(q, vec2<f32>(0.0))) - radius;
}

@fragment
fn fs_main(input: VertexOutput) -> @location(0) vec4<f32> {
    let half_size = input.params.xy;
    let radius = input.params.z;
    let border_width = input.params.w;

    let dist = sd_round_box(input.localPos, half_size, radius);
    // fwidth(dist) is the per-screen-pixel change in distance, so the transitions below stay one
    // pixel wide at any scale (analytic AA).
    let aa = max(fwidth(dist), 1e-4);

    // Outer coverage: 1 well inside the shape, fading to 0 across the edge at dist = 0.
    let coverage = clamp(0.5 - dist / aa, 0.0, 1.0);

    // Border band occupies dist in [-border_width, 0]; fill is deeper than -border_width.
    let border_factor = select(
        clamp(0.5 + (dist + border_width) / aa, 0.0, 1.0),
        0.0,
        border_width <= 0.0);

    let rgba = mix(input.color, input.borderColor, border_factor);
    return vec4<f32>(rgba.rgb, rgba.a * coverage);
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
    var texColor = textureSample(textureData, textureSampler, input.texCoord);
    return input.color * texColor;
}";
    }
}
