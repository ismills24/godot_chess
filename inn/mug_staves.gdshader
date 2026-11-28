shader_type spatial;
// The inn's wooden mugs, for a CylinderMesh in its own space (centred, y up): vertical
// staves, each its own tone with long grain, dark seams between them, each stave a touch
// convex so the light catches it; on the top cap a rim of end grain round a dark wet inside.
// Detail is sized for the full-resolution play view, where the mugs sit right by the board.

#include "res://menu/alley_noise.gdshaderinc"

uniform vec4 wood_color : source_color = vec4(0.353, 0.227, 0.133, 1.0);
uniform float staves = 13.0;
// The top cap's radius and the wall thickness, in the mesh's own metres.
uniform float top_radius = 0.0405;
uniform float wall = 0.005;
// Height of the mesh (the top cap sits at +height/2).
uniform float height = 0.13;

varying vec3 local_at;
varying vec3 local_normal;
varying float seed;

void vertex() {
	local_at = VERTEX;
	local_normal = NORMAL;
	vec3 origin = MODEL_MATRIX[3].xyz;
	seed = cell_hash(origin.xz * 11.3 + origin.y * 5.7 + 0.37);
}

void fragment() {
	float a = atan(local_at.z, local_at.x) / TAU + 0.5 + seed;
	float s = a * staves;
	float id = floor(s);
	float f = fract(s);
	float tone_jitter = cell_hash(vec2(id, seed * 37.0 + 3.0));

	if (local_normal.y > 0.5) {
		// Top cap: end grain rim, then the stained inside.
		float r = length(local_at.xz);
		float inside = 1.0 - smoothstep(top_radius - wall - 0.0008, top_radius - wall, r);
		float rim_seam = 1.0 - smoothstep(0.0, 0.035, min(f, 1.0 - f));
		vec3 rim = wood_color.rgb * (0.75 + 0.3 * tone_jitter) * (1.0 - rim_seam * 0.6)
			* (0.9 + 0.2 * alley_noise(vec2(a * 300.0, r * 900.0)));
		float depth = r / max(top_radius - wall, 0.001);
		vec3 hollow = wood_color.rgb * mix(0.08, 0.22, depth * depth);
		ALBEDO = mix(rim, hollow, inside);
		ROUGHNESS = mix(0.7, 0.45, inside);
		SPECULAR = 0.3;
	} else {
		float y = local_at.y;
		// Long grain up each stave, with a finer fibre on top.
		float grain = alley_noise(vec2(f * 9.0 + id * 17.0, y * 14.0 + id * 3.0));
		float fibre = alley_noise(vec2(f * 46.0 + id * 31.0, y * 120.0 + seed * 40.0));
		float streak = sin(f * 22.0 + grain * 6.0 + id * 2.0) * 0.5 + 0.5;
		float seam = 1.0 - smoothstep(0.0, 0.05, min(f, 1.0 - f));
		// Worn paler round the lip and the base where hands and tables rub.
		float lip = smoothstep(height * 0.32, height * 0.5, abs(y));
		float tone = (0.78 + 0.34 * tone_jitter) * (0.84 + 0.16 * streak + 0.18 * (fibre - 0.5) + 0.12 * (grain - 0.5));
		tone *= 1.0 + lip * 0.18;
		tone *= 1.0 - seam * 0.65;
		// Grime gathered in streaks down some staves.
		float grime = smoothstep(0.6, 0.9, alley_noise(vec2(id * 5.3 + seed * 9.0, y * 6.0)));
		tone *= 1.0 - grime * 0.25;
		ALBEDO = wood_color.rgb * tone;
		ROUGHNESS = clamp(0.68 + seam * 0.25 - lip * 0.12 + grime * 0.1, 0.35, 1.0);
		SPECULAR = 0.35;
		// Each stave bowed a little, and the seams sunk in.
		float bow = (f - 0.5) * 0.55;
		NORMAL_MAP = vec3(clamp(0.5 + bow * 0.5, 0.0, 1.0), 0.5 + (fibre - 0.5) * 0.08, 1.0);
		NORMAL_MAP_DEPTH = 1.0;
	}
}
