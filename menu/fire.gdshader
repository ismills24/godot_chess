shader_type spatial;
render_mode unshaded, cull_back, depth_draw_never, shadows_disabled, blend_add;

uniform float elapsed = 0.0;
varying float height;
varying float face_heat;
varying float tongue_heat;
varying float flicker;

void vertex() {
	height = UV.y;
	float seed = INSTANCE_CUSTOM.x * 6.28318;
	float rise = elapsed * (6.6 + INSTANCE_CUSTOM.y * 3.4) + seed;
	float pulse = sin(rise - height * 5.7);
	VERTEX.y *= 0.78 + 0.25 * pulse + 0.12 * sin(rise * 1.63);
	VERTEX.xz *= 0.91 + 0.22 * cos(rise + height * 4.1);
	VERTEX.xz += height * height * vec2(0.31 * sin(rise * 0.81), 0.19 * cos(rise * 1.14));
	face_heat = COLOR.r;
	tongue_heat = INSTANCE_CUSTOM.z;
	flicker = 0.91 + 0.09 * pulse;
}

void fragment() {
	float heat = clamp(1.0 - height * 1.08 + (face_heat - 0.55) * 0.20 + tongue_heat * 0.22, 0.0, 1.0);
	vec3 red = vec3(0.89, 0.11, 0.017);
	vec3 amber = vec3(1.0, 0.40, 0.047);
	vec3 core = vec3(1.0, 0.72, 0.23);
	ALBEDO = mix(mix(red, amber, smoothstep(0.15, 0.69, heat)), core, smoothstep(0.58, 0.97, heat)) * 1.9;
	// Additive shells overlap without sorting and thin out at the tips.
	ALPHA = flicker * (0.26 + tongue_heat * 0.08)
		* smoothstep(0.0, 0.10, height) * (1.0 - smoothstep(0.36, 1.0, height));
}
