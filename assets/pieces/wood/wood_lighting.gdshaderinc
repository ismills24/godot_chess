// Shared piece finish for surface splats and solid joints. Pieces are lit by the
// scene (lantern, fire, ambient), so this only describes the material.
// finish: 0 = maple, 1 = walnut, 2 = gilt (gold leaf over wood, for special pieces).
// edge/cavity are baked per splat in wood_shapes.gd; felt marks the foot pad band.

float piece_hash(vec3 p) {
    p = fract(p * 0.3183099 + 0.1);
    p *= 17.0;
    return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float piece_noise(vec3 x) {
    vec3 i = floor(x);
    vec3 f = fract(x);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(mix(piece_hash(i), piece_hash(i + vec3(1, 0, 0)), f.x),
                   mix(piece_hash(i + vec3(0, 1, 0)), piece_hash(i + vec3(1, 1, 0)), f.x), f.y),
               mix(mix(piece_hash(i + vec3(0, 0, 1)), piece_hash(i + vec3(1, 0, 1)), f.x),
                   mix(piece_hash(i + vec3(0, 1, 1)), piece_hash(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}

void piece_finish(vec3 p, int finish, float edge, float cavity, bool felt,
        out vec3 albedo, out float roughness, out float metallic, out float specular) {
    if (felt) {
        // Green baize glued under the foot.
        albedo = vec3(0.020, 0.060, 0.034);
        roughness = 1.0;
        metallic = 0.0;
        specular = 0.0;
        return;
    }
    // PS1-era read: a flat painted colour with a few broad, low-contrast tones,
    // like a small palettised texture. No fine grain, no gloss.
    float streak = piece_noise(p * vec3(3.0, 0.9, 3.0) + 7.3);
    float patch = piece_noise(p * 1.7 + 2.1);
    float tone = floor(clamp(streak * 0.7 + patch * 0.3, 0.0, 0.999) * 4.0) / 3.0;
    float grime = clamp(cavity, 0.0, 1.0);
    float rubbed = clamp(edge, 0.0, 1.0);
    if (finish == 2) {
        // Scenes have no reflection probes, so leaf is only partly metallic:
        // the diffuse share keeps it reading as gold under lantern light.
        // Brassy yellow, not orange, so it separates from maple under lamplight.
        albedo = mix(vec3(0.90, 0.74, 0.22), vec3(1.0, 0.88, 0.36), tone);
        albedo *= 1.0 - grime * 0.35;
        roughness = 0.38;
        metallic = 0.6;
        specular = 0.6;
        return;
    }
    vec3 light_tone = finish == 1 ? vec3(0.215, 0.118, 0.064) : vec3(0.54, 0.40, 0.25);
    vec3 dark_tone = finish == 1 ? vec3(0.170, 0.090, 0.048) : vec3(0.48, 0.345, 0.205);
    albedo = mix(dark_tone, light_tone, tone);
    // Only a hint of handling: slightly lighter lips, softly darker recesses.
    albedo *= 1.0 + rubbed * 0.06 - grime * 0.22;
    roughness = 0.85;
    metallic = 0.0;
    specular = 0.18;
}

// Shadow fill: a share of the piece's own colour added as emission, the same everywhere on the
// piece, while the lit colour is dimmed to match, so the shadowed side lifts and the lit side
// stays about where it was: lit and shadowed zones end up closer in brightness.
uniform float shadow_fill : hint_range(0.0, 1.0) = 0.35;

vec3 piece_fill(vec3 albedo) {
    return albedo * shadow_fill;
}

float piece_fill_keep() {
    return 1.0 - shadow_fill * 0.6;
}
