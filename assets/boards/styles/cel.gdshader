shader_type spatial;
// The board for the cel-shaded set: flat sand and clay squares lit in the same hard bands as
// the pieces, inked round every square and round the field.
#include "res://assets/boards/styles/common.gdshaderinc"
uniform vec3 bands = vec3(0.9, 0.5, 0.2);
uniform vec3 ink : source_color = vec3(0.07, 0.035, 0.02);

void fragment() {
    vec2 q = board_point();
    vec3 albedo;
    if (frame) {
        albedo = vec3(0.30, 0.17, 0.09);
        albedo = mix(albedo, ink, frame_line(0.0, 0.06));
        albedo = mix(albedo, vec3(0.62, 0.42, 0.24), frame_line(0.2, 0.05));
        albedo *= frame_side() > 0.5 ? 0.7 : 1.0;
    } else {
        albedo = mix(vec3(0.50, 0.31, 0.18), vec3(0.93, 0.80, 0.56), step(0.5, board_light(q)));
        // An ink line along every square edge, about a pixel and a half wide on screen.
        float pixel = max(fwidth(q.x), fwidth(q.y));
        float line = 1.0 - smoothstep(pixel * 0.5, pixel * 1.2, board_edge(q));
        albedo = mix(albedo, ink, line * 0.85);
    }
    ALBEDO = albedo;
    ROUGHNESS = 1.0;
    SPECULAR = 0.0;
}

void light() {
    float facing = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
    float band = mix(bands.z, bands.y, smoothstep(0.08, 0.14, facing));
    band = mix(band, bands.x, smoothstep(0.5, 0.56, facing));
    DIFFUSE_LIGHT += LIGHT_COLOR / PI * band * ATTENUATION;
}
