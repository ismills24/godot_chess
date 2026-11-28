shader_type spatial;
render_mode cull_disabled;
// Rainwater frozen in the rock's hollows: dark and glassy where it's deep, milky toward the
// rim, with white frost feathers, trapped bubbles and a few hairline cracks.

uniform vec4 deep_color : source_color = vec4(0.25, 0.245, 0.235, 1.0);
uniform vec4 milk_color : source_color = vec4(0.56, 0.57, 0.56, 1.0);
uniform vec4 frost_color : source_color = vec4(0.76, 0.78, 0.79, 1.0);

varying vec3 world_at;
varying vec2 local_at;

float hash3(vec3 p) {
	p = fract(p * vec3(0.1031, 0.1030, 0.0973));
	p += dot(p, p.yxz + 33.33);
	return fract((p.x + p.y) * p.z);
}

float noise2(vec2 x) {
	vec2 i = floor(x);
	vec2 f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash3(vec3(i, 0)), hash3(vec3(i + vec2(1, 0), 0)), f.x),
		mix(hash3(vec3(i + vec2(0, 1), 0)), hash3(vec3(i + vec2(1, 1), 0)), f.x), f.y);
}

float fbm(vec2 p, float fw) {
	float sum = 0.0;
	float amp = 0.5;
	for (int k = 0; k < 4; k++) {
		sum += amp * mix(0.5, noise2(p), 1.0 - smoothstep(0.3, 0.8, fw));
		p = p * 2.1 + 3.7;
		fw *= 2.1;
		amp *= 0.5;
	}
	return sum / 0.9375;
}

void vertex() {
	world_at = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	local_at = VERTEX.xz;
}

void fragment() {
	float fw = max(length(fwidth(world_at)), 1e-5);
	// A lobed, ragged outline, all inside the hollow's flat floor.
	float lobes = fbm(world_at.xz * 5.0 + 13.0, fw * 5.0);
	float edge = length(local_at) * (1.0 + 0.32 * lobes + 0.05 * fbm(world_at.xz * 40.0, fw * 40.0));
	if (edge > 0.86) {
		discard;
	}
	float rim = smoothstep(0.3, 0.86, edge);
	float wobble = fbm(world_at.xz * 6.0, fw * 6.0);
	vec3 color = mix(deep_color.rgb, milk_color.rgb, clamp(rim * 0.8 + (wobble - 0.5) * 0.6, 0.0, 1.0));
	// Frost feathers creeping in from the rim.
	float feather = fbm(world_at.xz * 22.0 + wobble * 2.0, fw * 22.0);
	float frost = smoothstep(0.55, 0.7, feather + rim * 0.35);
	frost = max(frost, smoothstep(0.8, 0.86, edge));
	color = mix(color, frost_color.rgb, frost * 0.8);
	// Hairline cracks.
	float split = abs(fbm(world_at.xz * 3.0 + 9.0, fw * 3.0) - 0.5);
	float crack = 1.0 - smoothstep(0.0, 0.01 + fw * 0.6, split);
	color = mix(color, frost_color.rgb, crack * 0.6);
	// Tiny trapped bubbles.
	vec2 cell = floor(world_at.xz * 60.0);
	vec2 f = fract(world_at.xz * 60.0) - 0.5;
	float bubble = step(0.93, hash3(vec3(cell, 5.0))) * (1.0 - smoothstep(0.12, 0.2, length(f)));
	color = mix(color, frost_color.rgb, bubble * 0.5 * (1.0 - smoothstep(0.0005, 0.002, fw)));
	ALBEDO = color;
	ROUGHNESS = mix(0.32, 0.7, max(frost, crack));
	SPECULAR = 0.28;
}
