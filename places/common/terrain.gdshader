shader_type spatial;
// Natural ground and rock for the outdoor places, procedural in world space (triplanar, so it
// works on cliffs as well as on the flat): two blotchy base tones, fine speckle, optional
// strata running along the rock, a top layer on up-facing ground (grass, moss, dry sand), a
// low layer below a height (wet sand, damp), and optional wind ripples for sand. Drawn at full
// resolution: every layer is smooth noise (no hard cells), with finer octaves fading in close up.

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

// Each octave turned against the last so value noise's grid never lines up.
const mat2 TURN = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8));

float fbm(vec2 p) {
	float total = alley_noise(p) * 0.5;
	p = TURN * p * 2.13 + 7.1;
	total += alley_noise(p) * 0.26;
	p = TURN * p * 2.07 + 3.3;
	total += alley_noise(p) * 0.13;
	p = TURN * p * 2.11 + 1.9;
	total += alley_noise(p) * 0.07;
	p = TURN * p * 2.03 + 5.7;
	total += alley_noise(p) * 0.04;
	return total;
}

// Grit: soft speckle at two small scales, a few centimetres and a few millimetres.
float grit(vec3 at, vec3 w) {
	vec2 a = at.zy * w.x + at.xz * w.y + at.xy * w.z;
	return alley_noise(TURN * a * 24.0) * 0.6 + alley_noise(TURN * TURN * a * 71.0 + 4.0) * 0.4;
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
	// Grit fades out with distance before it can shimmer.
	float near = 1.0 - smoothstep(0.01, 0.04, length(fwidth(world_at)));
	float grain = grit(world_at, w);
	color *= 1.0 + speckle * (grain - 0.5) * mix(0.3, 0.75, near);
	// The odd darker pebble or pit.
	color *= 1.0 - speckle * 0.25 * smoothstep(0.72, 0.86, alley_noise(TURN * (world_at.xz + world_at.y) * 9.0 + 2.0));
	if (strata > 0.0) {
		float band = sin(world_at.y * 7.0 + tri(p * 2.0, w) * 4.0);
		color *= 1.0 - strata * 0.22 * smoothstep(0.55, 0.95, band);
		color *= 1.0 + strata * 0.1 * smoothstep(0.6, 1.0, -band);
	}
	if (cracks > 0.0) {
		float split = abs(alley_noise(world_at.xz * 1.3 + tri(p * 3.0, w) * 0.8) - 0.5);
		float hairline = abs(alley_noise(TURN * world_at.xz * 4.1 + tri(p * 6.0, w) * 0.6) - 0.5);
		color *= 1.0 - cracks * 0.45 * (1.0 - smoothstep(0.0, 0.018, split));
		color *= 1.0 - cracks * 0.2 * (1.0 - smoothstep(0.0, 0.01, hairline));
	}
	if (ripples > 0.0) {
		float wave = sin(world_at.x * 9.0 + world_at.z * 3.0 + blotch * 6.0);
		float small_wave = sin(world_at.x * 23.0 - world_at.z * 7.0 + blotch * 9.0);
		color *= 1.0 + ripples * (0.08 * wave + 0.03 * small_wave);
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
