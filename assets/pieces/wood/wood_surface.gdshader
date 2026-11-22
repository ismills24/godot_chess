shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never;
#include "res://assets/pieces/wood/wood_lighting.gdshaderinc"
uniform bool dark_wood = false;
uniform float selection_amount : hint_range(0.0, 1.0) = 0.0;
uniform bool bishop_cut = false;
varying vec3 local_position;
varying vec3 world_normal;
varying vec3 world_position;
void vertex() {
    local_position = VERTEX;
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    world_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
}
void fragment() {
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
    ALBEDO = mix(wood_color(local_position, world_normal, world_position, INV_VIEW_MATRIX[3].xyz, dark_wood), vec3(0.4, 0.85, 1.0), selection_amount);
    ALPHA = exp(-4.0 * r) * 0.98;
}
