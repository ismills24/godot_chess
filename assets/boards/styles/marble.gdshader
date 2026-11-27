shader_type spatial;
// The board for the marble set: honed cream Botticino and dark verde marble, each square cut
// from its own part of the slab, set in a polished black marble frame with a gilt line. The
// squares keep off pure white and black so the Carrara and black pieces read against them.
#include "res://assets/boards/styles/common.gdshaderinc"

float veins(vec3 p, out float haze) {
    float n = board_fbm(p * 0.9);
    float band = abs(sin((p.x * 0.8 + p.z * 0.5 + p.y * 0.3 + n * 3.4) * 2.6));
    haze = 1.0 - smoothstep(0.0, 0.35, band);
    return 1.0 - smoothstep(0.0, 0.06, band);
}

void fragment() {
    vec2 q = board_point();
    float seed = board_seed(q);
    vec3 albedo;
    float haze;
    float rough = 0.18;
    if (frame) {
        vec3 p = local_position / square_size * 1.4;
        float vein = veins(p + 20.0, haze);
        albedo = vec3(0.035, 0.037, 0.04) * (0.85 + board_fbm(p * 3.0) * 0.3);
        albedo = mix(albedo, vec3(0.5, 0.5, 0.48), vein * 0.7 + haze * 0.06);
        albedo = mix(albedo, vec3(0.62, 0.52, 0.30), frame_line(0.16, 0.035));
        rough = 0.1;
    } else {
        float light = board_light(q);
        vec3 p = vec3(q.x, seed * 13.0, q.y) + seed * 31.0;
        float vein = veins(p, haze);
        float cloud = board_fbm(vec3(q * 3.0, seed * 7.0));
        vec3 cream = vec3(0.80, 0.74, 0.62) * (0.93 + cloud * 0.1);
        cream = mix(cream, vec3(0.58, 0.50, 0.40), vein * 0.6 + haze * 0.1);
        vec3 verde = vec3(0.10, 0.25, 0.19) * (0.8 + cloud * 0.4);
        verde = mix(verde, vec3(0.62, 0.72, 0.62), vein * 0.7 + haze * 0.08);
        albedo = mix(verde, cream, light);
        float seam = 1.0 - smoothstep(0.0, max(fwidth(q.x), fwidth(q.y)), board_edge(q));
        albedo *= 1.0 - seam * 0.3;
    }
    ALBEDO = albedo;
    ROUGHNESS = rough;
    SPECULAR = 0.6;
}
