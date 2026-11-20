shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never;

uniform float opacity = 0.92;

void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float radius_squared = dot(p, p);
    if (radius_squared > 1.0) { discard; }
    ALBEDO = COLOR.rgb;
    ALPHA = exp(-4.0 * radius_squared) * opacity;
}
