shader_type spatial;
// The mountain's weathered sandstone, built for full resolution: procedural in world space
// (3D noise, so it works on the flat top, the cliff and the boulders alike). Blotchy grey base
// tones; rounded weathering lumps, shallow pits and hairline cracks carried into the lighting
// as bump; a sand grain fine enough to read up close; crusts of pale lichen and dark spots on
// up-facing rock; strata on the steep faces. Fine octaves fade out by screen footprint, so far
// rock settles to its average instead of shimmering.

uniform vec4 color_a : source_color = vec4(0.43, 0.42, 0.40, 1.0);
uniform vec4 color_b : source_color = vec4(0.33, 0.33, 0.31, 1.0);
uniform vec4 lichen_color : source_color = vec4(0.55, 0.57, 0.47, 1.0);
uniform vec4 crust_color : source_color = vec4(0.62, 0.62, 0.58, 1.0);
uniform vec4 low_color : source_color = vec4(0.35, 0.36, 0.34, 1.0);
uniform float low_height = -1000.0;
uniform float scale = 0.45;
uniform float lichen = 0.3;
uniform float strata = 0.5;
uniform float cracks = 0.5;
uniform float pits = 0.5;
uniform float bump = 1.0;
uniform float grain = 1.0;

varying vec3 world_at;
varying vec3 world_normal;

float hash3(vec3 p) {
	p = fract(p * vec3(0.1031, 0.1030, 0.0973));
	p += dot(p, p.yxz + 33.33);
	return fract((p.x + p.y) * p.z);
}

