shader_type spatial;
// The alchemist's worn pink rug, drawn for full resolution: a dark outer guard, a cream
// stripe, a border of little interlocking hooks, then a field of diamonds in a lattice with
// a medallion in the middle. Under the pattern there is a real knotted pile (rows of knots,
// each a slightly different shade), wear rubbed down to the pale warp where feet go, a few
// stains, and fringe tassels at both short ends. Edges are filtered by their screen size so
// the pattern stays crisp without shimmering.

uniform vec2 size = vec2(2.6, 3.8);
uniform vec3 field_color : source_color = vec3(0.42, 0.12, 0.26);
uniform vec3 motif_color : source_color = vec3(0.72, 0.32, 0.46);
uniform vec3 guard_color : source_color = vec3(0.2, 0.055, 0.15);
uniform vec3 stripe_color : source_color = vec3(0.78, 0.58, 0.38);
uniform vec3 warp_color : source_color = vec3(0.62, 0.52, 0.42);
// How far the fringe hangs beyond each short end, in metres (the mesh is that much longer).
uniform float fringe = 0.07;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

float fbm(vec2 p) {
	float total = 0.0;
	float amp = 0.5;
	for (int i = 0; i < 4; i++) {
		total += noise(p) * amp;
		p = p * 2.03 + 17.0;
		amp *= 0.5;
	}
	return total;
}

// A filtered step: 1 inside (d < 0), 0 outside, softened over one pixel.
float fill(float d) {
	float w = max(fwidth(d), 1e-5);
	return 1.0 - smoothstep(-w, w, d);
}

