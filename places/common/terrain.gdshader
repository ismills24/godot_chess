shader_type spatial;
// Natural ground and rock for the outdoor places, procedural in world space (triplanar, so it
// works on cliffs as well as on the flat): two blotchy base tones, fine speckle, optional
// strata running along the rock, a top layer on up-facing ground (grass, moss, dry sand), a
// low layer below a height (wet sand, damp), and optional wind ripples for sand. Features are
// kept chunky enough to survive the pixel finish.

#include "res://menu/alley_noise.gdshaderinc"

uniform vec4 color_a : source_color = vec4(0.5, 0.48, 0.44, 1.0);
uniform vec4 color_b : source_color = vec4(0.38, 0.36, 0.33, 1.0);
uniform vec4 top_color : source_color = vec4(0.3, 0.4, 0.2, 1.0);
uniform float top_amount = 0.0;
uniform vec4 low_color : source_color = vec4(0.3, 0.26, 0.2, 1.0);
uniform float low_height = -1000.0;
uniform float scale = 0.6;
uniform float strata = 0.0;
uniform float speckle = 0.25;
uniform float ripples = 0.0;
uniform float cracks = 0.0;
uniform float roughness_value = 0.92;

varying vec3 world_at;
varying vec3 world_normal;

void vertex() {
	world_at = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

float fbm(vec2 p) {
	return alley_noise(p) * 0.55 + alley_noise(p * 2.13 + 7.1) * 0.3 + alley_noise(p * 4.7 + 3.3) * 0.15;
}

float tri(vec3 p, vec3 w) {
	return fbm(p.zy) * w.x + fbm(p.xz) * w.y + fbm(p.xy) * w.z;
}

void fragment() {
	vec3 w = pow(abs(world_normal), vec3(4.0));
	w /= w.x + w.y + w.z;
	vec3 p = world_at * scale;
	float blotch = tri(p, w);
	vec3 color = mix(color_a.rgb, color_b.rgb, smoothstep(0.35, 0.68, blotch));
	// Fine grains, a hand-span across.
	vec3 grain_at = floor(world_at * 18.0);
	float grain = cell_hash(grain_at.xz + grain_at.y * 3.7);
	color *= 1.0 + speckle * (grain - 0.5) * 0.5;
	if (strata > 0.0) {
		float band = sin(world_at.y * 7.0 + tri(p * 2.0, w) * 4.0);
		color *= 1.0 - strata * 0.22 * smoothstep(0.55, 0.95, band);
		color *= 1.0 + strata * 0.1 * smoothstep(0.6, 1.0, -band);
	}
	if (cracks > 0.0) {
		float split = abs(alley_noise(world_at.xz * 1.3 + tri(p * 3.0, w) * 0.8) - 0.5);
		color *= 1.0 - cracks * 0.45 * (1.0 - smoothstep(0.0, 0.025, split));
	}
	if (ripples > 0.0) {
		float wave = sin(world_at.x * 9.0 + world_at.z * 3.0 + blotch * 6.0);
		color *= 1.0 + ripples * 0.08 * wave;
	}
	if (top_amount > 0.0) {
		float cover = world_normal.y + (tri(p * 1.7 + 11.0, w) - 0.5) * 0.9;
		float t = smoothstep(1.0 - top_amount, 1.0 - top_amount + 0.15, cover);
		vec3 top = top_color.rgb * (0.85 + 0.3 * tri(p * 3.0 + 5.0, w));
		color = mix(color, top, t);
	}
	float low = 1.0 - smoothstep(low_height - 0.05, low_height + 0.12 + 0.1 * blotch, world_at.y);
	color = mix(color, low_color.rgb, low);
	ALBEDO = color;
	ROUGHNESS = mix(roughness_value, 0.35, low * 0.8);
	SPECULAR = 0.3 + low * 0.3;
}
