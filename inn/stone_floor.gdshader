shader_type spatial;
// The bar room floor: big, irregular, badly laid stones. Fully procedural, in world space on
// a face looking up Y. Stones are Voronoi cells over a warped jittered grid, so no two are
// the same size or shape; the joints between them are wide, crooked and packed with dark
// grime, their edges are rounded and chipped, some stones are cracked, some sit a little
// proud or sunk (shaded lighter or darker), and the walkways are worn smoother.

#include "res://menu/alley_noise.gdshaderinc"

uniform vec4 stone_color : source_color = vec4(0.34, 0.31, 0.28, 1.0);
uniform vec4 grime_color : source_color = vec4(0.12, 0.1, 0.085, 1.0);
// Average stone size in metres.
uniform float stone_size = 0.6;
uniform float joint = 0.022;
varying vec3 world_at;

void vertex() {
	world_at = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

// Nearest and second-nearest feature points of a jittered grid: returns (F1 distance, edge
// distance), and the nearest cell's id in `id`.
vec2 stones(vec2 p, out vec2 id) {
	vec2 cell = floor(p);
	float best = 8.0;
	float second = 8.0;
	vec2 best_point = vec2(0.0);
	for (int y = -1; y <= 1; y++) {
		for (int x = -1; x <= 1; x++) {
			vec2 c = cell + vec2(float(x), float(y));
			vec2 point = c + 0.12 + 0.76 * vec2(cell_hash(c + 5.3), cell_hash(c * 1.31 + 9.7));
			float d = length(p - point);
			if (d < best) {
				second = best;
				best = d;
				best_point = point;
				id = c;
			} else if (d < second) {
				second = d;
			}
		}
	}
	// Distance to the bisector between the two nearest points: the true joint distance.
	float edge = 8.0;
	for (int y = -1; y <= 1; y++) {
		for (int x = -1; x <= 1; x++) {
			vec2 c = cell + vec2(float(x), float(y));
			vec2 point = c + 0.12 + 0.76 * vec2(cell_hash(c + 5.3), cell_hash(c * 1.31 + 9.7));
			if (length(point - best_point) < 0.001) {
				continue;
			}
			vec2 mid = (best_point + point) * 0.5;
			vec2 across = normalize(point - best_point);
			edge = min(edge, dot(mid - p, across));
		}
	}
	return vec2(best, edge);
}

float joint_mask(vec2 p, out vec2 id, out float edge) {
	// Warp the plane so stone outlines wobble rather than run straight.
	vec2 warped = p + 0.18 * vec2(alley_noise(p * 1.3) - 0.5, alley_noise(p * 1.3 + 7.1) - 0.5);
	vec2 s = stones(warped / stone_size, id);
	edge = s.y * stone_size;
	// Ragged, uneven joint width: chipped edges eat into the stone in places.
	float ragged = joint * (0.6 + 0.9 * alley_noise(p * 9.0)) + 0.02 * smoothstep(0.72, 0.9, alley_noise(p * 4.3 + 2.0));
	return 1.0 - smoothstep(ragged, ragged + 0.012, edge);
}

void fragment() {
	vec2 p = world_at.xz;
	vec2 id;
	float edge;
	float in_joint = joint_mask(p, id, edge);
	float seed = cell_hash(id + 31.0);
	float seed_b = cell_hash(id * 2.3 + 11.0);
	// Each stone its own shade and tint: grey, brown, a few greenish or rusty.
	vec3 stone = stone_color.rgb * (0.7 + 0.55 * seed);
	stone *= mix(vec3(1.0), vec3(1.08, 0.97, 0.86), step(0.7, seed_b));
	stone *= mix(vec3(1.0), vec3(0.9, 1.0, 0.92), step(0.88, seed_b));
	// Surface grain and pitting.
	float grain = alley_noise(p * 14.0) * 0.6 + alley_noise(p * 37.0) * 0.4;
	stone *= 0.78 + 0.4 * grain;
	// A rounded, darker rim where each stone's edge falls away into the joint.
	float rim = smoothstep(0.0, 0.05, edge);
	stone *= mix(0.7, 1.0, rim);
	// Cracks across some stones.
	float crack_line = abs(alley_noise(p * 2.2 + seed * 13.0) - 0.5);
	float crack = step(0.62, seed_b) * (1.0 - smoothstep(0.006, 0.016, crack_line));
	stone = mix(stone, grime_color.rgb, crack * 0.8);
	// Worn smoother and paler along the walkways (the lane by the bar, the aisle to the door).
	float worn = alley_noise(p * vec2(0.6, 0.25)) * smoothstep(1.6, 0.0, abs(p.x - 0.6));
	stone = mix(stone, stone * 1.18, worn * 0.5);
	vec3 grime = grime_color.rgb * (0.8 + 0.5 * alley_noise(p * 6.0));
	ALBEDO = mix(stone, grime, in_joint);
	ROUGHNESS = mix(mix(0.88, 0.62, worn), 1.0, in_joint);
	SPECULAR = 0.3;
	// Fake relief: stones bulge up from their joints, each tilted a touch differently.
	float bulge = 1.0 - rim;
	vec2 tilt = (vec2(seed, seed_b) - 0.5) * 0.25;
	vec3 n = normalize(vec3(tilt * rim + (grain - 0.5) * 0.15, 1.0));
	NORMAL_MAP = vec3(n.xy * 0.5 + 0.5, 1.0);
	NORMAL_MAP_DEPTH = 1.0 + bulge * 1.5;
}
