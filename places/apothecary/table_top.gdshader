shader_type spatial;
// The round table's own 1k wood, sharpened for the seated view, where the top fills the
// screen. Fine grain is drawn over it in the texture's space (so it runs with the coarse
// grain), and the top picks up use: fine scratches,
// rings left by flasks, a spatter of candle wax by the candle and a waxed sheen that is worn
// dull where hands rest.

uniform sampler2D albedo_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D normal_tex : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D orm_tex : hint_default_white, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float detail_strength = 0.3;
// The top surface's height and centre, in world space; wear is drawn only up there.
uniform float top_y = 0.74;
uniform vec2 centre = vec2(0.0, 0.0);
uniform vec2 wick = vec2(0.0, 0.0);

varying vec3 world_at;
varying vec3 world_normal;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

float lum(vec3 c) {
	return dot(c, vec3(0.3, 0.55, 0.15));
}

void vertex() {
	world_at = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	vec3 base = texture(albedo_tex, UV).rgb;
	// Fine grain drawn on top, in the texture's own space, where the grain runs along v:
	// fibres, a few darker late-wood lines and open pores, faded out where finer than a pixel.
	vec2 g = UV * vec2(2600.0, 60.0);
	float wobble = noise(UV * vec2(40.0, 6.0)) * 6.0;
	float fibres = noise(vec2(g.x + wobble * 40.0, g.y)) - 0.5;
	float late = smoothstep(0.82, 0.97, noise(vec2(UV.x * 520.0 + wobble, UV.y * 8.0)));
	vec2 pore_cell = floor(UV * vec2(1800.0, 400.0));
	float pores = step(0.94, hash(pore_cell));
	float fine_vis = 1.0 - smoothstep(0.4, 1.2, fwidth(g.x));
	float grain = 1.0 + (fibres * 0.5 - late * 0.35 - pores * 0.25) * fine_vis;
	vec3 color = base * mix(1.0, grain, detail_strength * 2.0);

	vec3 n_base = texture(normal_tex, UV).rgb;
	vec3 orm = texture(orm_tex, UV).rgb;
	float rough = orm.g;

	float on_top = smoothstep(0.85, 0.97, world_normal.y) * (1.0 - smoothstep(0.02, 0.05, abs(world_at.y - top_y)));
	vec2 p = world_at.xz - centre;

	// Fine scratches: short straight strokes in a few directions.
	float scratches = 0.0;
	for (int i = 0; i < 3; i++) {
		float a = float(i) * 2.1 + 0.4;
		vec2 dir = vec2(cos(a), sin(a));
		vec2 r = vec2(dot(p, dir), dot(p, vec2(-dir.y, dir.x)));
		vec2 cell = vec2(0.035, 0.0025);
		vec2 id = floor(r / cell);
		float lit = step(0.985, hash(id + float(i) * 13.0));
		float along = abs(fract(r.x / cell.x) - 0.5);
		float line = 1.0 - smoothstep(0.0, 0.25, abs(fract(r.y / cell.y) - 0.5) - 0.1);
		scratches = max(scratches, lit * line * (1.0 - smoothstep(0.25, 0.5, along)));
	}
	float scratch_vis = 1.0 - smoothstep(0.3, 1.0, fwidth(p.y) / 0.0025);
	scratches *= scratch_vis;

	// Rings where flasks and a cup have stood.
	float rings = 0.0;
	vec2 ring_at[3] = vec2[3](vec2(0.36, -0.1), vec2(-0.42, 0.18), vec2(0.18, 0.4));
	float ring_r[3] = float[3](0.032, 0.045, 0.038);
	for (int i = 0; i < 3; i++) {
		float d = abs(length(p - ring_at[i]) - ring_r[i]);
		float broken = smoothstep(0.3, 0.6, noise((p - ring_at[i]) * 60.0 + float(i) * 7.0));
		rings = max(rings, (1.0 - smoothstep(0.0015, 0.004, d)) * broken);
	}

	// Candle wax: little pale drops spattered round the candle.
	float wax = 0.0;
	vec2 w = world_at.xz - wick;
	vec2 drop_cell = floor(w / 0.018);
	vec2 drop_off = vec2(hash(drop_cell), hash(drop_cell + 5.3)) - 0.5;
	float drop_d = length(fract(w / 0.018) - 0.5 - drop_off * 0.5) * 0.018;
	float near = 1.0 - smoothstep(0.03, 0.11, length(w));
	wax = (1.0 - smoothstep(0.002, 0.0035, drop_d)) * step(1.0 - near * 0.55, hash(drop_cell + 2.7));
	wax = max(wax, 1.0 - smoothstep(0.034, 0.038, length(w) + 0.006 * noise(w * 300.0)));

	// Wax polish: glossy toward the middle, dulled where hands and sleeves rub the edge.
	float edge_rub = smoothstep(0.35, 0.55, length(p)) * (0.6 + 0.4 * noise(p * 14.0));
	float polish = 0.9 + 0.15 * noise(p * 6.0);

	color *= mix(1.0, 1.0 - 0.35 * rings, on_top);
	color = mix(color, color * 1.35 + vec3(0.03, 0.025, 0.02), scratches * on_top * 0.25);
	color = mix(color, vec3(0.78, 0.7, 0.58), wax * on_top);

	ALBEDO = color;
	vec3 n = normalize(vec3((n_base.xy - 0.5) + vec2(fibres * 0.12 * fine_vis, 0.0), 1.0));
	NORMAL_MAP = vec3(n.xy + 0.5, 1.0);
	ROUGHNESS = mix(rough, mix(rough * polish, rough * 1.1, edge_rub), on_top);
	ROUGHNESS = mix(ROUGHNESS, 0.85, scratches * on_top * 0.5);
	ROUGHNESS = mix(ROUGHNESS, 0.35, wax * on_top);
	AO = orm.r;
	SPECULAR = 0.5;
}
