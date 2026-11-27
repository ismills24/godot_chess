shader_type spatial;
// Turned and varnished wood with real grain: boxwood against rosewood. The tree's axis runs
// up through the piece off to one side, so growth rings show as stripes up the body and
// arcs on the top; fine fibres run along the grain.
#include "res://assets/pieces/wood_hd/styles/common.gdshaderinc"

void fragment() {
    vec3 p = local_position;
    float warp = piece_noise(p * vec3(3.0, 0.5, 3.0)) * 0.22 + piece_noise(p * vec3(11.0, 1.5, 11.0)) * 0.05;
    float rings = fract((length(p.xz - vec2(1.6, 0.8)) + warp) * 16.0);
    float late = smoothstep(0.0, 0.12, rings) * smoothstep(0.62, 0.32, rings);
    float fibre = piece_noise(p * vec3(90.0, 5.0, 90.0));
    vec3 early = finish == 1 ? vec3(0.31, 0.13, 0.065) : vec3(0.80, 0.62, 0.37);
    vec3 dark = finish == 1 ? vec3(0.13, 0.045, 0.022) : vec3(0.58, 0.39, 0.20);
    vec3 albedo = mix(early, dark, late * 0.85) * (0.92 + fibre * 0.12);
    albedo *= 1.0 + wear.x * 0.08 - wear.y * 0.3;
    float rough = 0.32;
    if (on_felt()) { albedo = vec3(0.02, 0.06, 0.034); rough = 1.0; }
    vec3 emission = vec3(0.0);
    piece_select(albedo, emission);
    ALBEDO = albedo;
    EMISSION = emission;
    ROUGHNESS = rough + fibre * 0.08;
    SPECULAR = 0.5;
}
