// Shared by the piece style shaders (styles/*.gdshader). Local space is the module's own
// (pieces about 1 unit wide). finish: 0 = the light side, 1 = the dark side, 2 = gilt.
// The vertex colour carries baked wear: red = exposed edge, green = cavity.
#include "res://assets/pieces/wood/wood_lighting.gdshaderinc"
uniform int finish = 0;
uniform bool felt_pad = false;
uniform float selection_amount : hint_range(0.0, 1.0) = 0.0;
varying vec3 local_position;
varying vec2 wear;

void vertex() {
    local_position = VERTEX;
    wear = COLOR.rg;
}

float piece_fbm(vec3 p) {
    return piece_noise(p) * 0.5 + piece_noise(p * 2.03 + 11.7) * 0.25
        + piece_noise(p * 4.01 + 3.1) * 0.125 + piece_noise(p * 8.07 + 7.9) * 0.0625;
}

bool on_felt() {
    return felt_pad && local_position.y < 0.003;
}

// The selection tint every style shares, applied last.
void piece_select(inout vec3 albedo, inout vec3 emission) {
    albedo = mix(albedo, vec3(0.4, 0.85, 1.0), selection_amount);
    emission += vec3(0.25, 0.6, 0.8) * selection_amount * 0.6;
    emission += piece_fill(albedo);
    albedo *= piece_fill_keep();
}
