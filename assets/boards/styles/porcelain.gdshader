shader_type spatial;
// The board for the porcelain set: glazed tiles in a warm buff and a soft delft blue, between
// the milk-white and cobalt pieces, each tile's glaze pooling a little toward its edges, set
// in a cobalt frame painted with white lines, all under a glassy shine.
#include "res://assets/boards/styles/common.gdshaderinc"

void fragment() {
    vec2 q = board_point();
    float seed = board_seed(q);
    vec3 albedo;
    if (frame) {
        albedo = vec3(0.05, 0.10, 0.36);
        albedo = mix(albedo, vec3(0.92, 0.92, 0.88), frame_line(0.15, 0.05));
        albedo = mix(albedo, vec3(0.92, 0.92, 0.88), frame_line(0.24, 0.02));
        albedo *= 1.0 - frame_side() * 0.3;
    } else {
        float light = board_light(q);
        float edge = board_edge(q);
        float pool = 1.0 - smoothstep(0.0, 0.12, edge);
        float mottle = board_fbm(vec3(q * 4.0, seed * 11.0));
        vec3 ivory = vec3(0.70, 0.62, 0.49) * (0.96 + mottle * 0.06);
        ivory = mix(ivory, vec3(0.58, 0.50, 0.39), pool * 0.5);
        vec3 delft = mix(vec3(0.28, 0.45, 0.80), vec3(0.17, 0.29, 0.64), mottle * 0.5 + pool * 0.4);
        albedo = mix(delft, ivory, light);
        // The grout line between tiles.
        float grout = 1.0 - smoothstep(0.0, max(fwidth(q.x), fwidth(q.y)) * 1.2, edge);
        albedo = mix(albedo, vec3(0.52, 0.52, 0.5), grout * 0.7);
    }
    ALBEDO = albedo;
    ROUGHNESS = 0.08;
    SPECULAR = 0.7;
}
