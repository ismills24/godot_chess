shader_type spatial;
render_mode unshaded;

uniform float elapsed = 0.0;
varying vec3 glass_at;

void vertex() {
	glass_at = VERTEX;
}

void fragment() {
	float angle = atan(glass_at.z, glass_at.x);
	float pane = fract(angle * (6.0 / TAU) + 0.5);
	float height = clamp(glass_at.y / 0.342 + 0.5, 0.0, 1.0);
	float center = 1.0 - abs(pane * 2.0 - 1.0);
	float wick = exp(-pow((height - 0.38) * 3.1, 2.0)) * (0.54 + center * 0.46);
	float patina = 0.88 + 0.08 * sin(angle * 17.0 - height * 20.0) + 0.04 * sin(height * 53.0);
	float flicker = 0.93 + 0.05 * sin(elapsed * 6.1) + 0.02 * sin(elapsed * 14.2);
	ALBEDO = mix(vec3(0.45, 0.19, 0.044), vec3(1.0, 0.79, 0.42), wick) * flicker * patina * 2.4;
}
