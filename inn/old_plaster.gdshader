shader_type spatial;
// Tired lime plaster for the inn walls, in world space on whichever wall a face belongs to:
// blotchy and uneven, soot and damp running down from the top, hairline cracks, and here and
// there a patch fallen away to show the dark lath behind.

#include "res://menu/alley_noise.gdshaderinc"

uniform vec4 plaster_color : source_color = vec4(0.23, 0.2, 0.17, 1.0);
uniform vec4 lath_color : source_color = vec4(0.09, 0.06, 0.04, 1.0);
// Height the soot runs down from.
uniform float top = 2.4;

varying vec3 world_at;
varying vec3 world_normal;

void vertex() {
	world_at = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	// Wall-plane coordinates: along the wall (s) and up (y).
	float s = abs(world_normal.x) > 0.5 ? world_at.z : world_at.x;
	vec2 p = vec2(s, world_at.y);

	float blotch = alley_noise(p * 1.3) * 0.6 + alley_noise(p * 4.1) * 0.4;
	float trowel = alley_noise(p * vec2(9.0, 3.0)) - 0.5;
	// Soot and damp in streaks running down from the top.
	float streak = alley_noise(vec2(s * 3.1, world_at.y * 0.35));
	float soot = smoothstep(top - 1.6, top, world_at.y) * (0.45 + 0.55 * streak);
	float damp = (1.0 - smoothstep(0.0, 0.7, world_at.y)) * (0.5 + 0.5 * streak);
	// Hairline cracks wandering across the wall.
	float crack_field = alley_noise(p * 2.6 + vec2(4.0, 1.0));
	float crack = (1.0 - smoothstep(0.0, 0.012, abs(crack_field - 0.5)))
		* smoothstep(0.45, 0.7, alley_noise(p * 0.9 + 7.0));
	// A few holes where plaster has dropped off: dark lath slats behind a crumbly rim.
	float hole_field = alley_noise(p * 1.1 + 13.0) * 0.7 + alley_noise(p * 6.0) * 0.3;
	float hole = smoothstep(0.74, 0.77, hole_field);
	float rim = smoothstep(0.7, 0.74, hole_field) * (1.0 - hole);
	float slats = step(0.5, fract(world_at.y * 22.0));

	vec3 color = plaster_color.rgb * (0.82 + blotch * 0.3 + trowel * 0.08);
	color *= 1.0 - soot * 0.55;
	color *= 1.0 - damp * 0.3;
	color *= 1.0 - crack * 0.55;
	color *= 1.0 - rim * 0.3;
	color = mix(color, lath_color.rgb * mix(0.5, 1.0, slats), hole);

	ALBEDO = color;
	ROUGHNESS = 0.95;
	SPECULAR = 0.2;
}
