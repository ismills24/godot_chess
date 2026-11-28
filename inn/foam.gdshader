shader_type spatial;
// The head on a mug of ale, for a CylinderMesh in its own space: a creamy surface packed with
// small bubbles of mixed sizes, a few big ones, darker little gaps between them; the side
// shows the same froth.

#include "res://menu/alley_noise.gdshaderinc"

uniform vec4 foam_color : source_color = vec4(0.847, 0.784, 0.627, 1.0);
// Average bubble size in metres.
uniform float bubble = 0.0024;

varying vec3 local_at;
varying vec3 local_normal;
varying float seed;

void vertex() {
	local_at = VERTEX;
	local_normal = NORMAL;
	vec3 origin = MODEL_MATRIX[3].xyz;
	seed = cell_hash(origin.xz * 13.1 + 0.5) * 50.0;
}

// Distance to the nearest bubble's edge, as a fraction of its radius (0 centre, 1 rim).
float bubbles(vec2 p, out float id) {
	vec2 cell = floor(p);
	float best = 8.0;
	id = 0.0;
	for (int y = -1; y <= 1; y++) {
		for (int x = -1; x <= 1; x++) {
			vec2 c = cell + vec2(float(x), float(y));
			vec2 centre = c + 0.2 + 0.6 * vec2(cell_hash(c + 2.7), cell_hash(c * 1.7 + 8.1));
			float r = 0.35 + 0.35 * cell_hash(c * 2.9 + 4.4);
			float d = length(p - centre) / r;
			if (d < best) {
				best = d;
				id = cell_hash(c + 19.0);
			}
		}
	}
	return best;
}

void fragment() {
	vec2 p = local_normal.y > 0.5 ? local_at.xz
		: vec2(atan(local_at.z, local_at.x) * length(local_at.xz), local_at.y);
	p += seed;
	float id_small;
	float id_big;
	float small = bubbles(p / bubble, id_small);
	float big = bubbles(p / (bubble * 2.6) + 7.3, id_big);
	// Bubble rims and the gaps between them are a little darker; centres catch the light.
	float froth = smoothstep(0.55, 1.0, min(small, 1.4));
	float big_rim = smoothstep(0.75, 0.95, big) * (1.0 - smoothstep(0.95, 1.1, big)) * step(0.6, id_big);
	float tone = 1.0 - froth * 0.12 - big_rim * 0.07 + 0.05 * (id_small - 0.5);
	tone *= 0.92 + 0.12 * alley_noise(p * 300.0);
	ALBEDO = foam_color.rgb * tone;
	ROUGHNESS = mix(0.45, 0.85, froth);
	SPECULAR = 0.4;
}
