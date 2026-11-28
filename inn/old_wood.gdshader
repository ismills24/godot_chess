shader_type spatial;
// Weathered wood for the inn's boxes: floorboards, vault boarding, beams, tables, chairs.
// Grain runs along each box's longest side. Every box gets its own seed from where it sits,
// so batched planks differ: their own tone and grain, knots on some, a split along the grain
// on others, worn pale edges, dark end grain, grime blotches and nail holes near the ends of
// long boards. Sizes are in metres; finer fibres, pores and a grain relief add detail at full resolution.

#include "res://menu/alley_noise.gdshaderinc"

uniform vec4 wood_color : source_color = vec4(0.22, 0.12, 0.067, 1.0);
// 0 = tidy, 1 = knocked about.
uniform float wear = 1.0;

varying vec3 local_at;
varying vec3 extent;
varying float seed;

void vertex() {
	// Batched boxes are unit cubes scaled by their instance transform: recover metres.
	extent = vec3(length(MODEL_MATRIX[0].xyz), length(MODEL_MATRIX[1].xyz), length(MODEL_MATRIX[2].xyz));
	local_at = VERTEX * extent;
	vec3 origin = MODEL_MATRIX[3].xyz;
	seed = cell_hash(origin.xz * 7.13 + origin.y * 3.1 + 0.5);
}

float h(float n) {
	return cell_hash(vec2(seed * 91.7, n));
}

void fragment() {
	// u runs along the grain (the longest side), c across it; len and girth are the sizes.
	float u;
	vec2 c;
	float len;
	vec2 girth;
	if (extent.x >= extent.y && extent.x >= extent.z) {
		u = local_at.x; c = local_at.yz; len = extent.x; girth = extent.yz;
	} else if (extent.z >= extent.y) {
		u = local_at.z; c = local_at.xy; len = extent.z; girth = extent.xy;
	} else {
		u = local_at.y; c = local_at.xz; len = extent.y; girth = extent.xz;
	}
	float across = c.x + c.y * 0.7;

	// Knots: on about half the pieces, one or two dark eyes with the grain swirling round.
	float knot = 0.0;
	float swirl = 0.0;
	for (int i = 0; i < 2; i++) {
		if (h(float(i) + 1.0) < 0.55 + 0.15 * wear) {
			vec2 centre = vec2((h(float(i) + 3.0) - 0.5) * len * 0.8, (h(float(i) + 5.0) - 0.5) * 0.12);
			float size = 0.022 + 0.025 * h(float(i) + 7.0);
			float d = length(vec2((u - centre.x) / (size * 1.7), (across - centre.y) / size));
			knot = max(knot, 1.0 - smoothstep(0.55, 1.0, d));
			swirl += exp(-d * 0.9) * 2.5;
		}
	}

	// Grain: wavy rings and fibres, bent around the knots.
	float bend = sin(u * 1.7 + seed * 20.0) * 0.6 + alley_noise(vec2(u * 0.8, across * 3.0 + seed * 9.0)) * 1.5;
	float rings = sin(across * 70.0 + bend * 4.0 + swirl * 3.0 + seed * 40.0);
	float fibres = alley_noise(vec2(u * 2.5, across * 160.0 + seed * 17.0)) - 0.5;

	// A split along the grain on some pieces, part of the way along.
	float split = 0.0;
	if (h(11.0) < 0.45 * wear + 0.1) {
		float line = (h(12.0) - 0.5) * 0.1 + 0.004 * sin(u * 23.0 + seed * 5.0);
		float start = (h(13.0) - 0.5) * len;
		float reach = len * (0.15 + 0.35 * h(14.0));
		split = (1.0 - smoothstep(0.004, 0.009, abs(across - line)))
			* step(start, u) * step(u, start + reach);
	}

	// Edges rubbed pale along the long sides, end grain dark, grime in blotches.
	vec2 to_side = girth * 0.5 - abs(c);
	float rubbed = 1.0 - smoothstep(0.0, 0.012, min(to_side.x, to_side.y));
	float end_grain = smoothstep(len * 0.5 - 0.02, len * 0.5, abs(u));
	float grime = smoothstep(0.55, 0.85, alley_noise(vec2(u * 2.2, across * 6.0) + seed * 31.0)) * wear;
	float scratch = (1.0 - smoothstep(0.0, 0.003, abs(fract(across * 9.0 + u * 0.4 + seed * 3.0) - 0.5)))
		* step(0.8, alley_noise(vec2(u * 3.0, across * 2.0) + seed * 13.0)) * wear;

	// Nail heads near the ends of long boards.
	float nails = 0.0;
	if (len > 1.5) {
		for (int side = -1; side <= 1; side += 2) {
			vec2 nail = vec2(float(side) * (len * 0.5 - 0.07), 0.0);
			for (int k = -1; k <= 1; k += 2) {
				float d = length(vec2(u - nail.x, across - float(k) * 0.045));
				nails = max(nails, 1.0 - smoothstep(0.006, 0.011, d));
			}
		}
	}

	// Full-resolution detail on top: fine fibres and pores along the grain, faded out where
	// they'd shimmer (far off, or the box seen edge-on).
	float fine_rate = fwidth(across) * 520.0 + fwidth(u) * 12.0;
	float fine_fade = 1.0 - smoothstep(0.35, 1.0, fine_rate);
	float fine = (alley_noise(vec2(u * 9.0 + bend, across * 520.0 + seed * 23.0)) - 0.5) * fine_fade;
	float pores = smoothstep(0.72, 0.95, alley_noise(vec2(u * 60.0, across * 900.0 + seed * 7.0)))
		* (1.0 - smoothstep(0.35, 1.0, fine_rate * 1.7));
	// Crisp dark late-wood lines at the edge of each ring.
	float late = smoothstep(0.82, 0.98, rings) * (1.0 - smoothstep(0.35, 1.0, fwidth(across) * 70.0));
	float tone = mix(0.62, 1.25, seed) * (0.86 + rings * 0.12 + fibres * 0.26 + fine * 0.28 - pores * 0.12 - late * 0.1);
	tone *= mix(1.0, 1.3, rubbed * wear);
	tone *= mix(1.0, 0.6, end_grain);
	tone *= 1.0 - grime * 0.45;
	tone *= 1.0 + scratch * 0.35;
	tone = mix(tone, 0.32, knot * 0.85);
	tone = mix(tone, 0.12, split);
	vec3 color = wood_color.rgb * tone;
	color = mix(color, vec3(0.05, 0.045, 0.04), nails * 0.9);

	ALBEDO = color;
	ROUGHNESS = clamp(0.8 - rubbed * 0.15 + grime * 0.1 + split * 0.2, 0.4, 1.0);
	SPECULAR = mix(0.25, 0.6, nails);

	// Relief from the same grain (screen-space bump): the rings and fibres catch a lamp's
	// sheen, splits and nail holes sink in.
	float relief = (rings * 0.12 + fibres * 0.3 + fine * 0.35 - pores * 0.25 - late * 0.2) * 0.0012
		- split * 0.0015 - knot * 0.0004;
	vec3 dpdx = dFdx(VERTEX);
	vec3 dpdy = dFdy(VERTEX);
	vec3 r1 = cross(dpdy, NORMAL);
	vec3 r2 = cross(NORMAL, dpdx);
	float det = dot(dpdx, r1);
	vec3 grad = sign(det) * (dFdx(relief) * r1 + dFdy(relief) * r2);
	NORMAL = normalize(abs(det) * NORMAL - grad);
}
