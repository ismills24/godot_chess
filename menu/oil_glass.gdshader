shader_type spatial;

#include "res://menu/alley_noise.gdshaderinc"

varying vec3 glass_at;

void vertex() {
	glass_at = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 p = glass_at.zy;
	float oil_glow = alley_noise(p * vec2(2.5, 1.9) + 3.17);
	float mottling = alley_noise(p * vec2(11.5, 8.0)) * 0.64
		+ alley_noise(p * vec2(24.0, 25.0) + 7.29) * 0.36;
	float grime = alley_noise(p * vec2(12.0, 1.25) + 6.01);
	float transmission = smoothstep(0.08, 0.96, oil_glow * 0.65 + mottling * 0.35);
	vec3 amber = mix(vec3(0.19, 0.069, 0.022), vec3(0.94, 0.59, 0.28), transmission);
	ALBEDO = mix(vec3(0.045, 0.049, 0.045), amber * 0.27, transmission);
	ROUGHNESS = mix(0.33, 0.78, grime);
	EMISSION = amber * (0.31 + 0.56 * transmission) * mix(0.54, 1.0, grime) * 2.2;
}
