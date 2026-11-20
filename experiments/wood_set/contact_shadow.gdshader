shader_type spatial;
render_mode unshaded, depth_draw_never, cull_disabled;
void fragment() {
    float radius = length(UV * 2.0 - 1.0);
    ALBEDO = vec3(0.035, 0.02, 0.012);
    ALPHA = (1.0 - smoothstep(0.30, 1.0, radius)) * 0.33;
}
