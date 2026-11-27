shader_type spatial;
// Glazed porcelain: milk-white against cobalt blue. The glaze pools darker and richer in
// recesses and thins to pale on sharp edges, under a glassy shine.
#include "res://assets/pieces/wood_hd/styles/common.gdshaderinc"

void fragment() {
    vec3 albedo;
    if (finish == 1) {
        albedo = mix(vec3(0.07, 0.14, 0.45), vec3(0.025, 0.05, 0.22), wear.y);
        albedo = mix(albedo, vec3(0.55, 0.62, 0.8), wear.x * 0.35);
    } else {
        albedo = mix(vec3(0.94, 0.94, 0.91), vec3(0.78, 0.8, 0.82), wear.y * 0.8);
    }
    float rough = 0.08;
    if (on_felt()) { albedo = vec3(0.02, 0.06, 0.034); rough = 1.0; }
    vec3 emission = vec3(0.0);
    piece_select(albedo, emission);
    ALBEDO = albedo;
    EMISSION = emission;
    ROUGHNESS = rough;
    SPECULAR = 0.75;
}
