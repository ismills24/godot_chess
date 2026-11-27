shader_type spatial;
// Polished stone: white Carrara with grey veins against black marble with pale veins.
#include "res://assets/pieces/wood_hd/styles/common.gdshaderinc"

void fragment() {
    vec3 p = local_position;
    float n = piece_fbm(p * 2.6);
    float band = abs(sin((p.x * 2.1 + p.y * 1.3 + p.z * 0.7 + n * 3.4) * 4.2));
    float vein = 1.0 - smoothstep(0.0, 0.07, band);
    float haze = 1.0 - smoothstep(0.0, 0.35, band);
    float cloud = piece_fbm(p * 7.0 + 4.0);
    vec3 albedo;
    if (finish == 1) {
        albedo = vec3(0.045, 0.048, 0.052) * (0.85 + cloud * 0.3);
        albedo = mix(albedo, vec3(0.42, 0.44, 0.42), vein * 0.8 + haze * 0.08);
    } else {
        albedo = vec3(0.88, 0.87, 0.84) * (0.94 + cloud * 0.08);
        albedo = mix(albedo, vec3(0.45, 0.46, 0.48), vein * 0.75 + haze * 0.12);
    }
    albedo *= 1.0 - wear.y * 0.25;
    float rough = 0.14;
    if (on_felt()) { albedo = vec3(0.02, 0.06, 0.034); rough = 1.0; }
    vec3 emission = vec3(0.0);
    piece_select(albedo, emission);
    ALBEDO = albedo;
    EMISSION = emission;
    ROUGHNESS = rough;
    SPECULAR = 0.6;
}
