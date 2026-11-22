shader_type spatial;
render_mode unshaded, cull_back;
uniform sampler2D albedo_tex : source_color, filter_nearest, repeat_disable;
uniform vec4 tint : source_color = vec4(1.0);
uniform bool vertex_snap = true;
uniform bool affine_uv = true;
varying vec2 affine_coord;
varying float clip_depth;
varying float illumination;
void vertex() {
    vec4 clip = PROJECTION_MATRIX * MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
    if (vertex_snap) {
        vec2 grid = VIEWPORT_SIZE * 0.5;
        clip.xy = floor(clip.xy / clip.w * grid + 0.5) / grid * clip.w;
    }
    POSITION = clip;
    affine_coord = UV * clip.w;
    clip_depth = clip.w;
    vec3 n = normalize(MODEL_NORMAL_MATRIX * NORMAL);
    illumination = 0.62 + 0.38 * max(dot(n, normalize(vec3(-0.6, 0.9, 0.65))), 0.0);
}
void fragment() {
    vec2 coord = affine_uv ? affine_coord / clip_depth : UV;
    vec3 color = texture(albedo_tex, coord).rgb * tint.rgb * illumination;
    float dither = mod(FRAGCOORD.x + mod(FRAGCOORD.y, 2.0) * 2.0, 4.0) / 4.0 - 0.375;
    ALBEDO = clamp(floor(color * 31.0 + dither + 0.5) / 31.0, 0.0, 1.0);
}
