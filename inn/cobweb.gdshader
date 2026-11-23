shader_type spatial;
// A cobweb slung across a corner. The quad's top edge runs along the ceiling seam and its
// sides against the two walls; the web sags into a ragged triangle below, with threads
// radiating from a hub, a few spiral turns, broken patches and dusty haze.
render_mode blend_mix, cull_disabled, depth_draw_never, shadows_disabled;

#include "res://menu/alley_noise.gdshaderinc"

uniform vec4 web_color : source_color = vec4(0.5, 0.5, 0.5, 1.0);
uniform float seed = 0.0;
uniform float opacity = 0.7;

void fragment() {
	vec2 uv = UV;
	// Ragged triangle: full width at the top, sagging to a point near the bottom.
	float sag = 0.82 + 0.1 * sin(uv.x * 5.0 + seed);
	float half_width = 0.5 * (1.0 - uv.y / sag) + (alley_noise(uv * 9.0 + seed) - 0.5) * 0.08;
	float inside = step(abs(uv.x - 0.5), half_width) * step(uv.y, sag);

	vec2 hub = vec2(0.5 + 0.06 * sin(seed * 3.0), 0.3);
	vec2 d = (uv - hub) * vec2(1.0, 1.4);
	float r = length(d);
	float a = atan(d.y, d.x);
	float radials = 1.0 - smoothstep(0.0, 0.05, abs(fract(a / 6.2832 * 13.0 + 0.04 * sin(r * 9.0 + seed)) - 0.5) * 2.0);
	float spiral = 1.0 - smoothstep(0.0, 0.18, abs(fract(r * 14.0 + a * 0.12 + seed) - 0.5) * 2.0);
	spiral *= step(r, 0.42);
	float broken = smoothstep(0.35, 0.6, alley_noise(uv * 6.0 + seed * 7.0));
	float threads = max(radials * 0.35, spiral * 0.25) * broken;
	// Mostly a grey veil of dust: at this resolution single threads only sparkle.
	float dust = 0.3 * (1.0 - smoothstep(0.05, 0.6, r)) + 0.22 * alley_noise(uv * 8.0 + seed);
	// A couple of loose strands hanging below the web.
	float strand = (1.0 - smoothstep(0.0, 0.012, abs(uv.x - 0.4 - 0.03 * sin(uv.y * 6.0 + seed))))
		* step(sag - 0.1, uv.y) * step(uv.y, 0.98);

	ALBEDO = web_color.rgb;
	ALPHA = clamp((threads + dust) * inside + strand * 0.5, 0.0, 1.0) * opacity;
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
}
