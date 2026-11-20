shader_type spatial;
render_mode unshaded, cull_disabled;
#include "res://assets/pieces/wood/wood_lighting.gdshaderinc"
uniform bool dark_wood = false;
varying vec3 local_position;
varying vec3 world_normal;
varying vec3 world_position;
void vertex() {
    local_position = VERTEX;
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    world_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
}
void fragment() {
    ALBEDO = wood_color(local_position, world_normal, world_position, INV_VIEW_MATRIX[3].xyz, dark_wood);
}
