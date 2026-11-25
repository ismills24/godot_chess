shader_type spatial;
// Wet cobbles at true scale against the pieces (one cobble is about two squares),
// adapted from menu/wet_cobbles.gdshader: pooled water, lamp glints, rain rings,
// and a running gutter along the kerb.
#include "res://menu/alley_noise.gdshaderinc"

uniform sampler2D cobbles : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D relief : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float tile_size = 24.0;
uniform float elapsed = 0.0;
uniform float gutter_z = -8.0;
uniform float gutter_width = 1.7;
uniform vec3 lamp_at = vec3(-7.0, 14.0, 5.0);
uniform vec3 candle_at = vec3(7.0, 1.0, 5.0);
uniform float candle_strength = 1.0;
varying vec3 world_at;

void vertex() {
	world_at = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 at = world_at.xz;
	vec2 tiles = at / tile_size;
	float pooled = alley_noise(at * 0.09) * 0.72 + alley_noise(at * 0.31) * 0.28;
	// The gutter runs full of water along the kerb.
	float gutter = 1.0 - smoothstep(gutter_width * 0.7, gutter_width, at.y - gutter_z);
	float water = max(smoothstep(0.52, 0.72, pooled), gutter);
	float wet = max(smoothstep(0.30, 0.66, pooled), gutter);
	vec3 stone = texture(cobbles, tiles).rgb;
	vec3 grooves = texture(relief, tiles).rgb;
	ALBEDO = stone * mix(vec3(0.50, 0.53, 0.52), vec3(0.20, 0.34, 0.37), wet);
	ALBEDO = mix(ALBEDO, vec3(0.05, 0.075, 0.08), gutter * 0.6);
	ROUGHNESS = mix(0.82, 0.18, wet);
	SPECULAR = mix(0.25, 0.7, water);
	NORMAL_MAP = mix(grooves, vec3(0.5, 0.5, 1.0), water * 0.85);
	NORMAL_MAP_DEPTH = 1.0;

	// Fake reflections of the lamp and the candle in standing water.
	vec3 view = normalize(CAMERA_POSITION_WORLD - world_at);
	vec3 ripples = normalize(vec3((grooves.r - 0.5) * 0.21, 1.0, (grooves.g - 0.5) * 0.19));
	float lamp_glint = smoothstep(0.93, 0.997, dot(view, reflect(-normalize(lamp_at - world_at), ripples)));
	float candle_pool = 1.0 - smoothstep(0.0, 4.0, distance(at, candle_at.xz));
	float flow = alley_noise(vec2(at.x * 0.8 - elapsed * 1.6, at.y * 2.2));
	vec3 glints = vec3(0.85, 0.55, 0.25) * lamp_glint * 0.3
		+ vec3(0.9, 0.42, 0.10) * candle_pool * candle_pool * 0.25 * candle_strength
		+ vec3(0.10, 0.18, 0.20) * gutter * smoothstep(0.55, 0.8, flow);
	EMISSION = water * glints;

	// Rain rings in puddles.
	vec2 splash = at * 0.45;
	vec2 cell = floor(splash);
	float seed = cell_hash(cell);
	float age = fract(elapsed * (0.9 + seed * 0.33) + seed * 3.79);
	vec2 center = vec2(seed, cell_hash(cell + 13.0)) * 0.38 + 0.31;
	float ring = 1.0 - smoothstep(0.012, 0.05, abs(length(fract(splash) - center) - age * 0.33));
	EMISSION += water * ring * (1.0 - age) * vec3(0.07, 0.12, 0.14);
}
