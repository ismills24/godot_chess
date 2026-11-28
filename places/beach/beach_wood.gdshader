shader_type spatial;
// Sun-bleached, salt-worn planks, worked out per pixel so they stay crisp at any distance.
// The grain runs along each mesh's local X; sizes are in metres (use a mesh sized in metres,
// not a scaled unit cube). Each plank picks its own log, tone and wear from where it sits.

uniform vec3 light_color : source_color = vec3(0.66, 0.50, 0.35);
uniform vec3 mid_color : source_color = vec3(0.44, 0.31, 0.21);
uniform vec3 dark_color : source_color = vec3(0.24, 0.16, 0.10);
uniform vec3 bleach_color : source_color = vec3(0.56, 0.52, 0.46);
uniform float bleach : hint_range(0.0, 1.0) = 0.45;
uniform float ring_density = 62.0;
uniform float bump = 0.9;
uniform float roughness_base : hint_range(0.0, 1.0) = 0.85;
// Logs and poles: grain along local Y instead of X.
uniform bool grain_along_y = false;

varying vec3 local_pos;
varying vec3 local_normal;
varying vec3 seed;

float hash13(vec3 p) {
	p = fract(p * 0.1031);
	p += dot(p, p.zyx + 31.32);
	return fract((p.x + p.y) * p.z);
}

float noise3(vec3 p) {
	vec3 i = floor(p);
	vec3 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	float a = hash13(i);
	float b = hash13(i + vec3(1, 0, 0));
	float c = hash13(i + vec3(0, 1, 0));
	float d = hash13(i + vec3(1, 1, 0));
	float e = hash13(i + vec3(0, 0, 1));
	float g = hash13(i + vec3(1, 0, 1));
	float h = hash13(i + vec3(0, 1, 1));
	float k = hash13(i + vec3(1, 1, 1));
	return mix(mix(mix(a, b, f.x), mix(c, d, f.x), f.y), mix(mix(e, g, f.x), mix(h, k, f.x), f.y), f.z);
}

float fbm(vec3 p) {
	float sum = 0.0;
	float amp = 0.5;
	for (int i = 0; i < 4; i++) {
		sum += amp * noise3(p);
		p = p * 2.03 + vec3(1.7, 9.2, 3.1);
		amp *= 0.5;
	}
	return sum;
}

void vertex() {
	local_pos = grain_along_y ? VERTEX.yxz : VERTEX;
	local_normal = grain_along_y ? NORMAL.yxz : NORMAL;
	seed = MODEL_MATRIX[3].xyz * 7.31 + vec3(MODEL_MATRIX[0].x, MODEL_MATRIX[2].z, MODEL_MATRIX[1].y);
}

// Returns height (for the bump) in x, ring darkness in y, fibre streak in z.
vec3 wood(vec3 p, float s) {
	// A log axis running along X, off to one side of this plank, slightly wavy.
	vec2 axis = vec2(hash13(seed) - 0.5, hash13(seed + 3.1) - 0.5) * 0.6 + vec2(0.0, -0.18);
	vec3 warp = vec3(0.0, fbm(p * vec3(1.2, 6.0, 6.0) + s) - 0.5, fbm(p * vec3(1.2, 6.0, 6.0) + s + 5.0) - 0.5);
	vec2 yz = p.yz + warp.yz * 0.05 + vec2(0.0, sin(p.x * 3.0 + s * 6.0) * 0.012);
	float r = length(yz - axis) * ring_density * (0.85 + 0.3 * hash13(seed + 7.7));
	float ring = fract(r);
	// Weathering wears the soft early wood away, leaving the hard late-wood rings raised.
	float late = smoothstep(0.55, 0.85, ring) * (1.0 - smoothstep(0.92, 1.0, ring));
	// Fine fibres along the grain.
	float fibre = noise3(p * vec3(6.0, 380.0, 380.0) + s * 13.0);
	float fibre2 = noise3(p * vec3(14.0, 900.0, 900.0) + s * 29.0);
	float streak = fbm(p * vec3(2.0, 60.0, 60.0) + s * 3.0);
	float height = late * 0.6 + fibre * 0.25 + fibre2 * 0.12 - streak * 0.2;
	return vec3(height, late, streak * 0.6 + fibre * 0.25 + fibre2 * 0.15);
}

