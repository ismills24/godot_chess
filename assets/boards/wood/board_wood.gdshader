shader_type spatial;
render_mode unshaded;
uniform vec4 wood_tint : source_color = vec4(0.6, 0.4, 0.25, 1.0);
varying vec3 world_position;
varying vec3 world_normal;
void vertex() {
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    world_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
}
void fragment() {
    float phase = world_position.x * 17.0 + sin(world_position.z * 1.6 + world_position.x * 2.0) * 1.5;
    float grain = sin(phase) * 0.012 + sin(phase * 0.43) * 0.008;
    float shade = 0.63 + 0.37 * max(dot(normalize(world_normal), normalize(vec3(-0.65, 0.85, 0.5))), 0.0);
    ALBEDO = wood_tint.rgb * (shade + grain);
}
