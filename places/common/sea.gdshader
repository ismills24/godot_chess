shader_type spatial;
// The sea off the beach: turquoise over the sand in the shallows deepening to blue further out,
// with rollers that come in as lines of foam, break and wash up the sand. The shore is the
// line x = shore_x + shore_wave * sin(z * shore_freq); "out" is +X from it.

#include "res://menu/alley_noise.gdshaderinc"

uniform vec4 shallow_color : source_color = vec4(0.25, 0.8, 0.78, 1.0);
uniform vec4 mid_color : source_color = vec4(0.05, 0.55, 0.65, 1.0);
uniform vec4 deep_color : source_color = vec4(0.03, 0.22, 0.45, 1.0);
uniform vec4 foam_color : source_color = vec4(0.95, 0.97, 0.98, 1.0);
uniform float shore_x = 3.0;
uniform float shore_wave = 0.8;
uniform float shore_freq = 0.25;
uniform float elapsed = 0.0;
uniform float swell_period = 7.0;

varying vec3 world_at;
varying float out_dist;

// Noise turned off the grid (value noise alone has square contours), in octaves.
const mat2 TURN = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8));

float wavy(vec2 p) {
	return alley_noise(TURN * p) * 0.55 + alley_noise(TURN * TURN * p * 2.1 + 5.2) * 0.3 + alley_noise(TURN * p * 4.3 + 1.7) * 0.15;
}

// Small wind ripples on the surface, for the light to glint off.
float chop(vec2 p, float t) {
	return alley_noise(TURN * p * 3.0 + vec2(t * 0.6, t * 0.2)) * 0.5
		+ alley_noise(TURN * TURN * p * 7.0 - vec2(t * 0.9, -t * 0.4)) * 0.3
		+ alley_noise(TURN * p * 17.0 + vec2(t * 1.3, t * 0.7)) * 0.2;
}

float shore(float z) {
	return shore_x + shore_wave * sin(z * shore_freq) + 0.4 * sin(z * 0.61 + 1.3);
}

void vertex() {
	world_at = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	out_dist = world_at.x - shore(world_at.z);
	// Gentle swell, flattening in toward the beach.
	float swell = sin(out_dist * 0.35 - elapsed * 0.9 + world_at.z * 0.05) * 0.06 * smoothstep(1.0, 25.0, out_dist);
	VERTEX.y += swell;
}

void fragment() {
	float x = world_at.x - shore(world_at.z);
	float ripple = wavy(world_at.xz * vec2(0.9, 0.35) + vec2(elapsed * 0.25, 0.0)) * 0.6
		+ wavy(world_at.xz * vec2(2.6, 1.1) - vec2(elapsed * 0.4, elapsed * 0.1)) * 0.4;
	float lace = wavy(world_at.xz * 6.0 + vec2(elapsed * 0.3, 0.0));
	vec3 color = mix(shallow_color.rgb, mid_color.rgb, smoothstep(1.0, 14.0, x + ripple * 3.0));
	color = mix(color, deep_color.rgb, smoothstep(14.0, 120.0, x));
	// Rollers: each comes in from out at sea, crests as a white line and breaks near the beach.
	float foam = 0.0;
	for (int k = 0; k < 3; k++) {
		float phase = fract(elapsed / swell_period + float(k) / 3.0);
		float crest = mix(28.0, 1.0, phase);
		float wobble = (alley_noise(vec2(world_at.z * 0.3, float(k) * 7.0 + floor(elapsed / swell_period))) - 0.5) * 3.0;
		float d = x - crest - wobble;
		float line = 1.0 - smoothstep(0.0, mix(0.4, 1.6, phase), abs(d + 0.3));
		float behind = smoothstep(0.0, 2.5, d) * (1.0 - smoothstep(2.5, 6.0, d)) * phase;
		float broken = smoothstep(0.3, 0.42, ripple + 0.3 * phase + (lace - 0.5) * 0.2);
		foam = max(foam, line * mix(0.35, 1.0, phase) * broken + behind * 0.35 * smoothstep(0.5, 0.6, ripple + (lace - 0.5) * 0.25));
	}
	// The wash on the shallows: lacy foam right at the edge.
	float edge = 1.0 - smoothstep(0.0, 1.4, x);
	foam = max(foam, edge * smoothstep(0.38, 0.46, ripple + 0.25 * sin(elapsed * 1.1 + world_at.z) + (lace - 0.5) * 0.3));
	color = mix(color, foam_color.rgb, clamp(foam, 0.0, 1.0));
	ALBEDO = color;
	// Ripples tilt the surface; they fade out far away, where they'd only shimmer.
	float near = 1.0 - smoothstep(0.05, 0.4, length(fwidth(world_at.xz)));
	vec2 q = world_at.xz;
	float e = 0.02;
	float c0 = chop(q, elapsed);
	vec2 slope = vec2(chop(q + vec2(e, 0.0), elapsed) - c0, chop(q + vec2(0.0, e), elapsed) - c0) / e;
	vec3 bent = normalize(vec3(-slope.x * 0.06 * near, 1.0, -slope.y * 0.06 * near));
	NORMAL = normalize((VIEW_MATRIX * vec4(bent, 0.0)).xyz);
	ROUGHNESS = mix(0.08, 0.6, foam);
	SPECULAR = 0.6;
	ALPHA = smoothstep(-0.6, 0.6, x + 0.4 * sin(elapsed * 0.8 + world_at.z * 0.4));
}
