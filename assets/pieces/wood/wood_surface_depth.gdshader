shader_type spatial;
render_mode cull_disabled, depth_draw_opaque;
#include "res://assets/pieces/wood/wood_lighting.gdshaderinc"
uniform int finish = 0;
uniform bool felt_pad = false;
uniform float selection_amount : hint_range(0.0, 1.0) = 0.0;
uniform bool bishop_cut = false;
varying vec3 local_position;
varying vec3 world_normal;
varying vec3 world_position;
varying vec2 wear;
void vertex() {
    local_position = VERTEX;
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    world_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
    wear = COLOR.rg;
}
void fragment() {
    // Surface splats are one-sided: far-side and underside footprints must not
    // poke bright/dark dots beyond the visible silhouette in the cutout pass.
    vec3 view_direction = normalize(INV_VIEW_MATRIX[3].xyz - world_position);
    if (dot(normalize(world_normal), view_direction) <= 0.0) { discard; }
    // Match the bishop's short diagonal cut so soft splat footprints cannot
    // protrude across its lips. Keep this transform in sync with wood_shapes.gd.
    if (bishop_cut) {
        vec2 offset = local_position.xy - vec2(0.13, 0.64);
        vec2 cut = vec2(cos(0.65) * offset.x - sin(0.65) * offset.y,
                        sin(0.65) * offset.x + cos(0.65) * offset.y);
        if (abs(cut.x) < 0.0278 && abs(cut.y) < 0.1798 && abs(local_position.z) < 0.36) { discard; }
    }
    vec2 p = UV * 2.0 - 1.0;
    float r = dot(p, p);
    if (r > 0.80) { discard; }
    vec3 albedo;
    float roughness;
    float metallic;
    float specular;
    piece_finish(local_position, finish, wear.x, wear.y, felt_pad && local_position.y < 0.003, albedo, roughness, metallic, specular);
    ALBEDO = mix(albedo, vec3(0.4, 0.85, 1.0), selection_amount);
    EMISSION = vec3(0.25, 0.6, 0.8) * selection_amount * 0.6 + piece_fill(ALBEDO);
    ALBEDO *= piece_fill_keep();
    ROUGHNESS = roughness;
    METALLIC = metallic;
    SPECULAR = specular;
    // Splats are camera-agnostic quads: shade with the sampled surface normal.
    NORMAL = normalize((VIEW_MATRIX * vec4(world_normal, 0.0)).xyz);
    ALPHA = exp(-4.0 * r) * 0.98;
    ALPHA_SCISSOR_THRESHOLD = 0.09;
}
