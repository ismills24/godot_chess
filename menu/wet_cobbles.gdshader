shader_type spatial;

#include "res://menu/alley_noise.gdshaderinc"

uniform sampler2D cobbles : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D relief : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float elapsed = 0.0;
uniform vec3 fire_at;
uniform vec3 backlight_at;
uniform vec3 lantern_at;
uniform float lantern_strength;
varying vec3 world_at;

void vertex() {
	world_at = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 tiles = UV * vec2(3.65, 20.0);
	vec2 at = world_at.xz;
	float pooled = alley_noise(at * vec2(1.08, 0.57)) * 0.72 + alley_noise(at * vec2(3.80, 2.02)) * 0.28;
	float water = smoothstep(0.49, 0.72, pooled);
	float wet = smoothstep(0.34, 0.69, pooled);
	vec3 stone = texture(cobbles, tiles).rgb;
	vec3 grooves = texture(relief, tiles).rgb;
	ALBEDO = stone * mix(vec3(0.59, 0.63, 0.61), vec3(0.23, 0.40, 0.44), wet);
	ROUGHNESS = mix(0.80, 0.16, wet);
	SPECULAR = mix(0.29, 0.73, water);
	NORMAL_MAP = mix(grooves, vec3(0.5, 0.5, 1.0), water * 0.81);
	NORMAL_MAP_DEPTH = 1.06;

	// Broaden the reflections only on flooded cobbles without a second scene pass.
	vec3 view = normalize(CAMERA_POSITION_WORLD - world_at);
	vec3 ripples = normalize(vec3((grooves.r - 0.5) * 0.21, 1.0, (grooves.g - 0.5) * 0.19));
	vec3 to_fire = normalize(fire_at - world_at);
	vec3 to_mist = normalize(backlight_at - world_at);
	vec3 to_lantern = normalize(lantern_at - world_at);
	float fire_glint = smoothstep(0.929, 0.997, dot(view, reflect(-to_fire, ripples)));
	float sky_glint = smoothstep(0.84, 0.992, dot(view, reflect(-to_mist, ripples)));
	float lantern_glint = smoothstep(0.947, 0.997, dot(view, reflect(-to_lantern, ripples)));
	float grazing = pow(1.0 - max(view.y, 0.0), 3.0);
	float open_sky = 1.0 - smoothstep(0.26, 1.83, abs(at.x + 0.13));
	float cobble_faces = smoothstep(0.23, 0.53, dot(stone, vec3(0.31, 0.52, 0.17)));
	float flicker = 0.83 + sin(elapsed * 8.4) * 0.10 + sin(elapsed * 17.1) * 0.07;
	float reflection_mask = max(water * 0.82, wet * 0.76 * cobble_faces);
	vec3 cool = vec3(0.22, 0.37, 0.43) * (0.58 * sky_glint + 1.87 * grazing * open_sky);
	float warm_spill = 1.0 - smoothstep(0.0, 2.8, length((at - fire_at.xz) * vec2(1.35, 0.78)));
	vec3 warm = vec3(0.89, 0.33, 0.083) * (fire_glint + 0.30 * warm_spill * grazing) * flicker;
	warm += vec3(0.23, 0.10, 0.026) * lantern_glint * lantern_strength;
	EMISSION = reflection_mask * (cool + warm);

	vec2 splash = at * vec2(1.6, 1.23);
	vec2 cell = floor(splash);
	float seed = cell_hash(cell);
	float age = fract(elapsed * (1.12 + seed * 0.33) + seed * 3.79);
	vec2 center = vec2(seed, cell_hash(cell + 13.0)) * 0.38 + 0.31;
	float ring = 1.0 - smoothstep(0.012, 0.047, abs(length(fract(splash) - center) - age * 0.31));
	EMISSION += water * ring * (1.0 - age) * vec3(0.075, 0.14, 0.16)
		* (1.0 - smoothstep(6.0, 14.0, distance(CAMERA_POSITION_WORLD, world_at)));
}
