shader_type canvas_item;
render_mode unshaded;

uniform sampler2D stage_texture : filter_linear, repeat_disable;
uniform float vignette = 0.42;
uniform vec3 shadow_tint = vec3(0.012, 0.032, 0.040);
uniform vec3 highlight_tint = vec3(1.02, 0.99, 0.95);

float bayer2(vec2 cell) {
	return 2.0 * cell.x + 3.0 * cell.y - 4.0 * cell.x * cell.y;
}

void fragment() {
	vec2 size = vec2(textureSize(stage_texture, 0));
	vec2 cell = floor(UV * size);
	vec2 texel = UV * size - 0.5;
	vec2 transition = smoothstep(vec2(0.23), vec2(0.77), fract(texel));
	vec2 sample_at = (floor(texel) + transition + 0.5) / size;
	float order = 4.0 * bayer2(mod(cell, 2.0)) + bayer2(mod(floor(cell * 0.5), 2.0));
	float dither = (order + 0.5) / 16.0 - 0.5;
	vec3 light = texture(stage_texture, sample_at).rgb;

	// Split tone: lift crushed shadows toward teal, warm the highlights.
	float luma = dot(light, vec3(0.2126, 0.7152, 0.0722));
	light = mix(light + shadow_tint * (1.0 - smoothstep(0.0, 0.25, luma)), light * highlight_tint,
		smoothstep(0.35, 0.9, luma));

	// Oval vignette, evaluated per stage pixel so it stays on the pixel grid.
	vec2 centered = ((cell + 0.5) / size - 0.5) * vec2(1.0, 1.25);
	float edge = smoothstep(0.30, 0.78, length(centered));
	light *= 1.0 - vignette * edge * edge;

	vec3 palette = floor(light * 31.0 + 0.5 + dither * 0.32) / 31.0;
	vec3 color = mix(light, palette, 0.60);
	COLOR = vec4(clamp(color, 0.0, 1.0), 1.0);
}
