shader_type spatial;
// The board for the boxwood and rosewood set: an inlaid board of figured sycamore and
// padauk veneers, the grain turned a quarter in every other square the way marquetry boards
// are laid, under a deep gloss. Rosewood frame with boxwood and ebony stringing.
#include "res://assets/boards/styles/common.gdshaderinc"

float rings(vec2 along_across, float seed) {
    float warp = board_fbm(vec3(along_across * vec2(0.6, 2.0), seed * 30.0)) * 0.9;
    float r = fract(along_across.y * 3.2 + warp + seed * 7.0);
    return smoothstep(0.0, 0.15, r) * smoothstep(0.75, 0.35, r);
}

void fragment() {
    vec2 q = board_point();
    float seed = board_seed(q);
    vec3 albedo;
    float rough = 0.22;
    if (frame) {
        vec3 p = local_position / square_size;
        vec2 run = abs(p.x) > abs(p.z) ? p.zx : p.xz;
        float late = rings(vec2(run.x, run.y + p.y * 2.0) * vec2(1.0, 1.4), 0.3);
        albedo = mix(vec3(0.26, 0.09, 0.045), vec3(0.11, 0.035, 0.02), late * 0.85);
        albedo = mix(albedo, vec3(0.84, 0.70, 0.46), frame_line(0.14, 0.05));
        albedo = mix(albedo, vec3(0.03, 0.02, 0.015), frame_line(0.22, 0.05));
        albedo *= 1.0 - frame_side() * 0.2;
    } else {
        float light = board_light(q);
        vec2 cell = floor(q);
        vec2 run = mod(cell.x + cell.y, 2.0) > 0.5 ? q.yx : q;
        float late = rings(run, seed);
        float fibre = piece_noise(vec3(run.x * 3.0, run.y * 80.0, seed * 5.0));
        vec3 sycamore = mix(vec3(0.88, 0.78, 0.60), vec3(0.78, 0.66, 0.48), late * 0.6);
        vec3 padauk = mix(vec3(0.52, 0.25, 0.13), vec3(0.38, 0.15, 0.08), late * 0.6);
        albedo = mix(padauk, sycamore, light) * (0.93 + fibre * 0.1);
        // A hairline where the veneers meet.
        float seam = 1.0 - smoothstep(0.0, max(fwidth(q.x), fwidth(q.y)) * 0.9, board_edge(q));
        albedo *= 1.0 - seam * 0.35;
        rough += fibre * 0.06;
    }
    ALBEDO = albedo;
    ROUGHNESS = rough;
    SPECULAR = 0.55;
}
