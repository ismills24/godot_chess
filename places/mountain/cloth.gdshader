shader_type spatial;
// Coarse homespun wool (the hermit's bundle, your folded blanket): a fine over-under weave,
// a few darker stripes, and wear and fluff across it.

uniform vec4 base_color : source_color = vec4(0.48, 0.23, 0.16, 1.0);
uniform vec4 stripe_color : source_color = vec4(0.3, 0.14, 0.1, 1.0);
uniform float weave = 260.0;
uniform float stripes = 1.0;

varying vec3 local_at;
varying vec3 local_normal;

float hash2(vec2 p) {
	vec3 q = fract(vec3(p.xyx) * vec3(0.1031, 0.103, 0.0973));
	q += dot(q, q.yzx + 33.33);
	return fract((q.x + q.y) * q.z);
}

float noise2(vec2 x) {
	vec2 i = floor(x);
	vec2 f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash2(i), hash2(i + vec2(1, 0)), f.x), mix(hash2(i + vec2(0, 1)), hash2(i + vec2(1, 1)), f.x), f.y);
}

void vertex() {
	// Object space scaled to metres, so the weave stays the same size on any piece.
	local_at = (MODEL_MATRIX * vec4(VERTEX, 0.0)).xyz;
	local_normal = NORMAL;
}

void fragment() {
	vec3 w = abs(normalize(local_normal));
	vec2 uv = w.y > max(w.x, w.z) ? local_at.xz : (w.x > w.z ? local_at.zy : local_at.xy);
	float fw = max(length(fwidth(uv)) * weave, 1e-4);
	vec2 g = uv * weave;
	vec2 cell = floor(g);
	vec2 f = fract(g) - 0.5;
	float over = mod(cell.x + cell.y, 2.0);
	// Each thread bulges across its cell.
	float thread = over > 0.5 ? cos(f.x * PI) : cos(f.y * PI);
	float keep = 1.0 - smoothstep(0.4, 1.0, fw);
	float h = mix(0.6, thread, keep);
	float wear = noise2(uv * 18.0);
	float band = smoothstep(0.45, 0.5, abs(fract(uv.x * 9.0 + noise2(uv * 4.0) * 0.1) - 0.5));
	vec3 color = mix(base_color.rgb, stripe_color.rgb, band * stripes);
	color *= 0.8 + 0.3 * h;
	color *= 0.9 + 0.2 * wear;
	color *= 0.93 + 0.14 * hash2(cell) * keep;
	ALBEDO = color;
	ROUGHNESS = 0.97;
	SPECULAR = 0.2;
}
