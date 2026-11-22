shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled;

#include "res://menu/alley_noise.gdshaderinc"

uniform float elapsed = 0.0;
uniform float phase = 0.0;
uniform float opacity = 0.37;
uniform bool smoke = false;
uniform bool halo = false;
uniform vec4 mist_color : source_color = vec4(0.23, 0.42, 0.44, 1.0);

void fragment() {
	vec2 p = UV;
	vec2 flow = vec2(p.x * 7.0 + phase - elapsed * 0.19, p.y * 4.5 + elapsed * 0.035);
	float pockets = alley_noise(flow) * 0.74 + alley_noise(flow * 2.32 + 7.3) * 0.26;
	float bank = (p.y - 0.58 + 0.32 * (pockets - 0.5)) * 3.5;
	float edges = smoothstep(0.0, 0.28, p.x) * smoothstep(0.0, 0.28, 1.0 - p.x);
	edges *= smoothstep(0.0, 0.22, p.y) * smoothstep(0.0, 0.26, 1.0 - p.y);
	float amount = edges * exp(-bank * bank) * smoothstep(0.18, 0.80, pockets);
	vec3 shade = mist_color.rgb;
	if (halo) {
		float radius = length(p - 0.5);
		amount = 1.0 - smoothstep(0.04, 0.49, radius);
		amount *= amount;
	}
	if (smoke) {
		float height = 1.0 - p.y;
		float billow = alley_noise(vec2(p.x * 6.0, p.y * 7.0 + elapsed * 0.58));
		float center = 0.34 + height * (0.16 + 0.22 * sin(height * 3.4 + elapsed * 0.70));
		float width = 0.07 + height * 0.31;
		float profile = abs(p.x - center + (billow - 0.5) * 0.19) / width;
		amount = edges * (1.0 - smoothstep(0.26, 1.0, profile)) * (0.39 + billow * 0.61);
		shade = mix(shade, vec3(0.38, 0.26, 0.19), exp(-height * 5.0) * 0.69);
	}
	ALBEDO = shade;
	ALPHA = amount * opacity;
}
