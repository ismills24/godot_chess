shader_type spatial;
render_mode unshaded, cull_front, shadows_disabled;
// Outline pass for the cel-shaded pieces: the back faces, pushed out along their normals in
// screen space so the line stays `width` pixels wide at any distance.
uniform float width = 1.6;
uniform vec3 color : source_color = vec3(0.07, 0.035, 0.02);

void vertex() {
    vec4 clip = PROJECTION_MATRIX * MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
    vec3 view_normal = normalize((MODELVIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
    vec2 push = (PROJECTION_MATRIX * vec4(view_normal, 0.0)).xy;
    if (dot(push, push) > 0.0) {
        clip.xy += normalize(push) * width * 2.0 / VIEWPORT_SIZE * clip.w;
    }
    POSITION = clip;
}

void fragment() {
    ALBEDO = color;
}
