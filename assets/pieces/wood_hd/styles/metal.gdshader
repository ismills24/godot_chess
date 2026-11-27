shader_type spatial;
// Cast metal: polished brass against dark gunmetal pewter. The scenes have no reflection
// probes, so a soft fake surround (bright above, dark below) stands in for reflections,
// and the metals are only partly metallic so lamplight still lands on them.
#include "res://assets/pieces/wood_hd/styles/common.gdshaderinc"

void fragment() {
    vec3 albedo = finish == 1 ? vec3(0.22, 0.23, 0.25) : vec3(0.93, 0.72, 0.33);
    float brushed = piece_noise(local_position * vec3(60.0, 2.0, 60.0));
    albedo *= 1.0 - wear.y * 0.45;
    // Handled edges rubbed bright.
    albedo = mix(albedo, albedo * 1.35, wear.x * 0.5);
    float rough = finish == 1 ? 0.42 : 0.28;
    float metal = 0.75;
    vec3 emission = vec3(0.0);
    if (on_felt()) {
        albedo = vec3(0.02, 0.06, 0.034); rough = 1.0; metal = 0.0;
    } else {
        vec3 world_normal = (INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz;
        float surround = smoothstep(-0.3, 0.9, world_normal.y);
        float edge = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.0);
        emission = albedo * (0.05 + surround * 0.16 + edge * 0.12);
    }
    piece_select(albedo, emission);
    ALBEDO = albedo;
    EMISSION = emission;
    ROUGHNESS = rough + brushed * 0.08;
    METALLIC = metal;
    SPECULAR = 0.7;
}
