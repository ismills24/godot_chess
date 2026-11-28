shader_type spatial;
// Street paving seen up close: small units in running bond (brick pavers on the pavements,
// granite setts in the road), procedural in world space on the top face. Each unit has its own
// tone, a rounded arris that catches the sun, fine grain and wear, sand and grime packed into
// the joints, and a faint normal relief so the joints read at full resolution. Side faces get
// the same tone without the joints (they're slab edges, barely seen).

#include "res://menu/alley_noise.gdshaderinc"

uniform vec4 stone_color : source_color = vec4(0.54, 0.45, 0.4, 1.0);
uniform vec4 stone_color_b : source_color = vec4(0.43, 0.35, 0.31, 1.0);
uniform vec4 joint_color : source_color = vec4(0.29, 0.24, 0.22, 1.0);
// Unit length along x, width along z, in metres.
uniform vec2 unit_size = vec2(0.22, 0.11);
uniform float joint = 0.006;
// How far each unit's tone strays from the blend of the two colours.
uniform float tone_spread = 1.0;
// 0 = a regular half-bond, 1 = setts laid with random offsets row by row.
uniform float irregular = 0.0;
// How domed each unit is (setts are proud, pavers nearly flat).
uniform float dome = 0.3;
uniform float roughness_value = 0.88;

varying vec3 world_at;
varying vec3 world_normal;

void vertex() {
	world_at = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

float fbm(vec2 p) {
	return alley_noise(p) * 0.5 + alley_noise(p * 2.07 + 5.3) * 0.28 + alley_noise(p * 4.31 + 1.7) * 0.14
		+ alley_noise(p * 9.1 + 8.2) * 0.08;
}

void fragment() {
	vec2 p = world_at.xz;
	float row = floor(p.y / unit_size.y);
	float shift = mix(0.5 * mod(row, 2.0), cell_hash(vec2(row, 7.0)), irregular);
	vec2 laid = vec2(p.x / unit_size.x + shift, p.y / unit_size.y);
	vec2 cell = floor(laid);
	vec2 inside = (fract(laid) - 0.5) * unit_size;
	float seed = cell_hash(cell + 31.0);
	float seed2 = cell_hash(cell + 97.0);
	// Setts are a touch irregular in plan: wobble the edge distance with noise.
	float wobble = (alley_noise(p * 38.0 + seed * 11.0) - 0.5) * joint * (0.8 + irregular * 1.6);
	vec2 half_size = unit_size * 0.5 - joint;
	vec2 to_edge = half_size - abs(inside);
	float edge = min(to_edge.x, to_edge.y) + wobble;
	float arris = 0.012 + irregular * 0.01;
	float face = smoothstep(-0.0015, 0.0015, edge);
	// Height across the unit: flat top, rounded edge, a slight dome.
	float round_off = smoothstep(0.0, arris, edge);
	vec2 uv_in = inside / max(half_size, vec2(0.001));
	float height = round_off * (1.0 - dome * 0.25 * dot(uv_in, uv_in));

	float blend = clamp(0.5 + (seed - 0.5) * tone_spread, 0.0, 1.0);
	vec3 stone = mix(stone_color.rgb, stone_color_b.rgb, blend);
	stone *= 0.93 + 0.14 * seed2;
	// Grain: blotches across the unit and fine sparkle.
	float grain = fbm(p * 22.0 + seed * 17.0);
	stone *= 0.86 + 0.28 * grain;
	float fleck = cell_hash(floor(p * 260.0));
	stone *= 1.0 + 0.10 * (fleck - 0.5);
	// Worn, slightly polished middles; darker grimy edges.
	stone *= 0.9 + 0.1 * round_off;
	// Stains and tyre/foot wear in broad patches across many units.
	float stain = fbm(p * 1.3 + 3.0);
	stone *= 1.0 - 0.16 * smoothstep(0.55, 0.85, stain);
	float joint_grit = alley_noise(p * 90.0);
	vec3 joints = joint_color.rgb * (0.75 + 0.5 * joint_grit);

	vec3 n = abs(world_normal);
	bool top = n.y > n.x && n.y > n.z;
	vec3 color = top ? mix(joints, stone, face) : stone * 0.9;
	ALBEDO = color;
	ROUGHNESS = top ? mix(0.98, roughness_value - 0.1 * round_off * grain, face) : roughness_value;
	SPECULAR = 0.35;
	if (top) {
		// Bump from the height field by screen-space derivatives.
		float bump = height * 0.006 + grain * 0.0012 + fleck * 0.0002;
		vec3 dpdx = dFdx(world_at);
		vec3 dpdy = dFdy(world_at);
		float dhx = dFdx(bump);
		float dhy = dFdy(bump);
		vec3 r1 = cross(dpdy, world_normal);
		vec3 r2 = cross(world_normal, dpdx);
		float det = dot(dpdx, r1);
		vec3 surf_grad = sign(det) * (dhx * r1 + dhy * r2);
		vec3 bumped = normalize(abs(det) * world_normal - surf_grad * 1.0);
		NORMAL = normalize((VIEW_MATRIX * vec4(bumped, 0.0)).xyz);
	}
}
