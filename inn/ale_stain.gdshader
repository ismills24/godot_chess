shader_type spatial;
// A spilled-ale stain decal for a flat quad on the floor or a table top. The blot outline
// is noise-ragged with a darker dried rim; `wet` makes it glossy so it catches the fire.
// `ring` > 0 draws a mug ring instead of a blot.
render_mode blend_mix, depth_draw_never, shadows_disabled;

#include "res://menu/alley_noise.gdshaderinc"

uniform vec4 stain_color : source_color = vec4(0.07, 0.04, 0.015, 1.0);
uniform float seed = 0.0;
uniform float wet = 0.0;
uniform float ring = 0.0;
uniform float strength = 0.8;

void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float r = length(p);
	float a = atan(p.y, p.x);
	float edge = 0.72 + 0.18 * sin(a * 3.0 + seed) + 0.1 * sin(a * 7.0 + seed * 2.3)
		+ (alley_noise(p * 4.0 + seed) - 0.5) * 0.3;
	float blot = 1.0 - smoothstep(edge - 0.06, edge, r);
	// Splash droplets around the main blot.
	float drops = step(0.83, alley_noise(p * 9.0 + seed * 5.0)) * step(r, 0.98);
	float rim = smoothstep(edge - 0.2, edge - 0.04, r) * blot;
	float shape = max(blot, drops * 0.8);
	float inner = 0.55 + 0.25 * alley_noise(p * 6.0 + seed);
	float tone = mix(inner, 1.0, rim);
	if (ring > 0.0) {
		float band = 1.0 - smoothstep(0.0, 0.1, abs(r - 0.82 + 0.04 * sin(a * 2.0 + seed)));
		float gap = smoothstep(0.2, 0.5, alley_noise(vec2(a * 2.0, seed)));
		shape = band * gap;
		tone = 1.0;
	}
	ALBEDO = stain_color.rgb * mix(1.3, 0.5, tone);
	ALPHA = shape * strength * tone;
	ROUGHNESS = mix(0.75, 0.08, wet);
	SPECULAR = mix(0.3, 0.9, wet);
}
