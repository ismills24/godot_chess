shader_type spatial;
// The board for the wood set's painted maple and walnut: pale maple and mid walnut squares
// under a soft satin varnish, a dark walnut frame with a maple line round the field.
// The squares sit a step off the pieces' own woods so both sides stand out on them.
#include "res://assets/boards/styles/common.gdshaderinc"

void fragment() {
    vec2 q = board_point();
    float seed = board_seed(q);
    vec3 albedo;
    float rough = 0.42;
    if (frame) {
        vec3 p = local_position / square_size;
        float fibre = board_fbm(vec3(p.x * 2.0 + p.z * 2.0, p.y * 30.0, p.x * 0.3));
        albedo = vec3(0.16, 0.085, 0.045) * (0.8 + fibre * 0.4);
        albedo = mix(albedo, vec3(0.72, 0.56, 0.34), frame_line(0.18, 0.06));
        albedo *= 1.0 - frame_side() * 0.25;
    } else {
        // Grain along the files, each square's veneer cut from a different part of the log.
        float grain = board_fbm(vec3(q.x * 9.0, q.y * 0.7, seed * 40.0)) * 0.7
            + piece_noise(vec3(q.x * 60.0, q.y * 2.0, seed * 9.0)) * 0.3;
        float light = board_light(q);
        vec3 maple = vec3(0.80, 0.64, 0.42) * (0.92 + seed * 0.08);
        vec3 walnut = vec3(0.40, 0.24, 0.13) * (0.9 + seed * 0.14);
        albedo = mix(walnut, maple, light) * (0.88 + grain * 0.2);
        rough += grain * 0.08;
    }
    ALBEDO = albedo;
    ROUGHNESS = rough;
    SPECULAR = 0.45;
}
