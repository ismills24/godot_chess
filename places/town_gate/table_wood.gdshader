shader_type spatial;
// The guard's table and its stools: old oiled boards, procedural in world space so it stays
// crisp at full resolution whatever UVs the prop came with. Boards run along `plank_axis` on
// up-facing surfaces and upright on the sides; each board has its own tone, long wavy grain
// lines, the odd knot, dark gaps between boards and a soft sheen where hands have worn it.

#include "res://menu/alley_noise.gdshaderinc"

uniform vec4 wood_color : source_color = vec4(0.36, 0.13, 0.075, 1.0);
uniform vec4 grain_color : source_color = vec4(0.19, 0.065, 0.04, 1.0);
uniform vec4 gap_color : source_color = vec4(0.1, 0.05, 0.03, 1.0);
uniform float plank_width = 0.13;
// Angle (radians, about Y) the top boards run at.
uniform float plank_angle = 0.3;

varying vec3 world_at;
varying vec3 world_normal;

void vertex() {
	world_at = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

float fbm(vec2 p) {
	return alley_noise(p) * 0.5 + alley_noise(p * 2.03 + 5.3) * 0.27 + alley_noise(p * 4.1 + 1.7) * 0.15
		+ alley_noise(p * 8.3 + 8.2) * 0.08;
}

void fragment() {
	vec3 n = abs(world_normal);
	vec2 q; // q.x along the grain, q.y across the boards
	if (n.y > 0.6) {
		float c = cos(plank_angle);
		float s = sin(plank_angle);
		q = vec2(world_at.x * c - world_at.z * s, world_at.x * s + world_at.z * c);
	} else {
		// Sides: grain runs upright, boards wrap round.
		float around = n.x > n.z ? world_at.z : world_at.x;
		q = vec2(world_at.y, around);
	}
	float board = floor(q.y / plank_width);
	float across = fract(q.y / plank_width);
	float seed = cell_hash(vec2(board, 3.0));
	float along = q.x + seed * 13.0;
	// Grain: stretched noise rings, wavering.
	float warp = fbm(vec2(along * 0.9, q.y * 5.0 + seed * 5.0)) * 7.0 + fbm(vec2(along * 4.0, q.y * 20.0)) * 0.8;
	float ring_at = q.y * 45.0 + warp + seed * 7.0;
	float rings = fract(ring_at);
	float aa = fwidth(ring_at) + 0.02;
	float line = smoothstep(0.0, 0.1 + aa, rings) * (1.0 - smoothstep(0.7 - aa, 1.0, rings));
	float fine = alley_noise(vec2(along * 4.0, q.y * 400.0)) * 0.6 + alley_noise(vec2(along * 9.0, q.y * 900.0)) * 0.4;
	// Knots: the odd dark eye with rings bent round it.
	vec2 knot_at = vec2(floor(along / 0.6) * 0.6 + 0.3, 0.5);
	float knot_seed = cell_hash(vec2(floor(along / 0.6), board + 11.0));
	vec2 dk = vec2((along - knot_at.x) * 2.5, (across - knot_at.y) * plank_width * 10.0);
	float knot = knot_seed > 0.85 ? 1.0 - smoothstep(0.01, 0.025, length(dk) * 0.4) : 0.0;
	vec3 wood = mix(wood_color.rgb, wood_color.rgb * 0.82, seed);
	wood = mix(wood, grain_color.rgb, (1.0 - line) * 0.28 + fine * 0.32);
	wood = mix(wood, grain_color.rgb * 0.6, knot);
	// Open pores: short dark dashes strung along the grain.
	float pore_n = alley_noise(vec2(along * 30.0, ring_at * 9.0 + board * 3.0));
	float pore = smoothstep(0.72, 0.9, pore_n) / (1.0 + fwidth(ring_at * 9.0));
	wood *= 1.0 - 0.14 * pore * (1.0 - knot);
	// Long blotchy colour drifts and worn lighter patches.
	float blotch = fbm(q * vec2(0.8, 3.0) + seed);
	wood *= 0.88 + 0.24 * blotch;
	// Gaps between boards.
	float to_gap = min(across, 1.0 - across) * plank_width;
	float gap_aa = fwidth(q.y) * 0.75;
	float gap = 1.0 - smoothstep(0.0015, 0.0015 + gap_aa + 0.0015, to_gap);
	vec3 color = mix(wood, gap_color.rgb, gap * 0.9);
	ALBEDO = color;
	ROUGHNESS = mix(0.5, 0.75, fine) + gap * 0.25;
	SPECULAR = 0.4;
}
