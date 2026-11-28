shader_type canvas_item;
render_mode unshaded;
// The grade of menu/pixel_finish.gdshader (split tone, oval vignette) for a scene stage rendered
// at full window resolution: the same look as the sharp board overlay, without the pixel grid
// or the palette dither.

uniform sampler2D stage_texture : filter_linear, repeat_disable;
uniform float vignette = 0.42;
uniform vec3 shadow_tint = vec3(0.012, 0.032, 0.040);
uniform vec3 highlight_tint = vec3(1.02, 0.99, 0.95);

void fragment() {
	vec3 light = texture(stage_texture, UV).rgb;
	float luma = dot(light, vec3(0.2126, 0.7152, 0.0722));
	light = mix(light + shadow_tint * (1.0 - smoothstep(0.0, 0.25, luma)), light * highlight_tint,
		smoothstep(0.35, 0.9, luma));
	vec2 centered = (UV - 0.5) * vec2(1.0, 1.25);
	float edge = smoothstep(0.30, 0.78, length(centered));
	light *= 1.0 - vignette * edge * edge;
	COLOR = vec4(clamp(light, 0.0, 1.0), 1.0);
}
