shader_type spatial;
// Old, knocked-about red brick in grey mortar, laid in running bond. Fully procedural, in
// world space on a face looking down Z (the hearth front). Bricks are drawn chunkier than
// life so the joints survive the pixel finish. Each brick gets its own shade; edges and
// corners are chipped back into the mortar irregularly, some bricks are cracked or missing
// a chunk of face, and soot creeps up from the fire.

#include "res://menu/alley_noise.gdshaderinc"

uniform vec4 brick_color : source_color = vec4(0.52, 0.17, 0.11, 1.0);
uniform vec4 mortar_color : source_color = vec4(0.5, 0.53, 0.56, 1.0);
uniform vec2 brick_size = vec2(0.3, 0.11);
uniform float joint = 0.018;
// Where the fire opening sits (world x, y of its top centre), for the soot.
uniform vec2 soot_origin = vec2(0.0, 1.05);
varying vec3 world_at;

void vertex() {
	world_at = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 p = world_at.xy;
	float row = floor(p.y / brick_size.y);
	vec2 laid = vec2(p.x / brick_size.x + 0.5 * mod(row, 2.0), p.y / brick_size.y);
	vec2 cell = floor(laid);
	vec2 inside = fract(laid) * brick_size;
	float seed = cell_hash(cell + 17.0);
	float seed_b = cell_hash(cell * 1.7 + 3.0);

	// Distance to the brick's edge, in metres, with the corners rounded off.
	vec2 to_edge = min(inside, brick_size - inside);
	float corner = length(max(vec2(0.03) - to_edge, 0.0));
	float edge = min(to_edge.x, to_edge.y) - corner;
	// Chipping: noise eats the edges back, more on some bricks and more at the corners.
	float wear = 0.4 + 1.2 * seed_b * seed_b;
	float chips = alley_noise(p * 46.0) * 0.6 + alley_noise(p * 130.0) * 0.4;
	float bite = joint * 0.5 + max(chips - 0.35, 0.0) * 0.03 * wear + corner * 0.6;
	bool in_mortar = edge < bite;

	// A few bricks have lost a chunk of face, leaving a rough crater.
	float crater = 0.0;
	if (seed > 0.86) {
		vec2 hole = vec2(cell_hash(cell + 5.0), cell_hash(cell + 9.0)) * brick_size;
		float reach = length((inside - hole) / brick_size * vec2(1.0, 0.45));
		crater = 1.0 - smoothstep(0.16, 0.22, reach + (alley_noise(p * 70.0) - 0.5) * 0.12);
	}
	// Hairline cracks on others.
	float crack = 0.0;
	if (seed_b > 0.72) {
		float line = abs(alley_noise(p * vec2(9.0, 22.0) + seed * 40.0) - 0.5);
		crack = 1.0 - smoothstep(0.012, 0.03, line);
	}

	// Brick body: its own shade (a few over-fired dark, a few pale and orange), speckled.
	vec3 shade = brick_color.rgb * mix(0.72, 1.18, seed);
	shade = mix(shade, vec3(0.62, 0.3, 0.17), step(0.82, seed_b) * 0.6);
	shade = mix(shade, vec3(0.2, 0.08, 0.06), step(seed, 0.1) * 0.7);
	float speckle = alley_noise(p * 160.0);
	shade *= 0.86 + speckle * 0.24;
	// Edges worn darker, as if rounded.
	shade *= mix(0.72, 1.0, smoothstep(bite, bite + 0.02, edge));
	shade = mix(shade, shade * 0.45 + vec3(0.05, 0.04, 0.035), crater);
	shade *= 1.0 - crack * 0.6;

	vec3 mortar = mortar_color.rgb * (0.78 + alley_noise(p * 90.0) * 0.32);
	// Joints sit back from the face: a touch darker under the brick above.
	mortar *= 0.85 + 0.15 * smoothstep(0.0, joint, edge + joint * 0.5);

	vec3 color = in_mortar ? mortar : shade;
	// Soot: a plume rising from the opening, plus general grime.
	float plume = 1.0 - smoothstep(0.3, 1.3, abs(p.x - soot_origin.x) - (p.y - soot_origin.y) * 0.25);
	plume *= smoothstep(soot_origin.y - 0.25, soot_origin.y + 0.3, p.y);
	float grime = alley_noise(p * vec2(2.3, 0.7)) * 0.5 + alley_noise(p * 7.0) * 0.5;
	color *= 1.0 - clamp(plume * 0.55 + grime * 0.3, 0.0, 0.8);

	ALBEDO = color;
	ROUGHNESS = in_mortar ? 0.97 : mix(0.82, 0.95, crater);
	SPECULAR = 0.15;
}
