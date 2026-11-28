shader_type spatial;
render_mode cull_disabled;
// Leafy clumps (bushes, pine needles, ivy, palm fronds): dark in the hollows, light where the
// leaves face the sky, broken into rounded leaf-shaped flecks (each leaf its own shade, its
// edge shadowed, a vein down it), and stirring in the wind (stronger the higher above the
// clump's root). The leaves also tilt the light a little so clumps don't read as smooth balls.

#include "res://menu/alley_noise.gdshaderinc"

uniform vec4 dark_color : source_color = vec4(0.07, 0.14, 0.06, 1.0);
uniform vec4 light_color : source_color = vec4(0.35, 0.55, 0.18, 1.0);
uniform float sway = 0.04;
uniform float leaf_size = 0.09;
uniform float elapsed = 0.0;
// Optional dry tips (palm fronds, autumn).
uniform vec4 tip_color : source_color = vec4(0.5, 0.45, 0.2, 1.0);
uniform float tips = 0.0;

varying vec3 world_at;
varying vec3 world_normal;
varying float lift;

void vertex() {
	vec3 root = MODEL_MATRIX[3].xyz;
	vec3 at = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	lift = max(at.y - root.y + 0.5, 0.0);
	float gust = sin(elapsed * 1.3 + root.x * 0.7 + root.z * 0.5) + 0.5 * sin(elapsed * 2.9 + root.z * 1.3 + VERTEX.x * 3.0);
	vec3 push = vec3(gust, 0.0, 0.6 * cos(elapsed * 1.1 + root.x)) * sway * lift;
	VERTEX += (inverse(MODEL_MATRIX) * vec4(push, 0.0)).xyz;
	world_at = at;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

// Leaves as cells: the nearest of a jittered point per cell. Returns (leaf's own random,
// distance to its centre 0..1, distance to the next leaf's edge).
vec3 leaves(vec2 p) {
	vec2 base = floor(p);
	float nearest = 8.0;
	float second = 8.0;
	float which = 0.0;
	for (int j = -1; j <= 1; j++) {
		for (int i = -1; i <= 1; i++) {
			vec2 cell = base + vec2(float(i), float(j));
			vec2 centre = cell + vec2(cell_hash(cell), cell_hash(cell + 31.7)) * 0.8 + 0.1;
			vec2 d = p - centre;
			// Leaves are longer than wide, each turned its own way.
			float turn = cell_hash(cell + 9.1) * 6.283;
			d = mat2(vec2(cos(turn), sin(turn)), vec2(-sin(turn), cos(turn))) * d;
			float dist = length(d * vec2(1.0, 1.7));
			if (dist < nearest) {
				second = nearest;
				nearest = dist;
				which = cell_hash(cell + 3.3);
			} else if (dist < second) {
				second = dist;
			}
		}
	}
	return vec3(which, nearest, second - nearest);
}

void fragment() {
	vec3 w = abs(world_normal);
	vec2 p = (w.y > 0.6 ? world_at.xz : (w.x > w.z ? world_at.zy : world_at.xy)) / leaf_size;
	p += world_at.y * vec2(0.37, 0.21) / leaf_size;
	vec3 cell = leaves(p);
	float leaf = cell.x;
	float clump = alley_noise(world_at.xz * 1.7 + world_at.y * 1.3) * 0.7 + alley_noise(world_at.xz * 5.3 - world_at.y * 3.1) * 0.3;
	// Each leaf lit across its face, darker toward its edges and where leaves overlap.
	float edge = smoothstep(0.0, 0.12, cell.z);
	float light = clamp(world_normal.y * 0.45 + 0.4 + (leaf - 0.5) * 0.5 + (clump - 0.5) * 0.4 - cell.y * 0.18, 0.0, 1.0);
	light *= 0.72 + 0.28 * edge;
	vec3 color = mix(dark_color.rgb, light_color.rgb, light);
	color = mix(color, tip_color.rgb, tips * smoothstep(0.75, 1.0, UV.x) * (0.6 + 0.4 * leaf));
	// Fine flecks fade out at distance.
	float near = 1.0 - smoothstep(0.15, 0.6, length(fwidth(p)));
	color *= mix(1.0, 0.9 + 0.2 * alley_noise(p * 3.7), near);
	vec3 tilt = vec3(cell_hash(vec2(leaf, 1.0)) - 0.5, 0.0, cell_hash(vec2(leaf, 2.0)) - 0.5) * 0.6 * near;
	NORMAL = normalize(NORMAL + (VIEW_MATRIX * vec4(tilt, 0.0)).xyz);
	ALBEDO = color;
	ROUGHNESS = 0.85;
	BACKLIGHT = light_color.rgb * 0.25;
}
