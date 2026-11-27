shader_type spatial;
render_mode cull_disabled;
#include "res://assets/pieces/wood/wood_lighting.gdshaderinc"
uniform int finish = 0;
uniform float selection_amount : hint_range(0.0, 1.0) = 0.0;
varying vec3 local_position;
void vertex() {
    local_position = VERTEX;
}
void fragment() {
    vec3 albedo;
    float roughness;
    float metallic;
    float specular;
    // The sleeve sits in the collar's shadow; read it as a slightly dirtier joint.
    piece_finish(local_position, finish, 0.0, 0.25, false, albedo, roughness, metallic, specular);
    ALBEDO = mix(albedo, vec3(0.4, 0.85, 1.0), selection_amount);
    EMISSION = vec3(0.25, 0.6, 0.8) * selection_amount * 0.6 + piece_fill(ALBEDO);
    ALBEDO *= piece_fill_keep();
    ROUGHNESS = roughness;
    METALLIC = metallic;
    SPECULAR = specular;
}