void fragment() {
	// Metres from the rug's centre; y runs along its length.
	vec2 full = size + vec2(0.0, fringe * 2.0);
	vec2 m = (UV - 0.5) * full;
	vec2 half_size = size * 0.5;
	float beyond = abs(m.y) - half_size.y;

	// Fringe: tassels hanging past the short ends, cut away between them.
	if (beyond > 0.0) {
		float strand = fract(m.x * 90.0);
		float tassel_len = fringe * (0.7 + 0.3 * hash(vec2(floor(m.x * 90.0), sign(m.y))));
		float cut = step(0.5, abs(strand - 0.5) * 2.0) + step(tassel_len, beyond);
		if (cut > 0.5 || abs(m.x) > half_size.x - 0.02) {
			discard;
		}
		ALBEDO = warp_color * (0.75 + 0.3 * hash(vec2(floor(m.x * 90.0), 3.0))) * (1.0 - beyond / fringe * 0.4);
		ROUGHNESS = 1.0;
		SPECULAR = 0.1;
	} else {
		// Distance in from the nearest edge, and position within the field.
		vec2 edge = half_size - abs(m);
		float inset = min(edge.x, edge.y);

		// Field: a lattice of diamonds with a small cross in each, a medallion at the centre.
		vec2 cell = vec2(0.19, 0.22);
		vec2 q = m / cell;
		vec2 f = abs(fract(q) - 0.5);
		float diamond = fill(f.x + f.y - 0.33);
		float diamond_core = fill(f.x + f.y - 0.17);
		float cross = fill(min(f.x, f.y) - 0.025) * fill(f.x + f.y - 0.12);
		float lattice = fill(abs(f.x + f.y - 0.42) - 0.018);
		float medallion_d = abs(m.x) / 0.5 + abs(m.y) / 0.78;
		float medallion = fill(medallion_d - 1.0);
		float medallion_ring = fill(abs(medallion_d - 0.86) - 0.025) + fill(abs(medallion_d - 0.55) - 0.03);
		float medallion_heart = fill(length(m / vec2(0.12, 0.18)) - 1.0);

		vec3 color = field_color;
		color = mix(color, motif_color, diamond * 0.65);
		color = mix(color, guard_color, diamond_core * 0.7);
		color = mix(color, stripe_color, cross * 0.75);
		color = mix(color, stripe_color * 0.8, lattice * 0.45);
		// Medallion: plain dark ground, gold rings, a pink heart.
		color = mix(color, guard_color * 1.3, medallion * (1.0 - medallion_ring) * 0.8);
		color = mix(color, stripe_color, medallion_ring * 0.85);
		color = mix(color, motif_color * 1.1, medallion_heart);

		// Border: guard band, stripe, hook band, stripe.
		float hooks_t = (edge.x < edge.y) ? m.y : m.x;
		float hook_band = fill(abs(inset - 0.15) - 0.06);
		float along = (fract(hooks_t * 8.0) - 0.5) / 8.0;
		float across = inset - 0.15;
		float chain = abs(along) + abs(across);
		float hook = fill(chain - 0.05) * (1.0 - fill(chain - 0.032)) + fill(chain - 0.014);
		color = mix(color, guard_color * 1.4, hook_band);
		color = mix(color, motif_color * 1.05, hook * hook_band);
		color = mix(color, stripe_color, fill(abs(inset - 0.075) - 0.012));
		color = mix(color, stripe_color, fill(abs(inset - 0.225) - 0.01));
		color = mix(color, guard_color, fill(inset - 0.055));

		// Pile: rows of knots, each its own shade, with fibres lying along the rows.
		vec2 knot_grid = m * vec2(150.0, 170.0);
		vec2 knot_id = floor(knot_grid);
		vec2 in_knot = fract(knot_grid) - 0.5;
		float knot_shade = 0.9 + 0.2 * hash(knot_id);
		float knot_round = 1.0 - 0.25 * dot(in_knot, in_knot) * 2.0;
		float fibres = noise(vec2(m.x * 700.0, m.y * 90.0)) - 0.5;
		// Fade the knot texture out where it would be finer than a pixel.
		float knot_vis = 1.0 - smoothstep(0.35, 0.8, max(fwidth(knot_grid.x), fwidth(knot_grid.y)));
		float pile = mix(1.0, knot_shade * knot_round * (1.0 + fibres * 0.18), knot_vis);

		// Wear: a trodden path down the middle and around the table, rubbed to the warp,
		// with the pile tufting at the edges of each worn patch.
		float tread = fbm(m * 2.2 + 3.0) * 0.7 + fbm(m * 9.0) * 0.3;
		float trodden = smoothstep(0.62, 0.78, tread + 0.18 * (1.0 - smoothstep(0.0, 0.7, abs(m.x + 0.3))));
		float bare = smoothstep(0.82, 0.9, tread + 0.12 * (1.0 - smoothstep(0.0, 0.6, abs(m.x + 0.3))));
		vec3 warp = warp_color * (0.8 + 0.25 * step(0.5, fract(m.x * 150.0)));
		color *= pile;
		color = mix(color, color * 1.12 + vec3(0.03, 0.02, 0.02), trodden * 0.6);
		color = mix(color, warp * 0.7, bare * 0.75);

		// A couple of old stains (spilled potion), and grime toward the edges.
		float stain = smoothstep(0.55, 0.62, fbm(m * 3.0 + 41.0)) * (1.0 - smoothstep(0.62, 0.7, fbm(m * 3.0 + 41.0)) * 0.4);
		color = mix(color, color * vec3(0.55, 0.5, 0.65), stain * 0.45);
		color *= 0.82 + 0.18 * smoothstep(0.0, 0.35, inset);
		color *= 0.9 + 0.2 * fbm(m * 5.0 + 9.0);

		ALBEDO = color;
		ROUGHNESS = 1.0;
		SPECULAR = 0.15;
		// The pile catches light a little along its rows.
		NORMAL_MAP = vec3(0.5 + in_knot.x * 0.25 * knot_vis, 0.5 + fibres * 0.3 * knot_vis, 1.0);
		NORMAL_MAP_DEPTH = 0.5;
	}
}
