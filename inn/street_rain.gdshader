shader_type spatial;
// Inn copy of the menu rain: the streaks fall over a configurable patch of street
// (rain_from .. rain_from + rain_span, in world x/z) instead of the menu alley.
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled;

#include "res://menu/alley_noise.gdshaderinc"

uniform float elapsed = 0.0;
uniform vec3 fire_at;
uniform vec3 lantern_at;
uniform vec2 rain_from = vec2(-8.0, 1.6);
uniform vec2 rain_span = vec2(17.0, 10.0);
varying vec3 streak_at;
varying float visibility;
varying float variation;

void vertex() {
	vec4 seed = INSTANCE_CUSTOM;
	float fall_time = 0.88 + seed.y * 0.49;
	float period = fall_time + 0.52 + seed.z * 1.33;
	float clock = elapsed + seed.w * period;
	float event_index = floor(clock / period);
	float age = mod(clock, period) / fall_time;
	vec2 event = vec2(event_index, seed.x * 251.0);
	float origin_x = cell_hash(event);
	float origin_z = cell_hash(event + vec2(19.0, 43.0));
	float drift = 0.10 + seed.x * 0.085;
	float drop = clamp(age, 0.0, 1.0) * (5.9 + seed.y * 1.4);
	float length_scale = mix(0.53, 1.15, seed.y);
	VERTEX.y *= length_scale;
	// The same slope drives the streak and its travel, so the tip leads the fall.
	VERTEX.x -= VERTEX.y * drift;
	VERTEX += vec3(rain_from.x + rain_span.x * origin_x + drop * drift,
		0.13 + 5.9 + seed.y * 1.4 - drop, rain_from.y + rain_span.y * origin_z);
	streak_at = VERTEX;
	visibility = smoothstep(0.0, 0.11, age) * (1.0 - smoothstep(0.88, 1.0, age));
	variation = seed.y;
}

void fragment() {
	float tapered = 1.0 - abs(UV.x * 2.0 - 1.0);
	float tail = smoothstep(0.04, 0.82, UV.y) * (1.0 - smoothstep(0.85, 1.0, UV.y));
	float depth = distance(CAMERA_POSITION_WORLD, streak_at);
	float in_alley = smoothstep(1.2, 2.6, depth) * (1.0 - smoothstep(10.0, 23.0, depth));
	float fire = 1.0 - smoothstep(0.55, 4.5, distance(streak_at, fire_at));
	float lantern = 1.0 - smoothstep(0.45, 3.2, distance(streak_at, lantern_at));
	float amber = max(fire, lantern);
	ALBEDO = mix(vec3(0.51, 0.70, 0.75), vec3(0.90, 0.66, 0.41), amber);
	ALPHA = tapered * tail * in_alley * visibility * (0.21 + variation * 0.17 + amber * 0.18);
}
