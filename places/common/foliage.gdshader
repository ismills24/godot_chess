shader_type spatial;
render_mode cull_disabled;
// Leafy clumps (bushes, pine needles, ivy, palm fronds): dark in the hollows, light where the
// leaves face the sky, broken into leaf-sized flecks that survive the pixel finish, and
// stirring in the wind (stronger the higher above the clump's root).

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

void fragment() {
	vec3 cell = floor(world_at / leaf_size);
	float leaf = cell_hash(cell.xz + cell.y * 5.3);
	float clump = alley_noise(world_at.xz * 1.7 + world_at.y * 1.3);
	float light = clamp(world_normal.y * 0.45 + 0.4 + (leaf - 0.5) * 0.55 + (clump - 0.5) * 0.4, 0.0, 1.0);
	vec3 color = mix(dark_color.rgb, light_color.rgb, light);
	color = mix(color, tip_color.rgb, tips * smoothstep(0.75, 1.0, UV.x) * (0.6 + 0.4 * leaf));
	ALBEDO = color;
	ROUGHNESS = 0.85;
	BACKLIGHT = light_color.rgb * 0.25;
}