void fragment() {
	float s = hash13(seed) * 10.0;
	vec3 p = local_pos;
	vec3 w = wood(p, s);
	// Checks: thin splits running along the grain, a few per plank.
	float check_line = abs(fract((p.z + p.y) * 9.0 + fbm(p * vec3(3.0, 0.0, 0.0) + s) * 0.6 + s) - 0.5);
	float check_on = smoothstep(0.62, 0.72, noise3(vec3(p.x * 2.5, s, s * 2.0)));
	float check = (1.0 - smoothstep(0.0, 0.012, check_line)) * check_on;
	// Knots, here and there.
	vec3 knot_at = vec3((hash13(seed + 11.0) - 0.5) * 0.6, 0.0, (hash13(seed + 13.0) - 0.5) * 0.12);
	vec2 kd = (p.xz - knot_at.xz) * vec2(1.0, 2.2);
	float knot_r = length(kd) * 60.0;
	float knot = (1.0 - smoothstep(0.6, 1.4, knot_r)) * step(0.45, hash13(seed + 17.0));
	float knot_rings = (1.0 - smoothstep(1.0, 4.0, knot_r)) * step(0.45, hash13(seed + 17.0)) * (0.5 + 0.5 * sin(knot_r * 6.0));

	float tone = hash13(seed + 23.0);
	vec3 base = mix(mid_color, light_color, smoothstep(0.2, 0.8, w.z) * 0.8 + tone * 0.25);
	base = mix(base, dark_color, w.y * 0.6);
	// Fibres: fine dark lines along the grain.
	base *= 0.62 + 0.55 * w.z;
	// Old water stains and grime in broad blotches.
	float stain = smoothstep(0.45, 0.75, fbm(p * vec3(4.0, 9.0, 9.0) + s * 4.0 + 20.0));
	base = mix(base, dark_color * 0.8, stain * 0.35);
	base = mix(base, dark_color * 0.7, knot * 0.9 + knot_rings * 0.35);
	base = mix(base, dark_color * 0.45, check * 0.85);
	// Sun and salt bleach the upper faces grey, more on some planks than others.
	float up = clamp(local_normal.y, 0.0, 1.0);
	float bleach_amount = bleach * (0.5 + 0.7 * tone) * (0.4 + 0.6 * up) * smoothstep(0.25, 0.75, fbm(p * vec3(3.0, 10.0, 10.0) + s * 2.0) + 0.2);
	base = mix(base, bleach_color * (0.85 + 0.3 * w.z), clamp(bleach_amount, 0.0, 0.8));
	// Ends of planks darker, where the end grain soaks things up.
	float end = abs(local_normal.x);
	base = mix(base, dark_color * 0.9, end * 0.5);
	ALBEDO = base;
	ROUGHNESS = clamp(roughness_base + w.y * 0.08 - knot * 0.1, 0.0, 1.0);

	// Bump from screen-space derivatives of the height field (Mikkelsen), in view space.
	float h = (w.x - check * 1.6 - knot_rings * 0.3) * bump * 0.0012;
	vec3 dpdx = dFdx(VERTEX);
	vec3 dpdy = dFdy(VERTEX);
	vec3 n = normalize(NORMAL);
	vec3 r1 = cross(dpdy, n);
	vec3 r2 = cross(n, dpdx);
	float det = dot(dpdx, r1);
	vec3 grad = sign(det) * (dFdx(h) * r1 + dFdy(h) * r2);
	NORMAL = normalize(abs(det) * n - grad);
}
