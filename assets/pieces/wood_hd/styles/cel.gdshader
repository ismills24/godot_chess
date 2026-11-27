shader_type spatial;
// Cel-shaded finish for the high-resolution pieces: one flat colour per wood, light in hard
// bands, a small hard highlight. The outline is a second pass (outline.gdshader).
// finish: 0 = maple, 1 = walnut, 2 = gilt.
#include "res://assets/pieces/wood_hd/styles/common.gdshaderinc"
// Brightness of the lit, half-lit and shadowed bands.
uniform vec3 bands = vec3(0.9, 0.5, 0.2);

void fragment() {
    vec3 albedo = finish == 1 ? vec3(0.12, 0.06, 0.032)
        : finish == 2 ? vec3(0.95, 0.78, 0.26)
        : vec3(0.64, 0.48, 0.30);
    // Only the deepest recesses darken, in one hard step.
    albedo *= wear.y > 0.55 ? 0.78 : 1.0;
    if (on_felt()) {
        albedo = vec3(0.02, 0.06, 0.034);
    }
    ALBEDO = mix(albedo, vec3(0.4, 0.85, 1.0), selection_amount);
    EMISSION = vec3(0.25, 0.6, 0.8) * selection_amount * 0.6 + piece_fill(ALBEDO);
    ALBEDO *= piece_fill_keep();
    ROUGHNESS = 1.0;
    SPECULAR = 0.0;
}

void light() {
    // Bands come from the angle to the light only; distance falloff and shadow scale the
    // whole band like ordinary lighting, so many weak lights don't each add full strength.
    float facing = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
    float band = mix(bands.z, bands.y, smoothstep(0.08, 0.14, facing));
    band = mix(band, bands.x, smoothstep(0.5, 0.56, facing));
    DIFFUSE_LIGHT += LIGHT_COLOR / PI * band * ATTENUATION;
    vec3 half_vector = normalize(LIGHT + VIEW);
    float shine = smoothstep(0.965, 0.975, dot(NORMAL, half_vector)) * step(0.3, facing);
    SPECULAR_LIGHT += LIGHT_COLOR / PI * shine * 0.35 * ATTENUATION;
}
