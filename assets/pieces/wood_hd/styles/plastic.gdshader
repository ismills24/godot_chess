shader_type spatial;
// Moulded plastic club set: cream against black, glossy, no grain.
#include "res://assets/pieces/wood_hd/styles/common.gdshaderinc"

void fragment() {
    vec3 albedo = finish == 1 ? vec3(0.035, 0.035, 0.04) : vec3(0.86, 0.81, 0.69);
    albedo *= 1.0 - wear.y * 0.2;
    float rough = 0.16;
    if (on_felt()) { albedo = vec3(0.02, 0.06, 0.034); rough = 1.0; }
    vec3 emission = vec3(0.0);
    piece_select(albedo, emission);
    ALBEDO = albedo;
    EMISSION = emission;
    ROUGHNESS = rough;
    SPECULAR = 0.65;
}
