shader_type spatial;
#include "res://menu/alley_noise.gdshaderinc"
uniform vec2 board_size = vec2(8.0);
uniform float square_size = 1.45;
uniform float drawing_seed = 7.0;
uniform sampler2D cobbles : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D relief : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
varying vec3 world_at;
void vertex() { world_at = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
void fragment() {
	vec2 at = world_at.xz;
	vec2 p = at / square_size + board_size * 0.5;
	// Continuous warp keeps neighbouring cells joined while varying their shapes.
	vec2 wobble = vec2(alley_noise(vec2(p.y * 1.8, p.x * 0.7) + drawing_seed),
		alley_noise(vec2(p.x * 1.6, p.y * 0.8) + drawing_seed + 31.0)) - 0.5;
	p += wobble * 0.12 + vec2(sin(p.y * 3.1), sin(p.x * 2.7)) * 0.014;
	vec2 cell = floor(p);
	vec2 f = fract(p);
	vec2 edge = min(f, 1.0 - f);
	float seed = cell_hash(cell + drawing_seed);
	float aa = max(fwidth(p.x), fwidth(p.y));
	float pressure = 0.006 + 0.009 * alley_noise(p * 4.3 + 12.0);
	float grid = 1.0 - smoothstep(pressure, pressure + aa, min(edge.x, edge.y));
	vec2 outer = max(-p, p - board_size);
	float inside = 1.0 - smoothstep(0.0, aa, max(outer.x, outer.y));
	float strokes_inside = 1.0 - smoothstep(0.025, 0.065, max(outer.x, outer.y));
	// Side-of-chalk strokes change direction, coverage and pressure per cell.
	float angle = -0.8 + seed * 1.6;
	vec2 direction = vec2(cos(angle), sin(angle));
	float across = dot(f, direction);
	float bands = alley_noise(vec2(across * (17.0 + seed * 12.0),
		dot(f, vec2(-direction.y, direction.x)) * 2.0) + seed * 71.0);
	float rub = alley_noise(f * 4.0 + cell * 17.0);
	float margin = 0.008 + 0.018 * alley_noise(p * 8.0);
	float fill_edge = smoothstep(margin, margin + 0.025, min(edge.x, edge.y));
	float fill = (0.38 + 0.28 * bands + 0.14 * rub) * fill_edge;
	float light_cell = 1.0 - mod(cell.x + cell.y, 2.0);
	float grain = alley_noise(at * 47.0);
	float chalk = max(fill * light_cell * inside, grid * strokes_inside * 0.76);
	chalk *= 0.7 + 0.3 * smoothstep(0.12, 0.75, grain);
	// Dry charcoal paving around play; damp cobbles at the landing's edge.
	vec2 half_span = board_size * square_size * 0.5;
	float landing_edge = abs(at.x) - half_span.x - 2.6;
	float street = smoothstep(-0.08, 0.08, landing_edge);
	float stone_noise = alley_noise(at * 1.3) * 0.6 + alley_noise(at * 9.0) * 0.4;
	vec3 slate = mix(vec3(0.14, 0.17, 0.18), vec3(0.19, 0.22, 0.23), stone_noise);
	vec2 seam = abs(fract((at + vec2(0.7, 1.1)) / vec2(4.8, 3.7)) - 0.5);
	float joint = smoothstep(0.493, 0.5, max(seam.x, seam.y));
	slate *= 1.0 - joint * 0.12;
	vec3 cobble = texture(cobbles, at / 12.0).rgb * vec3(0.25, 0.34, 0.37);
	ALBEDO = mix(mix(slate, vec3(0.48, 0.46, 0.40), chalk), cobble, street);
	ROUGHNESS = mix(0.96, 0.55, street);
	SPECULAR = mix(0.12, 0.38, street);
	NORMAL_MAP = mix(vec3(0.5, 0.5, 1.0), texture(relief, at / 12.0).rgb, street * 0.65);
}
