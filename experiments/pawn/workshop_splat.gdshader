shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never;
varying vec3 world_normal;
varying vec3 world_position;
uniform float metal_finish = 0.0;
void vertex() {
    world_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}
void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float r = dot(p, p);
    if (r > 0.8) { discard; }
    float light = 0.32 + 0.68 * max(dot(normalize(world_normal), normalize(vec3(-0.65, 0.85, 0.5))), 0.0);
    ALBEDO = COLOR.rgb * light;
    vec3 view_direction = normalize(INV_VIEW_MATRIX[3].xyz - world_position);
    vec3 half_direction = normalize(view_direction + normalize(vec3(-0.65, 0.85, 0.5)));
    float highlight = pow(max(dot(normalize(world_normal), half_direction), 0.0), 42.0);
    float rim = pow(1.0 - abs(dot(normalize(world_normal), view_direction)), 4.0);
    ALBEDO += metal_finish * (vec3(0.68, 0.76, 0.82) * highlight + vec3(0.12, 0.16, 0.20) * rim);
    ALPHA = exp(-4.0 * r) * 0.97;
}