float noise3(vec3 x) {
	vec3 i = floor(x);
	vec3 f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(
		mix(mix(hash3(i), hash3(i + vec3(1, 0, 0)), f.x),
			mix(hash3(i + vec3(0, 1, 0)), hash3(i + vec3(1, 1, 0)), f.x), f.y),
		mix(mix(hash3(i + vec3(0, 0, 1)), hash3(i + vec3(1, 0, 1)), f.x),
			mix(hash3(i + vec3(0, 1, 1)), hash3(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}

// Fractal noise whose octaves fade once they get finer than a pixel (`fw` = pixel size in
// noise units at the first octave).
float fbm(vec3 p, int octaves, float fw) {
	float sum = 0.0;
	float weight = 0.0;
	float amp = 0.5;
	for (int k = 0; k < octaves; k++) {
		float keep = 1.0 - smoothstep(0.3, 0.8, fw);
		sum += amp * mix(0.5, noise3(p), keep);
		weight += amp;
		p = p * 2.07 + vec3(1.7, 9.2, 4.1);
		fw *= 2.07;
		amp *= 0.5;
	}
	return sum / weight;
}

// Distance to the nearest of a scatter of pit centres, in a horizontal-ish projection.
float pit_field(vec2 p) {
	vec2 cell = floor(p);
	float best = 8.0;
	for (int j = -1; j <= 1; j++) {
		for (int i = -1; i <= 1; i++) {
			vec2 c = cell + vec2(float(i), float(j));
			float h = hash3(vec3(c, 7.0));
			vec2 centre = c + vec2(h, hash3(vec3(c, 3.0)));
			float radius = 0.25 + 0.5 * hash3(vec3(c, 11.0));
			float d = length(p - centre) / radius;
			best = min(best, d + step(h, 0.45) * 8.0);
		}
	}
	return best;
}

// Surface relief in metres (positive = proud), used for bump and for cavity darkening.
float relief(vec3 at, float fw, out float cavity) {
	vec3 p = at * scale;
	float lumps = fbm(p * 2.2, 4, fw * scale * 2.2);
	float h = (lumps - 0.5) * 0.06;
	// Knobbly weathering a hand-span across, and a coarse granular skin.
	float knobs = fbm(at * 7.0 + vec3(3.1), 3, fw * 7.0);
	h += (knobs - 0.5) * 0.012;
	float granules = fbm(at * 34.0, 2, fw * 34.0);
	h += (granules - 0.5) * 0.0035 * grain;
	// Hairline cracks wandering across the rock.
	float c_fw = fw * 1.4;
	float split = abs(fbm(at * 1.4 + vec3(lumps * 0.6), 3, c_fw) - 0.5);
	float crack = (1.0 - smoothstep(0.0, 0.012 + c_fw * 0.02, split)) * cracks;
	h -= crack * 0.008;
	// Shallow weathering pits.
	float pit_d = pit_field(at.xz * 3.2 + at.y * 0.7);
	float pit = (1.0 - smoothstep(0.3, 1.0, pit_d)) * pits;
	h -= pit * 0.01;
	// Sand grain.
	float sand = fbm(at * 90.0, 3, fw * 90.0);
	h += (sand - 0.5) * 0.0016 * grain;
	cavity = clamp(crack * 0.9 + pit * 0.45, 0.0, 1.0);
	return h;
}

void vertex() {
	world_at = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	vec3 n = normalize(world_normal);
	float fw = max(length(fwidth(world_at)), 1e-5);
	float cavity;
	float h = relief(world_at, fw, cavity);
	// Bump from finite differences along two tangents of the surface.
	vec3 t1 = normalize(abs(n.y) < 0.95 ? cross(n, vec3(0, 1, 0)) : cross(n, vec3(1, 0, 0)));
	vec3 t2 = cross(n, t1);
	float e = max(0.0015, fw * 0.5);
	float c_unused;
	float h1 = relief(world_at + t1 * e, fw, c_unused);
	float h2 = relief(world_at + t2 * e, fw, c_unused);
	vec3 bent = normalize(n - bump * ((h1 - h) / e * t1 + (h2 - h) / e * t2));

	vec3 p = world_at * scale;
	float blotch = fbm(p, 4, fw * scale);
	vec3 color = mix(color_a.rgb, color_b.rgb, smoothstep(0.35, 0.68, blotch));
	// Finer tonal mottling and grain.
	float mottle = fbm(world_at * 3.1 + 5.0, 3, fw * 3.1);
	color *= 0.9 + 0.2 * mottle;
	float speck = fbm(world_at * 160.0, 2, fw * 160.0);
	color *= 1.0 + (speck - 0.5) * 0.2 * grain;
	// Sparse dark and light mineral grains, round, a couple of millimetres across.
	vec3 g = world_at * 220.0;
	vec3 gc = floor(g);
	float mineral = hash3(gc);
	vec3 gj = vec3(hash3(gc + 17.0), hash3(gc + 31.0), hash3(gc + 47.0)) * 0.5 + 0.25;
	float gdot = 1.0 - smoothstep(0.18, 0.18 + fw * 220.0 + 0.05, length(fract(g) - gj));
	float grain_keep = 1.0 - smoothstep(0.002, 0.006, fw);
	color *= 1.0 + gdot * grain_keep * grain * (step(0.9, mineral) * 0.2 - step(mineral, 0.12) * 0.3);
	// Dark weather stains running down steep faces.
	float steep = 1.0 - smoothstep(0.55, 0.85, n.y);
	float streak = fbm(vec3(world_at.x * 3.0, world_at.y * 0.25, world_at.z * 3.0), 3, fw * 3.0);
	color *= 1.0 - steep * 0.25 * smoothstep(0.5, 0.75, streak);
	if (strata > 0.0) {
		float band = sin(world_at.y * 7.0 + fbm(p * 2.0, 3, fw * scale * 2.0) * 4.0);
		color *= 1.0 - strata * steep * 0.22 * smoothstep(0.55, 0.95, band);
		color *= 1.0 + strata * steep * 0.1 * smoothstep(0.6, 1.0, -band);
	}
	// Lichen: pale crusts and grey-green patches on the up-facing rock, with dark dots.
	float up = smoothstep(0.35, 0.8, n.y);
	float patch_n = fbm(world_at * 1.9 + 21.0, 4, fw * 1.9);
	float patch_edge = fbm(world_at * 14.0 + 3.0, 2, fw * 14.0);
	float cover = smoothstep(0.62 - lichen * 0.25, 0.66 - lichen * 0.25, patch_n + (patch_edge - 0.5) * 0.12) * up;
	vec3 lichen_tone = mix(lichen_color.rgb, crust_color.rgb, smoothstep(0.4, 0.7, patch_edge));
	color = mix(color, lichen_tone * (0.9 + 0.2 * speck), cover * min(1.0, lichen * 2.5));
	float dots = fbm(world_at * 70.0 + 40.0, 2, fw * 70.0);
	color *= 1.0 - 0.22 * smoothstep(0.72, 0.78, dots) * up * min(1.0, lichen * 3.0);
	// Cavities hold shadow and grime.
	color *= 1.0 - cavity * 0.4;
	float low = 1.0 - smoothstep(low_height - 0.05, low_height + 0.12 + 0.1 * blotch, world_at.y);
	color = mix(color, low_color.rgb, low);

	ALBEDO = color;
	ROUGHNESS = 0.9 - cover * 0.05 + cavity * 0.05;
	SPECULAR = 0.3;
	NORMAL = normalize((VIEW_MATRIX * vec4(bent, 0.0)).xyz);
}
