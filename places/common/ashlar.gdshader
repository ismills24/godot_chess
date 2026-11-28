shader_type spatial;
// Dressed stone blocks in running bond, procedural in world space on whichever face a box
// shows (walls, piers, copings, flagstones): each block its own shade, edges chipped back
// into the mortar, weather streaks running down the face, and moss that creeps along the
// joints, over the tops and up from the ground. Drawn at full resolution: pits and grain are
// soft noise, so nothing reads as a pixel square.

#include "res://menu/alley_noise.gdshaderinc"

uniform vec4 stone_color : source_color = vec4(0.55, 0.3, 0.22, 1.0);
uniform vec4 stone_color_b : source_color = vec4(0.45, 0.24, 0.18, 1.0);
uniform vec4 mortar_color : source_color = vec4(0.35, 0.3, 0.27, 1.0);
uniform vec4 moss_color : source_color = vec4(0.22, 0.3, 0.12, 1.0);
uniform vec2 block_size = vec2(0.55, 0.3);
uniform float joint = 0.018;
uniform float moss = 0.0;
uniform float streaks = 0.4;
// Moss rises from the ground up to this height.
uniform float damp_line = 0.0;

varying vec3 world_at;
varying vec3 world_normal;

void vertex() {
	world_at = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	vec3 n = abs(world_normal);
	vec2 p;
	bool top = n.y > n.x && n.y > n.z;
	if (top) {
		p = world_at.xz;
	} else if (n.x > n.z) {
		p = vec2(world_at.z, world_at.y);
	} else {
		p = world_at.xy;
	}
	vec2 size = top ? vec2(block_size.x, block_size.x * 0.8) : block_size;
	float row = floor(p.y / size.y);
	vec2 laid = vec2(p.x / size.x + 0.5 * mod(row, 2.0) + 0.17 * cell_hash(vec2(row, 3.0)), p.y / size.y);
	vec2 cell = floor(laid);
	vec2 inside = fract(laid) * size;
	float seed = cell_hash(cell + 17.0);
	float edge = min(min(inside.x, size.x - inside.x), min(inside.y, size.y - inside.y));
	float chip = alley_noise(p * 14.0 + seed * 9.0) * 0.7 + alley_noise(p * 41.0 + seed * 5.0) * 0.3;
	float in_joint = 1.0 - smoothstep(joint * (0.6 + chip * 1.4), joint * (0.6 + chip * 1.4) + 0.008, edge);
	vec3 stone = mix(stone_color.rgb, stone_color_b.rgb, seed);
	stone *= 0.88 + 0.24 * alley_noise(p * 5.0 + seed * 13.0);
	// Fine detail fades out with distance before it can shimmer.
	float near = 1.0 - smoothstep(0.004, 0.02, length(fwidth(p)));
	// Grain in the stone, and soft pits where it has weathered.
	stone *= 0.95 + 0.1 * alley_noise(vec2(p.x * 37.0 + p.y * 21.0, p.y * 37.0 - p.x * 21.0) + seed * 31.0);
	stone *= 1.0 + near * 0.06 * (alley_noise(vec2(p.x * 97.0 - p.y * 51.0, p.y * 97.0 + p.x * 51.0)) - 0.5);
	float pit = alley_noise(vec2(p.x * 21.0 + p.y * 13.0, p.y * 21.0 - p.x * 13.0) + seed * 7.0);
	stone *= 1.0 - 0.14 * smoothstep(0.72, 0.88, pit);
	// Faint diagonal tooling from the mason's chisel.
	stone *= 1.0 - near * 0.035 * smoothstep(0.3, 1.0, sin((inside.x + inside.y) * 140.0 + chip * 6.0));
	// Edges catch the light a little, the joint shadows the stone beside it.
	stone *= 1.0 - 0.18 * (1.0 - smoothstep(0.0, 0.05, edge - joint));
	// Rain streaks down the face from the copings.
	float streak = alley_noise(vec2(world_at.x * 2.3 + world_at.z * 2.3, world_at.y * 0.25));
	stone *= 1.0 - streaks * 0.35 * smoothstep(0.55, 0.85, streak) * (top ? 0.0 : 1.0);
	vec3 color = mix(stone, mortar_color.rgb * (0.8 + 0.4 * chip), in_joint);
	if (moss > 0.0) {
		float growth = alley_noise(world_at.xz * 1.1 + world_at.y * 0.9) * 0.55 + alley_noise(p * 4.0) * 0.33
			+ alley_noise(p * 17.0 + 3.0) * 0.12;
		float where = in_joint * 0.5 + (top ? 0.55 : 0.0) + (1.0 - smoothstep(damp_line - 0.2, damp_line + 0.6, world_at.y)) * 0.6;
		float cover = smoothstep(1.0 - moss, 1.15 - moss, growth * where + growth * 0.25);
		color = mix(color, moss_color.rgb * (0.7 + 0.45 * chip + 0.2 * alley_noise(p * 60.0)), cover);
	}
	ALBEDO = color;
	ROUGHNESS = 0.9;
}
