shader_type canvas_item;
uniform bool limited_palette = true;

void fragment() {
    vec4 source = texture(TEXTURE, UV);
    vec3 result = source.rgb;
    if (limited_palette) {
        // Small shared palette keeps highlights warm and shadows cool.
        vec3 palette[12] = vec3[12](
            vec3(0.055, 0.071, 0.10), vec3(0.09, 0.12, 0.16),
            vec3(0.15, 0.20, 0.24), vec3(0.23, 0.29, 0.31),
            vec3(0.34, 0.39, 0.38), vec3(0.44, 0.46, 0.40),
            vec3(0.53, 0.49, 0.37), vec3(0.64, 0.57, 0.42),
            vec3(0.74, 0.67, 0.51), vec3(0.84, 0.77, 0.62),
            vec3(0.93, 0.87, 0.73), vec3(1.0, 0.96, 0.84));
        float best = 100.0;
        for (int i = 0; i < 12; i++) {
            float difference = dot(source.rgb - palette[i], source.rgb - palette[i]);
            if (difference < best) { best = difference; result = palette[i]; }
        }
    }
    COLOR = vec4(result, source.a);
}
