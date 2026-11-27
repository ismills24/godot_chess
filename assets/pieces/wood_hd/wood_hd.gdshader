shader_type spatial;
// The wood set's finish (wood_lighting.gdshaderinc) on solid meshes. Wear is baked in the
// vertex colour: red = exposed edge, green = cavity.
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
void fragment() {
    vec3 albedo;
    float roughness;
    float metallic;
    float specular;
    piece_finish(local_position, finish, wear.x, wear.y, felt_pad && local_position.y < 0.003, albedo, roughness, metallic, specular);
    ALBEDO = mix(albedo, vec3(0.4, 0.85, 1.0), selection_amount);
    EMISSION = vec3(0.25, 0.6, 0.8) * selection_amount * 0.6 + piece_fill(ALBEDO);
    ALBEDO *= piece_fill_keep();
    ROUGHNESS = roughness;
    METALLIC = metallic;
    SPECULAR = specular;
}
