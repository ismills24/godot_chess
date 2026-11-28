shader_type spatial;
// A puddle of spilled beer on one board square, for a flat quad one square wide lying on the
// board (common/beer_spill.gd). A ragged amber pool with a bright wet meniscus and a darker
// soaked rim, a ring of foam bubbles round the edge and a few satellite drops. It stays
// see-through in the middle so the square's colour and any piece's foot still read; the foam
// rim and the gloss are what make it stand out on the wood.
// `spread` grows it from nothing (0) to its full size (1); `foam` is how much head is still on
// it (high just after the splash, settling to a lasting ring); `amount` fades it out as it dries.
render_mode blend_mix, depth_draw_never, shadows_disabled, cull_disabled;

#include "res://menu/alley_noise.gdshaderinc"

uniform float seed = 0.0;
uniform float spread : hint_range(0.0, 1.0) = 1.0;
uniform float foam : hint_range(0.0, 1.0) = 0.4;
uniform float amount : hint_range(0.0, 1.0) = 1.0;
uniform vec3 beer_light : source_color = vec3(1.0, 0.62, 0.1);
uniform vec3 beer_deep : source_color = vec3(0.6, 0.3, 0.04);
uniform vec3 foam_color : source_color = vec3(1.0, 0.95, 0.82);
// How much the beer glows on its own, so it reads in a dim room.
uniform float glow = 0.22;

float fbm(vec2 p) {
	return alley_noise(p) * 0.55 + alley_noise(p * 2.1 + 7.3) * 0.3 + alley_noise(p * 4.3 + 1.7) * 0.15;
}

// The pool's signed reach at `p` (square spans -1..1): > 0 inside, its depth toward the middle.
float pool_field(vec2 p, float grow) {
	vec2 c = vec2(sin(seed * 1.3), cos(seed * 2.1)) * 0.06;
	vec2 q = p - c;
	float r = length(q);
	float a = atan(q.y, q.x);
	float edge = 0.8 + 0.05 * sin(a * 2.0 + seed) + 0.04 * sin(a * 5.0 + seed * 1.7)
		+ (fbm(q * 2.4 + seed) - 0.5) * 0.24;
	return edge * grow - r;
}

void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float grow = 1.0 - pow(1.0 - clamp(spread, 0.0, 1.0), 2.0);
	float field = pool_field(p, grow);
	float px = fwidth(field) * 1.2 + 0.004;
	float pool = smoothstep(-px, px, field);

	// Satellite drops thrown off by the splash, beyond the main pool.
	float drops = 0.0;
	for (int i = 0; i < 5; i++) {
		float fi = float(i);
		float h = cell_hash(vec2(fi * 3.7, seed * 1.9 + 1.0));
		float h2 = cell_hash(vec2(seed * 0.7 + 2.0, fi * 5.3));
		float ang = fi * 0.9 + h * 2.4 + seed;
		float dist = (0.8 + h2 * 0.12) * grow;
		vec2 at = vec2(cos(ang), sin(ang)) * dist;
		float rad = (0.04 + 0.04 * h) * smoothstep(0.35, 0.9, spread);
		float d = rad - length(p - at);
		drops = max(drops, smoothstep(-px, px, d));
	}
	float wet = max(pool, drops);
	// 0 at the edge, 1 well inside.
	float depth = clamp(field / 0.35, 0.0, 1.0);
	float meniscus = (1.0 - smoothstep(0.0, 0.06, field)) * pool;

	// Foam: a creamy ring of head along the rim, bubbly on its inner side and broken here and
	// there, plus a few loose patches drifting on the pool.
	vec2 g = p * 18.0 + vec2(seed * 3.1, seed * 1.7);
	vec2 cell = floor(g);
	float bubbles = 0.0;
	for (int y = -1; y <= 1; y++) {
		for (int x = -1; x <= 1; x++) {
			vec2 n = cell + vec2(float(x), float(y));
			float h = cell_hash(n);
			vec2 centre = n + vec2(cell_hash(n + 17.0), cell_hash(n + 31.0));
			float size = 0.35 + 0.45 * h;
			bubbles = max(bubbles, 1.0 - smoothstep(size - 0.15, size, length(g - centre)));
		}
	}
	// The head piles up thicker along some of the rim than the rest.
	float width = (0.08 + 0.16 * foam) * (0.45 + 1.2 * fbm(normalize(p + 0.0001) * 1.8 + seed));
	float solid = 1.0 - smoothstep(width * 0.45, width * 0.7, field);
	float fringe = (1.0 - smoothstep(width * 0.6, width, field)) * bubbles;
	float broken = smoothstep(0.28, 0.45, fbm(p * 2.2 + seed * 4.0));
	float band = max(solid, fringe) * broken;
	float patches = smoothstep(0.52, 0.72, fbm(p * 3.0 + seed * 2.0 + TIME * 0.02)) * bubbles * (0.3 + foam);
	float froth = clamp(band + patches, 0.0, 1.0) * pool;
	froth = max(froth, drops * 0.6);

	vec3 beer = mix(beer_light, beer_deep, smoothstep(0.0, 1.0, depth));
	beer = mix(beer, beer_light * 1.15, meniscus * 0.6);
	vec3 colour = mix(beer, foam_color, froth);
	ALBEDO = colour;
	// A soft sheen drifting across the deeper beer, as if catching the candles.
	float sheen = smoothstep(0.62, 0.9, fbm(p * 1.4 + seed + vec2(TIME * 0.03, 0.0))) * depth * (1.0 - froth);
	EMISSION = colour * glow * wet + vec3(1.0, 0.85, 0.55) * sheen * 0.35;
	// See-through in the middle (the square shows through), denser at the soaked rim.
	float body = mix(0.55, 0.85, 1.0 - depth) + meniscus * 0.1;
	ALPHA = clamp(max(wet * body, froth * 0.95), 0.0, 1.0) * amount;
	ROUGHNESS = mix(0.05, 0.7, froth);
	SPECULAR = mix(0.9, 0.3, froth);
	// Slow ripples so the gloss moves a little under the candles.
	vec2 ripple = vec2(fbm(p * 5.0 + TIME * 0.15 + seed), fbm(p * 5.0 - TIME * 0.12 + seed + 4.0)) - 0.5;
	NORMAL_MAP = normalize(vec3(ripple * 0.35 * (1.0 - froth), 1.0)) * 0.5 + 0.5;
}
