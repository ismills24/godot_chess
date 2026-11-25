shader_type spatial;
// Dressed stone (kerb, pavement slabs). Where a fragment stands between the camera
// and the board it is dithered away, so the orbiting camera never loses the board.
#include "res://menu/alley_noise.gdshaderinc"

uniform vec4 tint : source_color = vec4(0.26, 0.27, 0.27, 1.0);
uniform vec2 slab_size = vec2(9.0, 9.0);
uniform vec2 board_half = vec2(6.4, 6.4);
varying vec3 world_at;
varying vec3 world_normal;

void vertex() {
	world_at = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	// Where would this fragment's line of sight meet the ground?
	vec3 forward = -INV_VIEW_MATRIX[2].xyz;
	vec2 ground = (world_at + forward * (world_at.y / max(-forward.y, 0.05))).xz;
	vec2 over = abs(ground) - board_half;
	if (world_at.y > 0.05 && max(over.x, over.y) < 0.0) {
		ivec2 pixel = ivec2(FRAGCOORD.xy) % 2;
		if (pixel.x + pixel.y != 0) { discard; }
	}
	// Slab joints on top faces; plain weathered faces elsewhere.
	vec2 face = abs(world_normal.y) > 0.5 ? world_at.xz : vec2(world_at.x + world_at.z, world_at.y);
	vec2 slab = face / slab_size;
	vec2 joint = abs(fract(slab) - 0.5) * slab_size;
	float seam = 1.0 - smoothstep(0.06, 0.12, min(slab_size.x * 0.5 - joint.x, slab_size.y * 0.5 - joint.y));
	float shade = 0.86 + 0.2 * cell_hash(floor(slab)) + 0.12 * (alley_noise(face * 0.7) - 0.5);
	ALBEDO = tint.rgb * shade * (1.0 - 0.55 * seam);
	ROUGHNESS = 0.88;
	SPECULAR = 0.25;
}
