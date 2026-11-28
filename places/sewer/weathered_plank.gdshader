shader_type spatial;
// Weathered softwood planks for the rat-catcher's crates, built from object-space position so the
// grain stays sharp at full resolution however close the camera is. Each plank is cut from an
// imaginary log running along local X: growth rings come from the distance to the log's pith
// (offset per plank), warped by noise, with fine fibres, pores, a few knots and grey weathering
// over the warm brown. The rings and fibres are also carved into the normal.

uniform vec3 wood_light : source_color = vec3(0.50, 0.34, 0.22);
uniform vec3 wood_dark : source_color = vec3(0.26, 0.165, 0.10);
uniform vec3 weather_color : source_color = vec3(0.44, 0.40, 0.35);
uniform float weathering : hint_range(0.0, 1.0) = 0.5;
uniform float ring_spacing = 0.013;
uniform float bump = 0.55;

varying vec3 obj;
varying vec3 obj_normal;

float hash(vec3 p) {
	p = fract(p * 0.3183099 + vec3(0.1, 0.2, 0.3));
	p *= 17.0;
	return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float noise(vec3 x) {
	vec3 i = floor(x);
	vec3 f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(hash(i), hash(i + vec3(1, 0, 0)), f.x), mix(hash(i + vec3(0, 1, 0)), hash(i + vec3(1, 1, 0)), f.x), f.y),
		mix(mix(hash(i + vec3(0, 0, 1)), hash(i + vec3(1, 0, 1)), f.x), mix(hash(i + vec3(0, 1, 1)), hash(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}

float fbm(vec3 p) {
	float total = 0.0;
	float amp = 0.5;
	for (int k = 0; k < 4; k++) {
		total += amp * noise(p);
		p = p * 2.03 + vec3(1.7, 9.2, 3.1);
		amp *= 0.5;
	}
	return total;
}

// Wood at p: x = darkness of the grain (0 light, 1 dark), y = relief (height).
vec2 wood(vec3 p, vec3 seed) {
	// The pith of this plank's log sits off below and to the side of the plank.
	vec2 pith = vec2(-0.09 - seed.x * 0.12, (seed.y - 0.5) * 0.3);
	vec3 q = p + seed * 13.0;
	float warp = fbm(vec3(q.x * 1.2, q.y * 6.0, q.z * 6.0)) * 0.035 + noise(vec3(q.x * 0.35, 0.0, seed.z * 7.0)) * 0.02;
	float radius = length(p.yz - pith) + warp;
	// Knots: a couple of dark eyes along the plank that bend the rings round them.
	float knot_x = (seed.z - 0.5) * 0.5;
	vec2 knot_d = vec2((p.x - knot_x) * 0.55, p.z - (seed.x - 0.5) * 0.08);
	float knot = exp(-dot(knot_d, knot_d) / 0.00018);
	radius += knot * 0.02;
	float ring = fract(radius / ring_spacing);
	// Latewood: a thin dark band at the end of each ring, sharp on one side.
	float late = smoothstep(0.62, 0.86, ring) * (1.0 - smoothstep(0.9, 1.0, ring));
	float fibre = noise(vec3(q.x * 9.0, q.y * 380.0, q.z * 380.0));
	float fibre_fine = noise(vec3(q.x * 22.0, q.y * 900.0, q.z * 900.0));
	float pores = smoothstep(0.78, 0.92, noise(vec3(q.x * 60.0, q.y * 520.0, q.z * 520.0)));
	float dark = late * 0.5 + fibre * 0.25 + fibre_fine * 0.12 + pores * 0.35 + knot * 0.9;
	// Weathered wood is worn soft between the latewood, which stands proud.
	float height = late * 0.6 + fibre * 0.35 + fibre_fine * 0.2 - pores * 0.4 - knot * 0.3;
	return vec2(dark, height);
}

void vertex() {
	obj = VERTEX;
	obj_normal = NORMAL;
}

void fragment() {
	vec3 node = NODE_POSITION_WORLD;
	vec3 seed = vec3(hash(node * 3.7), hash(node * 5.3 + 1.0), hash(node * 7.9 + 2.0));
	vec2 w = wood(obj, seed);
	// Relief from finite differences across the face.
	float e = 0.0006;
	vec2 wx = wood(obj + vec3(e, 0, 0), seed);
	vec2 wy = wood(obj + vec3(0, e, 0), seed);
	vec2 wz = wood(obj + vec3(0, 0, e), seed);
	vec3 grad = vec3(wx.y - w.y, wy.y - w.y, wz.y - w.y) / e;
	vec3 n = normalize(obj_normal);
	grad -= n * dot(grad, n);
	vec3 bent = normalize(n - grad * bump * 0.0015);
	NORMAL = normalize((VIEW_MATRIX * MODEL_MATRIX * vec4(bent, 0.0)).xyz);

	vec3 color = mix(wood_light, wood_dark, clamp(w.x, 0.0, 1.0));
	// Broad blotches of grey weathering and grime, more toward the plank ends.
	float blotch = fbm(vec3(obj.x * 3.0, obj.y * 3.0, obj.z * 9.0) + seed * 20.0);
	float grey = clamp((blotch - 0.45) * 2.2 + weathering - 0.2, 0.0, 1.0) * weathering;
	color = mix(color, weather_color * (0.8 + 0.4 * w.y), grey);
	color *= 0.9 + 0.2 * noise(vec3(obj.x * 1.5, 0.0, obj.z * 4.0) + seed * 9.0);
	// Scratches: thin pale nicks at a slant across the grain, from years of cages and boots.
	for (int k = 0; k < 3; k++) {
		float angle = 0.35 + float(k) * 1.1 + seed.x * 2.0;
		vec2 dir = vec2(cos(angle), sin(angle));
		vec2 uv = obj.xz * 9.0 + seed.yz * 5.0 + float(k) * 3.7;
		float along = dot(uv, dir);
		float across = dot(uv, vec2(-dir.y, dir.x));
		float cell = floor(across * 2.0);
		float line = abs(fract(across * 2.0) - 0.5);
		float present = step(0.72, hash(vec3(cell, float(k), seed.x * 9.0))) * step(0.45, noise(vec3(along * 0.8, cell, float(k))));
		color = mix(color, color * 1.25 + vec3(0.03, 0.025, 0.02), present * (1.0 - smoothstep(0.004, 0.012, line)) * 0.4);
	}

	ALBEDO = color;
	ROUGHNESS = 0.82 + 0.12 * w.x;
	SPECULAR = 0.3;
}
