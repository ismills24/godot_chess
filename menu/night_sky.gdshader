shader_type sky;

#include "res://menu/alley_noise.gdshaderinc"

void sky() {
	float height = clamp(EYEDIR.y, 0.0, 1.0);
	vec2 clouds_at = EYEDIR.xz / (0.45 + height) * 4.4;
	float bands = alley_noise(clouds_at * vec2(1.0, 1.9)) * 0.69;
	bands += alley_noise(clouds_at * vec2(2.3, 3.7) + 6.2) * 0.31;
	vec3 horizon = vec3(0.176, 0.344, 0.378);
	vec3 night = vec3(0.033, 0.105, 0.134);
	COLOR = mix(horizon, night, smoothstep(0.17, 0.86, height));
	COLOR *= 0.82 + bands * 0.31;
}
