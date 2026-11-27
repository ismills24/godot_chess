shader_type spatial;
// The board for the brass and pewter set: a plate of brushed nickel and polished copper
// squares, the brushing turned a quarter in alternate squares, in a dark gunmetal frame with
// a brass rule. Like the pieces, only partly metallic, with a soft fake surround standing in
// for reflections (the scenes have no reflection probes).
#include "res://assets/boards/styles/common.gdshaderinc"

void fragment() {
    vec2 q = board_point();
    vec3 albedo;
    float rough;
    float brushed;
    if (frame) {
        vec3 p = local_position / square_size;
        brushed = piece_noise(vec3(p.x * 2.0 + p.z * 2.0, p.y * 90.0, 0.0));
        albedo = vec3(0.17, 0.18, 0.20);
        float rule = frame_line(0.16, 0.06);
        albedo = mix(albedo, vec3(0.93, 0.72, 0.33), rule);
        rough = mix(0.4, 0.26, rule);
    } else {
        float light = board_light(q);
        vec2 cell = floor(q);
        vec2 run = mod(cell.x + cell.y, 2.0) > 0.5 ? q.yx : q;
        brushed = piece_noise(vec3(run.x * 2.0, run.y * 140.0, board_seed(q) * 9.0));
        albedo = mix(vec3(0.66, 0.36, 0.22), vec3(0.74, 0.75, 0.77), light);
        rough = mix(0.26, 0.34, light);
        float seam = 1.0 - smoothstep(0.0, max(fwidth(q.x), fwidth(q.y)) * 1.2, board_edge(q));
        albedo *= 1.0 - seam * 0.6;
    }
    vec3 world_normal = (INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz;
    float surround = smoothstep(-0.3, 0.9, world_normal.y);
    float edge = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.0);
    ALBEDO = albedo;
    EMISSION = albedo * (0.04 + surround * 0.1 + edge * 0.12);
    ROUGHNESS = rough + brushed * 0.1;
    METALLIC = 0.7;
    SPECULAR = 0.7;
}
