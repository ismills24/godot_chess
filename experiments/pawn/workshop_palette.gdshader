shader_type canvas_item;
uniform bool limited_palette = true;
void fragment() {
    vec4 source = texture(TEXTURE, UV);
    vec3 result = source.rgb;
    if (limited_palette) {
        vec3 palette[20] = vec3[20](
            vec3(0.055, 0.071, 0.10), vec3(0.09, 0.12, 0.16),
            vec3(0.15, 0.20, 0.24), vec3(0.23, 0.29, 0.31),
            vec3(0.34, 0.39, 0.38), vec3(0.44, 0.46, 0.40),
            vec3(0.53, 0.49, 0.37), vec3(0.64, 0.57, 0.42),
            vec3(0.74, 0.67, 0.51), vec3(0.84, 0.77, 0.62),
            vec3(0.93, 0.87, 0.73), vec3(1.0, 0.96, 0.84),
            vec3(0.14, 0.095, 0.075), vec3(0.23, 0.15, 0.105),
            vec3(0.36, 0.23, 0.145), vec3(0.53, 0.33, 0.19),
            vec3(0.40, 0.48, 0.55), vec3(0.57, 0.65, 0.72),
            vec3(0.74, 0.81, 0.86), vec3(0.91, 0.95, 0.98));
        float best = 100.0;
        for (int i = 0; i < 20; i++) {
            // Reserve rust/wood hues for genuinely reddish materials, so
            // shadowed ivory and iron retain the original cool shadow ramp.
            if (i >= 12 && i < 16 && source.r < source.g * 1.24) { continue; }
            if (i >= 16 && source.b < source.r * 1.04) { continue; }
            float difference = dot(source.rgb - palette[i], source.rgb - palette[i]);
            if (difference < best) { best = difference; result = palette[i]; }
        }
    }
    COLOR = vec4(result, source.a);
}
