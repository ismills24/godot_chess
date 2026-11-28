shader_type spatial;
// Beach sand worked out per pixel: broad pale/warm drifts, wind ripples a few centimetres
// apart (raised in the bump, so they catch the low sun), fine grain that fades out with
// distance instead of shimmering, a sprinkle of dark grains and shell bits, and wet,
// darker, glossier sand below `low_height` toward the sea.

uniform vec3 color_a : source_color = vec3(0.88, 0.78, 0.61);
uniform vec3 color_b : source_color = vec3(0.82, 0.71, 0.52);
uniform vec3 low_color : source_color = vec3(0.63, 0.54, 0.41);
uniform float low_height = -0.24;
uniform float ripple_strength = 1.0;
uniform float grain_strength = 1.0;

varying vec3 world_at;

float hash12(vec2 p) {
	vec3 q = fract(p.xyx * vec3(0.1031, 0.11369, 0.13787));
	q += dot(q, q.yzx + 19.19);
	return fract((q.x + q.y) * q.z);
}

vec2 hash22(vec2 p) {
	return vec2(hash12(p), hash12(p + 17.3));
}

float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
	return mix(mix(hash12(i), hash12(i + vec2(1, 0)), u.x), mix(hash12(i + vec2(0, 1)), hash12(i + vec2(1, 1)), u.x), u.y);
}

float fbm(vec2 p) {
	float sum = 0.0;
	float amp = 0.5;
	mat2 turn = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8));
	for (int i = 0; i < 5; i++) {
		sum += amp * vnoise(p);
		p = turn * p * 2.02 + 3.7;
		amp *= 0.5;
	}
	return sum;
}

// Sparse round specks: returns how much of a speck covers p (cells `size` across).
float specks(vec2 p, float density, float radius) {
	vec2 cell = floor(p);
	vec2 f = fract(p);
	float best = 0.0;
	for (int y = -1; y <= 1; y++) {
		for (int x = -1; x <= 1; x++) {
			vec2 c = cell + vec2(float(x), float(y));
			vec2 h = hash22(c);
			if (h.x > density) {
				continue;
			}
			vec2 at = vec2(float(x), float(y)) + hash22(c + 5.1) - f;
			best = max(best, 1.0 - smoothstep(radius * 0.6, radius, length(at)));
		}
	}
	return best;
}

void vertex() {
	world_at = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 p = world_at.xz;
	// How big a pixel is here: fade detail finer than that.
	float px = length(fwidth(p));
	float fine = 1.0 - smoothstep(0.002, 0.008, px);
	float mid = 1.0 - smoothstep(0.01, 0.04, px);

	float drift = fbm(p * 0.35);
	float patch = fbm(p * 2.2 + 9.0);
	vec3 color = mix(color_a, color_b, smoothstep(0.3, 0.7, drift * 0.7 + patch * 0.4));

	// Wind ripples: crests about 6 cm apart running roughly along the beach, wandering.
	float warp = fbm(p * 1.3 + 4.0) * 3.5 + fbm(p * 6.0) * 0.6;
	float phase = (p.x * 0.92 + p.y * 0.38) * 105.0 + warp * 6.0;
	// Asymmetric profile: a gentle windward slope and a steeper lee face.
	float saw = fract(phase / 6.2831853);
	float ripple = smoothstep(0.0, 0.75, saw) - smoothstep(0.75, 1.0, saw);
	float ripple_amount = ripple_strength * smoothstep(0.25, 0.6, fbm(p * 0.8 + 2.0)) * mid;

	// Grain: fine bright and dark grains, faded with distance.
	float g1 = vnoise(p * 900.0);
	float g2 = vnoise(p * 2300.0 + 7.0);
	float grain = ((g1 - 0.5) * 0.6 + (g2 - 0.5) * 0.4) * grain_strength * fine;
	color *= 1.0 + grain * 0.35;
	color *= 1.0 + (vnoise(p * 140.0) - 0.5) * 0.12 * mid;
	// Dark mineral grains and pale bits of broken shell.
	float dark = specks(p * 260.0, 0.22, 0.32) * fine;
	float shell = specks(p * 90.0 + 31.0, 0.025, 0.36) * mid;
	color = mix(color, vec3(0.30, 0.26, 0.22), dark * 0.55);
	color = mix(color, vec3(0.96, 0.93, 0.88), shell * 0.5);
	// Ripple crests a touch paler, troughs collect darker grains.
	color *= 1.0 + (ripple - 0.5) * 0.08 * ripple_amount;

	// Wet sand toward the sea.
	float low = 1.0 - smoothstep(low_height - 0.05, low_height + 0.12 + 0.1 * drift, world_at.y);
	color = mix(color, low_color * (0.92 + 0.16 * patch), low);
	ALBEDO = color;
	ROUGHNESS = mix(0.95, 0.4, low * 0.8);
	SPECULAR = 0.3 + low * 0.3;

	float h = ripple * ripple_amount * (1.0 - low * 0.7) * 0.004 + grain * 0.0006 + shell * 0.0008 * mid;
	vec3 dpdx = dFdx(VERTEX);
	vec3 dpdy = dFdy(VERTEX);
	vec3 n = normalize(NORMAL);
	vec3 r1 = cross(dpdy, n);
	vec3 r2 = cross(n, dpdx);
	float det = dot(dpdx, r1);
	vec3 grad = sign(det) * (dFdx(h) * r1 + dFdy(h) * r2);
	NORMAL = normalize(abs(det) * n - grad);
}
